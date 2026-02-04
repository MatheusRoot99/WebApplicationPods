using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

        private static int MapStep(string? status)
        {
            var s = (status ?? string.Empty).ToLowerInvariant();
            if (s.Contains("cancel")) return -1;
            if (s.Contains("entreg") || s.Contains("concl")) return 5;
            if (s.Contains("rota") || s.Contains("saiu") || s.Contains("entrega") || s.Contains("retirada")) return 4;
            if (s.Contains("prepar") || s.Contains("produção") || s.Contains("producao")) return 3;
            if (s.Contains("pago") || s.Contains("aprov")) return 2;
            if (s.Contains("aguard") && s.Contains("pag")) return 1;
            return 0;
        }

        // GET /Pedido/Acompanhar/123?t=TOKEN
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Acompanhar(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            if (!CanViewPedido(pedido, HttpContext.User, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction(nameof(Buscar));
            }

            IEnumerable<PedidoModel> ultimos = new[] { pedido };

            var user = HttpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                if (user.IsInRole("Lojista") || user.IsInRole("Admin"))
                {
                    ultimos = _pedidoRepository.ObterPorCliente(pedido.ClienteId)
                        .OrderByDescending(x => x.DataPedido)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    var cidStr = user.FindFirstValue("ClienteId");
                    if (int.TryParse(cidStr, out var cid) && cid > 0)
                    {
                        ultimos = _pedidoRepository.ObterPorCliente(cid)
                            .OrderByDescending(x => x.DataPedido)
                            .Take(10)
                            .ToList();
                    }
                }
            }
            else
            {
                var cookieToken = Request.Cookies["last_order_token"];
                if (!string.IsNullOrEmpty(cookieToken) &&
                    string.Equals(cookieToken, pedido.RastreioToken, StringComparison.Ordinal))
                {
                    ultimos = _pedidoRepository.ObterPorCliente(pedido.ClienteId)
                        .OrderByDescending(x => x.DataPedido)
                        .Take(10)
                        .ToList();
                }
            }

            ViewBag.Ultimos = ultimos;
            ViewBag.StatusUrl = Url.Action("Status", "Pedido", new { id = pedido.Id, t = pedido.RastreioToken });

            return View(pedido);
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

            var step = MapStep(p.Status);
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
