using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApplicationPods.Constants;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class PedidoController : Controller
    {
        private readonly IPedidoRepository _pedidoRepository;

        public PedidoController(IPedidoRepository pedidoRepository)
        {
            _pedidoRepository = pedidoRepository;
        }

        public IActionResult Index() => View();

        private static bool CanViewPedido(PedidoModel p, ClaimsPrincipal user, string? token)
        {
            if (user?.Identity?.IsAuthenticated == true)
            {
                if (user.IsInRole("Lojista") || user.IsInRole("Admin")) return true;

                var cidStr = user.FindFirstValue("ClienteId");
                if (int.TryParse(cidStr, out var cid) && cid == p.ClienteId) return true;
            }

            if (!string.IsNullOrEmpty(token) &&
                string.Equals(token, p.RastreioToken, StringComparison.Ordinal))
                return true;

            return false;
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

            // retirada local: "Pronto" já representa etapa 4
            if (retiradaNoLocal && StatusEh(status, PedidoStatus.Pronto))
            {
                return 4;
            }

            // entrega: ainda não saiu, mas já está na fase logística
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

        // GET /Pedido/Acompanhar/123?t=TOKEN
        [HttpGet]
        [HttpGet]
        public IActionResult Acompanhar(int id, string? t = null)
        {
            if (id <= 0)
                return RedirectToAction("Index", "Home");

            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
                return RedirectToAction("Index", "Home");

            // Se existir token de rastreio no pedido, valida quando vier na URL
            if (!string.IsNullOrWhiteSpace(pedido.RastreioToken))
            {
                if (string.IsNullOrWhiteSpace(t) ||
                    !string.Equals(pedido.RastreioToken, t, StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Home");
                }
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

            var historico = _pedidoRepository
                .ObterHistorico(pedido.Id)
                .OrderByDescending(x => x.DataCadastro)
                .ToList();

            ViewBag.Ultimos = ultimosPedidos;
            ViewBag.Historico = historico;

            return View(pedido);
        }

        [HttpGet]
        public IActionResult StatusJson(int id, string? t = null)
        {
            if (id <= 0)
            {
                return Json(new
                {
                    ok = false,
                    message = "Pedido inválido."
                });
            }

            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null)
            {
                return Json(new
                {
                    ok = false,
                    message = "Pedido não encontrado."
                });
            }

            if (!string.IsNullOrWhiteSpace(pedido.RastreioToken))
            {
                if (string.IsNullOrWhiteSpace(t) ||
                    !string.Equals(pedido.RastreioToken, t, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new
                    {
                        ok = false,
                        message = "Token de rastreio inválido."
                    });
                }
            }

            int step = ObterStepPedido(pedido);

            var times = new Dictionary<string, string?>()
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
                step = step,
                times = times,
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
            if (pedido == null)
                return 0;

            return MapStep(pedido.Status, pedido.RetiradaNoLocal);
        }

        [HttpGet("Pedido/ResumoPedidoCliente/{id:int}")]
        [AllowAnonymous]
        public IActionResult ResumoPedidoCliente(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            if (!CanViewPedido(pedido, HttpContext.User, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction(nameof(Buscar));
            }
            ViewBag.Historico = _pedidoRepository.ObterHistorico(pedido.Id);
            return View("ResumoPedidoCliente", pedido);
        }

        // GET /Pedido/Status?id=123&t=TOKEN
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status(int id, string? t)
        {
            var p = _pedidoRepository.ObterPorId(id);
            if (p == null) return NotFound();
            if (!CanViewPedido(p, HttpContext.User, t)) return Unauthorized();

            var step = MapStep(p.Status, p.RetiradaNoLocal);
            var times = new Dictionary<string, string?>
            {
                ["0"] = p.DataPedido.ToString("o"),
                ["1"] = p.DataAguardandoPagamento?.ToString("o"),
                ["2"] = p.DataPagamentoAprovado?.ToString("o"),
                ["3"] = p.DataInicioPreparo?.ToString("o"),
                ["4"] = p.DataSaiuParaEntregaOuRetirada?.ToString("o"),
                ["5"] = p.DataConcluido?.ToString("o")
            };

            return Json(new
            {
                status = p.Status,
                step,
                times,
                serverTime = DateTime.UtcNow.ToString("o")
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Ultimo()
        {
            var user = HttpContext.User;

            if (user?.Identity?.IsAuthenticated == true && (user.IsInRole("Lojista") || user.IsInRole("Admin")))
                return RedirectToAction("Index", "PedidosAdmin", new { filtro = "dia" });

            if (user?.Identity?.IsAuthenticated == true)
            {
                var clienteIdStr = user.FindFirstValue("ClienteId");
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

            if (!CanViewPedido(pedido, HttpContext.User, token))
            {
                TempData["Erro"] = "Token inválido ou sem permissão.";
                return View();
            }

            return RedirectToAction(nameof(ResumoPedidoCliente), new { id = pedido.Id, t = token });
        }
    }
}
