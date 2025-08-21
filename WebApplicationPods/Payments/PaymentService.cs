using Microsoft.AspNetCore.Http;
using WebApplicationPods.Data;              // BancoContext
using WebApplicationPods.Enum;              // PaymentMethod, PaymentStatus
using WebApplicationPods.Models;            // PaymentModel, PedidoModel
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly Func<string, IPaymentGateway> _gatewayFactory;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;

        public PaymentService(Func<string, IPaymentGateway> gatewayFactory,
                              IPedidoRepository pedidos,
                              BancoContext db)
        {
            _gatewayFactory = gatewayFactory;
            _pedidos = pedidos;
            _db = db;
        }

        public async Task<PaymentModel> StartPaymentAsync(PedidoModel pedido, PaymentMethod metodo)
        {
            // 1) Escolhe o provedor (ajuste a regra conforme sua preferência)
            var provider = metodo switch
            {
                PaymentMethod.CardCredit => "Stripe",
                PaymentMethod.CardDebit => "Stripe",
                PaymentMethod.Pix => "Stripe",    // troque para "MercadoPago" se preferir
                PaymentMethod.Cash => "None",
                _ => "Stripe"
            };

            var gateway = provider == "None" ? null : _gatewayFactory(provider);

            // 2) Cria o registro do pagamento
            var payment = new PaymentModel
            {
                PedidoId = pedido.Id,
                Metodo = metodo,
                Amount = pedido.ValorTotal,
                Provider = provider ?? string.Empty,
                Status = metodo == PaymentMethod.Cash ? PaymentStatus.Pending : PaymentStatus.Created
            };

            // Evita NULL em colunas NOT NULL
            payment.CardBrand ??= string.Empty;
            payment.CardLast4 ??= string.Empty;
            payment.Provider ??= string.Empty;
            payment.ProviderPaymentId ??= string.Empty;
            payment.ProviderOrderId ??= string.Empty;

            _db.Add(payment);
            await _db.SaveChangesAsync();

            // 3) Chama o gateway escolhido
            if (metodo == PaymentMethod.Pix && gateway is not null)
            {
                var r = await gateway.CreatePixAsync(pedido);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.PixQrData = r.QrData ?? string.Empty;
                payment.PixQrBase64Png = r.QrBase64Png; // pode ser null, ok
                payment.Status = PaymentStatus.Pending;
            }
            else if ((metodo == PaymentMethod.CardCredit || metodo == PaymentMethod.CardDebit) && gateway is not null)
            {
                var r = await gateway.CreateCardPaymentAsync(pedido, metodo);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.ClientSecretOrToken = r.ClientSecretOrToken ?? string.Empty;
                payment.Status = PaymentStatus.RequiresAction; // espera confirmação no front
            }
            else if (metodo == PaymentMethod.Cash)
            {
                payment.Status = PaymentStatus.Pending;
            }

            // Garantia extra antes de salvar
            payment.CardBrand ??= string.Empty;
            payment.CardLast4 ??= string.Empty;

            await _db.SaveChangesAsync();
            return payment;
        }

        public async Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson)
        {
            var payment = await _db.Set<PaymentModel>().FindAsync(paymentId);
            if (payment == null) return false;

            // Usa o provedor salvo no pagamento
            var gateway = _gatewayFactory(payment.Provider);
            var result = await gateway.ConfirmCardPaymentAsync(payment.ProviderPaymentId, clientPayloadJson);

            payment.Status = result.Success ? PaymentStatus.Paid : PaymentStatus.Failed;
            if (result.Success)
            {
                payment.PaidAt = DateTime.UtcNow;
                payment.CardBrand = string.IsNullOrWhiteSpace(result.Brand) ? null : result.Brand;
                payment.CardLast4 = string.IsNullOrWhiteSpace(result.Last4) ? null : result.Last4;
            }

            await _db.SaveChangesAsync();

            var pedido = _pedidos.ObterPorId(payment.PedidoId);
            if (pedido != null)
            {
                _pedidos.AtualizarStatus(pedido.Id, result.Success ? "Pago" : "Pagamento Falhou");
            }

            return result.Success;
        }

        public async Task ApplyWebhookAsync(HttpRequest request)
        {
            // Se você tiver rotas separadas (/webhooks/stripe e /webhooks/mp), chame direto o gateway certo
            // Aqui fazemos um dispatcher simples: tenta Stripe; se inválido, tenta MP.

            (string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra) parsed;

            // 1) Tenta Stripe
            try
            {
                var stripe = _gatewayFactory("Stripe");
                parsed = await stripe.HandleWebhookAsync(request);
                if (!string.IsNullOrWhiteSpace(parsed.providerPaymentId) && parsed.extra != "invalid-signature")
                {
                    await ApplyWebhookParsedAsync(parsed);
                    return;
                }
            }
            catch
            {
                // ignora e tenta o próximo
            }

            // 2) Tenta Mercado Pago
            try
            {
                var mp = _gatewayFactory("MercadoPago");
                parsed = await mp.HandleWebhookAsync(request);
                if (!string.IsNullOrWhiteSpace(parsed.providerPaymentId) && parsed.extra != "invalid-signature")
                {
                    await ApplyWebhookParsedAsync(parsed);
                    return;
                }
            }
            catch
            {
                // nenhum provedor reconheceu
            }
        }

        private async Task ApplyWebhookParsedAsync((string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra) parsed)
        {
            var payment = _db.Set<PaymentModel>().FirstOrDefault(p => p.ProviderPaymentId == parsed.providerPaymentId);
            if (payment == null) return;

            // Idempotência simples: não regride status já 'Paid'
            if (payment.Status == PaymentStatus.Paid && parsed.newStatus != PaymentStatus.Paid)
                return;

            payment.Status = parsed.newStatus;
            if (parsed.newStatus == PaymentStatus.Paid)
                payment.PaidAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var statusTexto =
                parsed.newStatus == PaymentStatus.Paid ? "Pago" :
                parsed.newStatus == PaymentStatus.Failed ? "Pagamento Falhou" :
                parsed.newStatus == PaymentStatus.Canceled ? "Cancelado" :
                                                             "Pendente";

            _pedidos.AtualizarStatus(payment.PedidoId, statusTexto);
        }
    }
}
