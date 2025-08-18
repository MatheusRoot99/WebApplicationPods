using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;                 // BancoContext
using WebApplicationPods.Enum;                 // PaymentMethod / PaymentStatus
using WebApplicationPods.Models;               // PedidoModel / PaymentModel
using WebApplicationPods.Payments;             // IPaymentService
using WebApplicationPods.Repository.Interface; // IPedidoRepository

namespace WebApplicationPods.Controllers
{
    public class PagamentoController : Controller
    {
        private readonly IPaymentService _payments;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;
        private readonly IConfiguration _cfg;

        public PagamentoController(
            IPaymentService payments,
            IPedidoRepository pedidos,
            BancoContext db,
            IConfiguration cfg)
        {
            _payments = payments;
            _pedidos = pedidos;
            _db = db;
            _cfg = cfg;
        }

        /// <summary>
        /// Página de checkout: recebe o pedidoId, cria (ou reaproveita) um Payment
        /// e envia a PublicKey do MP para a View montar o Brick.
        /// </summary>
        public async Task<IActionResult> Checkout(int pedidoId)
        {
            var pedido = _pedidos.ObterPorId(pedidoId);
            if (pedido == null) return NotFound();

            var metodo = MapMetodo(pedido.MetodoPagamento);

            // Reaproveita pagamento pendente (se ainda não foi pago/cancelado/falhou)
            var existing = await _db.Pagamentos
                .Where(p => p.PedidoId == pedido.Id && p.Metodo == metodo)
                .Where(p => p.Status != PaymentStatus.Paid &&
                            p.Status != PaymentStatus.Canceled &&
                            p.Status != PaymentStatus.Failed)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            var payment = existing ?? await _payments.StartPaymentAsync(pedido, metodo);

            // Public Key do Mercado Pago usada no <script> da View
            ViewBag.MP_PublicKey = _cfg["Payments:MercadoPago:PublicKey"]?.Trim();

            return View(payment);
        }

        /// <summary>
        /// Endpoint para polling do status (PIX / cartão).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Status(int id)
        {
            var p = await _db.Pagamentos.FindAsync(id);
            if (p == null) return NotFound();

            return Json(new
            {
                status = p.Status.ToString(),
                last4 = p.CardLast4,
                brand = p.CardBrand,
                paidAt = p.PaidAt
            });
        }

        /// <summary>
        /// Confirmação de cartão: recebe o payload gerado pelo Brick/Hosted Fields.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ConfirmCard(int id, [FromBody] object clientPayload)
        {
            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString());
            return Ok(new { success = ok });
        }

        /// <summary>
        /// Webhook do provedor de pagamento (ex.: Mercado Pago).
        /// Configure a URL pública no painel do gateway.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            await _payments.ApplyWebhookAsync(Request);
            return Ok();
        }

        /// <summary>
        /// Converte a string do pedido para o enum do domínio.
        /// </summary>
        private static PaymentMethod MapMetodo(string metodo)
        {
            if (string.IsNullOrWhiteSpace(metodo)) return PaymentMethod.Cash;
            var s = metodo.Trim().ToLowerInvariant();
            return s switch
            {
                "dinheiro" => PaymentMethod.Cash,
                "pix" => PaymentMethod.Pix,
                "cartão de crédito" => PaymentMethod.CardCredit,
                "cartao de credito" => PaymentMethod.CardCredit,
                "cartão de débito" => PaymentMethod.CardDebit,
                "cartao de debito" => PaymentMethod.CardDebit,
                _ => PaymentMethod.Cash
            };
        }
    }
}
