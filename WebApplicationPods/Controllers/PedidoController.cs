using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class PedidoController : Controller
    {
        private readonly BancoContext _context;
        private readonly IPedidoRepository _pedidoRepository;

        public PedidoController(BancoContext context, IPedidoRepository pedidoRepository)
        {
            _pedidoRepository = pedidoRepository;
            _context = context;
        }

        public IActionResult Index() => View();

        // ===== Helpers =====
        private static bool CanViewPedido(PedidoModel p, ClaimsPrincipal user, string? token)
        {
            if (user?.Identity?.IsAuthenticated == true)
            {
                if (user.IsInRole("Lojista")) return true;

                var cidStr = user.FindFirstValue("ClienteId");
                if (int.TryParse(cidStr, out var cid) && cid == p.ClienteId) return true;
            }

            if (!string.IsNullOrEmpty(token) &&
                string.Equals(token, p.RastreioToken, System.StringComparison.Ordinal))
                return true;

            return false;
        }

        // GET /Pedido/Acompanhar/123?t=TOKEN
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Acompanhar(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            // Regra de acesso (lojista, dono, ou token correto)
            if (!CanViewPedido(pedido, HttpContext.User, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction(nameof(Buscar));
            }

            // ------- HISTÓRICO (ViewBag.Ultimos) -------
            IEnumerable<PedidoModel> ultimos = Enumerable.Empty<PedidoModel>();
            var user = HttpContext.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                if (user.IsInRole("Lojista"))
                {
                    // Lojista: histórico do mesmo cliente do pedido
                    ultimos = _pedidoRepository.ObterPorCliente(pedido.ClienteId)
                                .OrderByDescending(x => x.DataPedido)
                                .Take(10)
                                .ToList();
                }
                else
                {
                    // Cliente logado: histórico do próprio cliente
                    var cidStr = user.FindFirstValue("ClienteId");
                    if (int.TryParse(cidStr, out var cid) && cid > 0)
                    {
                        ultimos = _pedidoRepository.ObterPorCliente(cid)
                                    .OrderByDescending(x => x.DataPedido)
                                    .Take(10)
                                    .ToList();
                    }
                    else
                    {
                        ultimos = new[] { pedido }; // fallback
                    }
                }
            }
            else
            {
                // Anônimo: sempre mostra o atual.
                ultimos = new[] { pedido };

                // Se o cookie do último pedido bater com o token, libera histórico do mesmo cliente
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

            // URL de status para o polling na view (opcional, mas útil)
            ViewBag.StatusUrl = Url.Action("Status", "Pedido", new { id = pedido.Id, t = pedido.RastreioToken });

            return View(pedido);
        }


        [HttpGet("Pedido/ResumoPedidoCliente/{id:int}")]
        [AllowAnonymous]
        public IActionResult ResumoPedidoCliente(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            // mesma regra de permissão do Acompanhar
            if (!CanViewPedido(pedido, HttpContext.User, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction(nameof(Buscar));
            }

            // Renderiza explicitamente a view ResumoPedidoCliente.cshtml
            return View("ResumoPedidoCliente", pedido);
        }

        // GET /Pedido/Status?id=123&t=TOKEN   (usado pelo polling da view)
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Status(int id, string? t)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            if (!CanViewPedido(pedido, HttpContext.User, t))
                return Unauthorized();

            return Json(new
            {
                status = pedido.Status,
                total = pedido.ValorTotal,
                atualizado = pedido.DataPedido
            });
        }

        // Decide o destino: Admin (lojista), último pedido do cliente (logado),
        // ou, para anônimo, usa cookie last_order_token.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Ultimo()
        {
            var user = HttpContext.User;

            // Lojista autenticado -> tela do admin
            if (user?.Identity?.IsAuthenticated == true && user.IsInRole("Lojista"))
                return RedirectToAction("Index", "PedidosAdmin", new { filtro = "dia" });

            // Cliente autenticado -> procura último em andamento dele
            if (user?.Identity?.IsAuthenticated == true)
            {
                var clienteIdStr = user.FindFirstValue("ClienteId");
                if (int.TryParse(clienteIdStr, out var clienteId) && clienteId > 0)
                {
                    var finais = new[] { "Cancelado", "Pagamento Falhou", "Entregue", "Concluído" };

                    var pedido = _pedidoRepository
                        .ObterPorCliente(clienteId)
                        .Where(p => !string.IsNullOrEmpty(p.Status) &&
                                    !finais.Any(f => p.Status.Contains(f, System.StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(p => p.DataPedido)
                        .FirstOrDefault();

                    if (pedido != null)
                        return RedirectToAction(nameof(Acompanhar), new { id = pedido.Id, t = pedido.RastreioToken });
                }
            }

            // Anônimo -> tenta cookie com token
            var token = Request.Cookies["last_order_token"];
            if (!string.IsNullOrWhiteSpace(token))
            {
                var pedidoByToken = _pedidoRepository.ObterPorToken(token);
                if (pedidoByToken != null)
                    return RedirectToAction(nameof(Acompanhar), new { id = pedidoByToken.Id, t = token });
            }

            // Nada encontrado -> página de busca (ou mude p/ Home/Index)
            return RedirectToAction(nameof(Buscar));
        }

        // Página simples para localizar pedido manualmente (telefone + código, etc.)
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
