using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApplicationPods.Constants;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers
{
    public class PedidoController : Controller
    {
        private readonly IPedidoRepository _pedidoRepository;
        private readonly ICurrentLojaService _currentLoja;

        public PedidoController(
            IPedidoRepository pedidoRepository,
            ICurrentLojaService currentLoja)
        {
            _pedidoRepository = pedidoRepository;
            _currentLoja = currentLoja;
        }

        public IActionResult Index() => View();

        private int? ObterLojaAtual()
        {
            if (_currentLoja.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            var lojaIdClaim = User.FindFirst("LojaId")?.Value
                           ?? User.FindFirst("lojaId")?.Value;

            if (int.TryParse(lojaIdClaim, out var lojaId) && lojaId > 0)
                return lojaId;

            return null;
        }

        private bool CanViewPedido(PedidoModel pedido, string? token)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                    return true;

                if (User.IsInRole("Lojista"))
                {
                    var lojaAtual = ObterLojaAtual();
                    return lojaAtual.HasValue &&
                           lojaAtual.Value > 0 &&
                           pedido.LojaId == lojaAtual.Value;
                }

                var clienteIdStr = User.FindFirstValue("ClienteId");
                if (int.TryParse(clienteIdStr, out var clienteId) &&
                    clienteId > 0 &&
                    clienteId == pedido.ClienteId)
                {
                    return true;
                }
            }

            return TokenConfere(pedido, token);
        }

        private static bool TokenConfere(PedidoModel pedido, string? token)
        {
            return !string.IsNullOrWhiteSpace(token) &&
                   !string.IsNullOrWhiteSpace(pedido.RastreioToken) &&
                   string.Equals(token, pedido.RastreioToken, StringComparison.Ordinal);
        }

        private static bool StatusEh(string? atual, string esperado)
        {
            return string.Equals(atual?.Trim(), esperado, StringComparison.OrdinalIgnoreCase);
        }

        private static int MapStep(string? status, bool retiradaNoLocal = false)
        {
            if (string.IsNullOrWhiteSpace(status))
                return 0;

            if (status.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("Falhou", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            if (StatusEh(status, PedidoStatus.Concluido) ||
                StatusEh(status, PedidoEntregaStatus.Entregue))
            {
                return 5;
            }

            if (StatusEh(status, PedidoStatus.SaiuParaEntrega) ||
                StatusEh(status, PedidoEntregaStatus.SaiuParaEntrega))
            {
                return 4;
            }

            if (retiradaNoLocal && StatusEh(status, PedidoStatus.Pronto))
                return 4;

            if (StatusEh(status, PedidoStatus.Pronto) ||
                StatusEh(status, PedidoEntregaStatus.AguardandoAtribuicao) ||
                StatusEh(status, PedidoEntregaStatus.Atribuido))
            {
                return 4;
            }

            if (StatusEh(status, PedidoStatus.EmPreparacao) ||
                status.Contains("Produ", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (StatusEh(status, PedidoStatus.Pago) ||
                status.Contains("Aprov", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (status.Contains("Aguard", StringComparison.OrdinalIgnoreCase) &&
                status.Contains("Pag", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        [HttpGet]
        public IActionResult Acompanhar(int id, string? t = null)
        {
            if (id <= 0)
                return RedirectToAction("Index", "Home");

            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
                return RedirectToAction("Index", "Home");

            if (!CanViewPedido(pedido, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction(nameof(Buscar));
            }

            var ultimosPedidos = Enumerable.Empty<PedidoModel>();

            if (pedido.ClienteId > 0)
            {
                ultimosPedidos = _pedidoRepository
                    .ObterPorCliente(pedido.ClienteId)
                    .Where(x => x.Id != pedido.Id)
                    .OrderByDescending(x => x.DataPedido)
                    .Take(10)
                    .ToList();
            }

            ViewBag.Ultimos = ultimosPedidos;
            ViewBag.Historico = _pedidoRepository
                .ObterHistorico(pedido.Id)
                .OrderByDescending(x => x.DataCadastro)
                .ToList();

            return View(pedido);
        }

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusJson(int id, string? t = null)
        {
            if (id <= 0)
                return Json(new { ok = false, message = "Pedido inválido." });

            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
                return Json(new { ok = false, message = "Pedido não encontrado." });

            if (!CanViewPedido(pedido, t))
                return Json(new { ok = false, message = "Token de rastreio inválido ou sem permissão." });

            var times = new Dictionary<string, string?>
            {
                ["0"] = pedido.DataPedido != default ? pedido.DataPedido.ToString("o") : null,
                ["1"] = pedido.DataAguardandoPagamento?.ToString("o"),
                ["2"] = pedido.DataPagamentoAprovado?.ToString("o"),
                ["3"] = pedido.DataInicioPreparo?.ToString("o"),
                ["4"] = pedido.DataSaiuParaEntregaOuRetirada?.ToString("o"),
                ["5"] = pedido.DataConcluido?.ToString("o")
            };

            return Json(new
            {
                ok = true,
                id = pedido.Id,
                status = pedido.Status,
                step = ObterStepPedido(pedido),
                times,
                dataPedido = pedido.DataPedido,
                dataAguardandoPagamento = pedido.DataAguardandoPagamento,
                dataPagamentoAprovado = pedido.DataPagamentoAprovado,
                dataInicioPreparo = pedido.DataInicioPreparo,
                dataSaiuParaEntregaOuRetirada = pedido.DataSaiuParaEntregaOuRetirada,
                dataConcluido = pedido.DataConcluido,
                dataCancelado = pedido.DataCancelado,
                retiradaNoLocal = pedido.RetiradaNoLocal
            });
        }

        private static int ObterStepPedido(PedidoModel pedido)
        {
            return MapStep(pedido.Status, pedido.RetiradaNoLocal);
        }

        [HttpGet("Pedido/ResumoPedidoCliente/{id:int}")]
        [AllowAnonymous]
        public IActionResult ResumoPedidoCliente(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
                return NotFound();

            if (!CanViewPedido(pedido, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction(nameof(Buscar));
            }

            ViewBag.Historico = _pedidoRepository.ObterHistorico(pedido.Id);
            return View("ResumoPedidoCliente", pedido);
        }

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
                return NotFound();

            if (!CanViewPedido(pedido, t))
                return Unauthorized();

            var times = new Dictionary<string, string?>
            {
                ["0"] = pedido.DataPedido.ToString("o"),
                ["1"] = pedido.DataAguardandoPagamento?.ToString("o"),
                ["2"] = pedido.DataPagamentoAprovado?.ToString("o"),
                ["3"] = pedido.DataInicioPreparo?.ToString("o"),
                ["4"] = pedido.DataSaiuParaEntregaOuRetirada?.ToString("o"),
                ["5"] = pedido.DataConcluido?.ToString("o")
            };

            return Json(new
            {
                status = pedido.Status,
                step = MapStep(pedido.Status, pedido.RetiradaNoLocal),
                times,
                serverTime = DateTime.UtcNow.ToString("o")
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Ultimo()
        {
            if (User?.Identity?.IsAuthenticated == true &&
                (User.IsInRole("Lojista") || User.IsInRole("Admin")))
            {
                return RedirectToAction("Index", "PedidosAdmin", new { filtro = "dia" });
            }

            if (User?.Identity?.IsAuthenticated == true)
            {
                var clienteIdStr = User.FindFirstValue("ClienteId");
                if (int.TryParse(clienteIdStr, out var clienteId) && clienteId > 0)
                {
                    var finais = new[] { "Cancelado", "Pagamento Falhou", "Entregue", "Concluído" };

                    var pedido = _pedidoRepository
                        .ObterPorCliente(clienteId)
                        .Where(p => !string.IsNullOrEmpty(p.Status) &&
                                    !finais.Any(f => p.Status.Contains(f, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(p => p.DataPedido)
                        .FirstOrDefault();

                    if (pedido != null)
                        return RedirectToAction(nameof(Acompanhar), new { id = pedido.Id, t = pedido.RastreioToken });
                }
            }

            var token = Request.Cookies["last_order_token"];
            if (!string.IsNullOrWhiteSpace(token))
            {
                var pedidoByToken = _pedidoRepository.ObterPorToken(token);
                if (pedidoByToken != null)
                    return RedirectToAction(nameof(Acompanhar), new { id = pedidoByToken.Id, t = token });
            }

            return RedirectToAction(nameof(Buscar));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Buscar() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Buscar(int id, string token)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
            {
                TempData["Erro"] = "Pedido não encontrado.";
                return View();
            }

            if (!CanViewPedido(pedido, token))
            {
                TempData["Erro"] = "Token inválido ou sem permissão.";
                return View();
            }

            return RedirectToAction(nameof(ResumoPedidoCliente), new { id = pedido.Id, t = token });
        }
    }
}
