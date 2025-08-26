using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class PagamentoController : Controller
    {
        private readonly IPaymentService _payments;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;
        private readonly IConfiguration _cfg;
        private readonly IPaymentCredentialsResolver _creds;
        private readonly UserManager<ApplicationUser> _userManager;

        public PagamentoController(
            IPaymentService payments,
            IPedidoRepository pedidos,
            BancoContext db,
            IConfiguration cfg,
            IPaymentCredentialsResolver creds,
            UserManager<ApplicationUser> userManager)
        {
            _payments = payments;
            _pedidos = pedidos;
            _db = db;
            _cfg = cfg;
            _creds = creds;
            _userManager = userManager;
        }

        /// <summary>Checkout: cria/usa Payment e manda a PublicKey do MP para o Brick.</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Checkout(int pedidoId)
        {
            var pedido = _pedidos.ObterPorId(pedidoId);
            if (pedido == null) return NotFound();

            var metodo = MapMetodo(pedido.MetodoPagamento);

            var existing = await _db.Pagamentos
                .Where(p => p.PedidoId == pedido.Id && p.Metodo == metodo)
                .Where(p => p.Status != PaymentStatus.Paid &&
                            p.Status != PaymentStatus.Canceled &&
                            p.Status != PaymentStatus.Failed)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            var payment = existing ?? await _payments.StartPaymentAsync(pedido, metodo);

            // pega credenciais (usuário logado OU default da loja OU appsettings)
            var mpCreds = await _creds.GetAsync<MercadoPagoOptions>(User, "MercadoPago");
            ViewBag.MP_PublicKey = mpCreds?.PublicKey ?? _cfg["Payments:MercadoPago:PublicKey"];

            return View(payment);
        }

        private void MarcarPedidoComoPago(int pedidoId) =>
            _pedidos.AtualizarStatus(pedidoId, "Pago");

        /// <summary>Polling de status.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Status(int id)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            var isPaid = p.Status == PaymentStatus.Paid;

            return Json(new
            {
                paid = isPaid,
                status = p.Status.ToString(),
                last4 = p.CardLast4,
                brand = p.CardBrand,
                paidAt = p.PaidAt,
                pedidoId = p.PedidoId,
                redirect = isPaid
                    ? Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId })
                    : null
            });
        }

        /// <summary>Confirmação do cartão (payload do Brick).</summary>
        [HttpPost]
        [AllowAnonymous] // cliente é anônimo
        [ValidateAntiForgeryToken] // mantém proteção (enviar token no fetch)
        public async Task<IActionResult> ConfirmCard(int id, [FromBody] object clientPayload)
        {
            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString());

            var p = await _db.Pagamentos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            string? redirect = null;

            if (ok && p != null)
            {
                MarcarPedidoComoPago(p.PedidoId);
                redirect = Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId });
            }

            return Ok(new { success = ok, redirect });
        }

        /// <summary>Webhook do provedor (MP).</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            await _payments.ApplyWebhookAsync(Request);
            return Ok();
        }

        private static PaymentMethod MapMetodo(string metodo)
        {
            if (string.IsNullOrWhiteSpace(metodo)) return PaymentMethod.Cash;
            var s = metodo.Trim().ToLowerInvariant();
            return s switch
            {
                "dinheiro" => PaymentMethod.Cash,
                "pix" => PaymentMethod.Pix,
                "cartão de crédito" or "cartao de credito" => PaymentMethod.CardCredit,
                "cartão de débito" or "cartao de debito" => PaymentMethod.CardDebit,
                _ => PaymentMethod.Cash
            };
        }
    }
}
