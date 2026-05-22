using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;
using Microsoft.AspNetCore.SignalR;

namespace WebApplicationPods.Controllers
{
    [ApiController]
    [Route("webhooks/stripe")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly BancoContext _db;
        private readonly IConfiguration _cfg;
        private readonly IPedidoAppService _pedidoAppService;
        private readonly IEstoqueService _estoque;
        private readonly IHubContext<PedidosHub> _hub;

        public StripeWebhookController(
            BancoContext db,
            IConfiguration cfg,
            IPedidoAppService pedidoAppService,
            IEstoqueService estoque,
            IHubContext<PedidosHub> hub)
        {
            _db = db;
            _cfg = cfg;
            _pedidoAppService = pedidoAppService;
            _estoque = estoque;
            _hub = hub;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Receive()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signatureHeader = Request.Headers["Stripe-Signature"].FirstOrDefault();
            var webhookSecret = _cfg["Payments:Stripe:WebhookSecret"];

            if (string.IsNullOrWhiteSpace(webhookSecret))
                return StatusCode(StatusCodes.Status500InternalServerError, "Stripe WebhookSecret não configurado.");

            if (string.IsNullOrWhiteSpace(signatureHeader))
                return BadRequest("Header Stripe-Signature ausente.");

            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signatureHeader,
                    webhookSecret
                );
            }
            catch (StripeException)
            {
                return BadRequest("Assinatura Stripe inválida.");
            }
            catch (Exception)
            {
                return BadRequest("Webhook Stripe inválido.");
            }

            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    await AplicarPaymentIntentSucceeded(stripeEvent);
                    break;

                case "payment_intent.payment_failed":
                    await AplicarPaymentIntentFailed(stripeEvent);
                    break;

                case "payment_intent.canceled":
                    await AplicarPaymentIntentCanceled(stripeEvent);
                    break;
            }

            return Ok();
        }

        private async Task AplicarPaymentIntentSucceeded(Event stripeEvent)
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;
            if (intent == null || string.IsNullOrWhiteSpace(intent.Id))
                return;

            var payment = await _db.Pagamentos
                .Include(x => x.Pedido)
                .ThenInclude(p => p.Cliente)
                .FirstOrDefaultAsync(x =>
                    x.Provider == "Stripe" &&
                    x.ProviderPaymentId == intent.Id);

            if (payment == null)
                return;

            if (payment.Status == PaymentStatus.Paid)
                return;

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(intent.LatestChargeId))
            {
                payment.ProviderOrderId = intent.LatestChargeId;
            }

            await _db.SaveChangesAsync();

            await _pedidoAppService.MarcarComoPagoAsync(
                payment.PedidoId,
                origem: "StripeWebhook.payment_intent.succeeded");

            await BaixarEstoqueSeNecessario(payment.PedidoId);

            if (payment.Pedido != null)
                await NotificarPedidoPago(payment.Pedido);
        }

        private async Task AplicarPaymentIntentFailed(Event stripeEvent)
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;
            if (intent == null || string.IsNullOrWhiteSpace(intent.Id))
                return;

            var payment = await _db.Pagamentos
                .FirstOrDefaultAsync(x =>
                    x.Provider == "Stripe" &&
                    x.ProviderPaymentId == intent.Id);

            if (payment == null)
                return;

            if (payment.Status == PaymentStatus.Paid)
                return;

            payment.Status = PaymentStatus.Failed;

            await _db.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                payment.PedidoId,
                "Pagamento Falhou",
                observacao: "Pagamento recusado pela Stripe.",
                origem: "StripeWebhook.payment_intent.payment_failed");
        }

        private async Task AplicarPaymentIntentCanceled(Event stripeEvent)
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;
            if (intent == null || string.IsNullOrWhiteSpace(intent.Id))
                return;

            var payment = await _db.Pagamentos
                .FirstOrDefaultAsync(x =>
                    x.Provider == "Stripe" &&
                    x.ProviderPaymentId == intent.Id);

            if (payment == null)
                return;

            if (payment.Status == PaymentStatus.Paid)
                return;

            payment.Status = PaymentStatus.Canceled;
            payment.CanceledAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                payment.PedidoId,
                "Cancelado",
                observacao: "Pagamento cancelado pela Stripe.",
                origem: "StripeWebhook.payment_intent.canceled");
        }

        private async Task BaixarEstoqueSeNecessario(int pedidoId)
        {
            var existeNaoBaixado = await _db.Set<PedidoItemModel>()
                .AnyAsync(i => i.PedidoId == pedidoId && !i.EstoqueBaixado);

            if (existeNaoBaixado)
                await _estoque.BaixarEstoquePedidoAsync(pedidoId);
        }

        private Task NotificarPedidoPago(PedidoModel pedido)
        {
            return _hub.Clients.Group("lojistas").SendAsync("NewOrder", new
            {
                id = pedido.Id,
                cliente = pedido.Cliente?.Nome ?? $"Cliente #{pedido.ClienteId}",
                valor = pedido.ValorTotal,
                quando = pedido.DataPedido.ToString("o"),
                metodo = pedido.MetodoPagamento,
                status = "Pago",
                retirada = pedido.RetiradaNoLocal
            });
        }
    }
}