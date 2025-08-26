using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly Func<string, IPaymentGateway> _gatewayFactory;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;
        private readonly IHttpContextAccessor _http;

        public PaymentService(
            Func<string, IPaymentGateway> gatewayFactory,
            IPedidoRepository pedidos,
            BancoContext db,
            IHttpContextAccessor http)
        {
            _gatewayFactory = gatewayFactory;
            _pedidos = pedidos;
            _db = db;
            _http = http;
        }

        private int GetUserId()
            => int.TryParse(_http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        /// <summary>
        /// Decide o provider pelo que foi salvo na tela de Config (tabela MerchantPaymentConfigs).
        /// Se não houver nada salvo, usa "MercadoPago" por padrão.
        /// </summary>
        private string ResolveProviderForCurrentUser()
        {
            var userId = GetUserId();
            var cfg = _db.MerchantPaymentConfigs.FirstOrDefault(x => x.UserId == userId);
            return string.IsNullOrWhiteSpace(cfg?.Provider) ? "MercadoPago" : cfg!.Provider;
        }

        public async Task<PaymentModel> StartPaymentAsync(PedidoModel pedido, PaymentMethod metodo)
        {
            var provider = metodo == PaymentMethod.Cash ? "None" : ResolveProviderForCurrentUser();
            var gateway = provider == "None" ? null : _gatewayFactory(provider);

            // Cria o registro do pagamento
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

            // Fluxo por método
            if (metodo == PaymentMethod.Pix && gateway is not null)
            {
                var r = await gateway.CreatePixAsync(pedido);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.PixQrData = r.QrData ?? string.Empty;
                payment.PixQrBase64Png = r.QrBase64Png; // pode ser null
                payment.Status = PaymentStatus.Pending;
            }
            else if ((metodo == PaymentMethod.CardCredit || metodo == PaymentMethod.CardDebit) && gateway is not null)
            {
                var r = await gateway.CreateCardPaymentAsync(pedido, metodo);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.ClientSecretOrToken = r.ClientSecretOrToken ?? string.Empty;
                payment.Status = PaymentStatus.RequiresAction; // aguarda confirmação no front
            }
            else if (metodo == PaymentMethod.Cash)
            {
                payment.Status = PaymentStatus.Pending;
            }

            // Garantia extra
            payment.CardBrand ??= string.Empty;
            payment.CardLast4 ??= string.Empty;

            await _db.SaveChangesAsync();
            return payment;
        }

        public async Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson)
        {
            var payment = await _db.Set<PaymentModel>().FindAsync(paymentId);
            if (payment == null) return false;

            if (string.IsNullOrWhiteSpace(payment.Provider))
                payment.Provider = ResolveProviderForCurrentUser();

            if (string.Equals(payment.Provider, "None", StringComparison.OrdinalIgnoreCase))
                return false;

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
            // Somente Mercado Pago (evita acionar Stripe à toa)
            var mp = _gatewayFactory("MercadoPago");
            var parsed = await mp.HandleWebhookAsync(request);
            await ApplyWebhookParsedAsync(parsed);
        }

        private async Task ApplyWebhookParsedAsync((string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra) parsed)
        {
            if (string.IsNullOrWhiteSpace(parsed.providerPaymentId)) return;

            var payment = _db.Set<PaymentModel>().FirstOrDefault(p => p.ProviderPaymentId == parsed.providerPaymentId);
            if (payment == null) return;

            // Idempotência básica
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
