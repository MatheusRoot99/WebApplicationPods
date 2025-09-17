using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly Func<string, IPaymentGateway> _gatewayFactory;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;
        private readonly IHubContext<PedidosHub> _hub;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPaymentCredentialsResolver _creds;

        public PaymentService(
            Func<string, IPaymentGateway> gatewayFactory,
            IPedidoRepository pedidos,
            BancoContext db,
            IHubContext<PedidosHub> hub,
            IHttpContextAccessor httpContextAccessor,
            IPaymentCredentialsResolver creds)
        {
            _gatewayFactory = gatewayFactory;
            _pedidos = pedidos;
            _db = db;
            _hub = hub;
            _httpContextAccessor = httpContextAccessor;
            _creds = creds;
        }

        // ===== Helpers de provedor =====
        private async Task<string> ResolvePixProviderAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var pixManual = await _creds.GetAsync<WebApplicationPods.Payments.Options.PixManualOptions>(user!, "PixManual");
            if (pixManual != null && !string.IsNullOrWhiteSpace(pixManual.PixKey))
                return "PixManual";
            return "MercadoPago";
        }

        private async Task<string> ResolveCardProviderAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            var mp = await _creds.GetAsync<WebApplicationPods.Payments.Options.MercadoPagoOptions>(user!, "MercadoPago");
            var mpOk = mp != null && !string.IsNullOrWhiteSpace(mp.PublicKey) && !string.IsNullOrWhiteSpace(mp.AccessToken);

            var st = await _creds.GetAsync<WebApplicationPods.Payments.Options.StripeOptions>(user!, "Stripe");
            var stOk = st != null && !string.IsNullOrWhiteSpace(st.PublishableKey) && !string.IsNullOrWhiteSpace(st.SecretKey);

            if (stOk) return "Stripe";
            if (mpOk) return "MercadoPago";
            return "Stripe";
        }

        // ===== Cálculo do total (itens + frete − descontos) =====
        private static decimal TotalParaCobranca(PedidoModel p)
        {
            var subtotal = p.ValorTotal;

            // tenta encontrar campos comuns de frete/desconto por nome
            decimal frete = TryGetDecimalProp(p, "TaxaEntrega")
                          + TryGetDecimalProp(p, "ValorEntrega")
                          + TryGetDecimalProp(p, "Frete");

            decimal desconto = TryGetDecimalProp(p, "Desconto")
                             + TryGetDecimalProp(p, "ValorDesconto");

            var total = subtotal + frete - desconto;
            return total < 0 ? 0 : total;
        }

        private static decimal TryGetDecimalProp(object obj, string propName)
        {
            var pi = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) return 0m;
            var raw = pi.GetValue(obj);
            if (raw == null) return 0m;
            try { return Convert.ToDecimal(raw, CultureInfo.InvariantCulture); }
            catch { return 0m; }
        }

        // ===== Fluxo principal =====
        public async Task<PaymentModel> StartPaymentAsync(PedidoModel pedido, PaymentMethod metodo)
        {
            var provider =
                 metodo == PaymentMethod.Cash ? "None" :
                 metodo == PaymentMethod.Pix ? await ResolvePixProviderAsync() :
                 (metodo == PaymentMethod.CardCredit || metodo == PaymentMethod.CardDebit) ? await ResolveCardProviderAsync() :
                 "None";

            var gateway = provider == "None" ? null : _gatewayFactory(provider);

            // valor final que será cobrado (inclui frete/descontos)
            var amount = TotalParaCobranca(pedido);

            var payment = new PaymentModel
            {
                PedidoId = pedido.Id,
                Metodo = metodo,
                Amount = amount,
                Provider = provider ?? string.Empty,
                Status = metodo == PaymentMethod.Cash ? PaymentStatus.Pending : PaymentStatus.Created,
                CardBrand = string.Empty,
                CardLast4 = string.Empty,
                ProviderPaymentId = string.Empty,
                ProviderOrderId = string.Empty
            };

            _db.Add(payment);
            await _db.SaveChangesAsync();

            if (metodo == PaymentMethod.Pix && gateway is not null)
            {
                var r = await gateway.CreatePixAsync(pedido, amount);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.PixQrData = r.QrData ?? string.Empty;
                payment.PixQrBase64Png = r.QrBase64Png;
                payment.Status = PaymentStatus.Pending;
            }
            else if ((metodo == PaymentMethod.CardCredit || metodo == PaymentMethod.CardDebit) && gateway is not null)
            {
                var r = await gateway.CreateCardPaymentAsync(pedido, metodo, amount);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.ClientSecretOrToken = r.ClientSecretOrToken ?? string.Empty;
                payment.Status = PaymentStatus.RequiresAction;
            }
            else if (metodo == PaymentMethod.Cash)
            {
                _pedidos.AtualizarStatus(pedido.Id, "Aguardando Confirmação (Dinheiro)");
                payment.Status = PaymentStatus.Pending;

                await _hub.Clients.Group("lojistas").SendAsync("PedidosChanged",
                    new { id = pedido.Id, status = "Aguardando Confirmação (Dinheiro)" });
            }

            await _db.SaveChangesAsync();
            return payment;
        }

        public async Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson)
        {
            var payment = await _db.Set<PaymentModel>().FindAsync(paymentId);
            if (payment == null) return false;

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

                await _hub.Clients.Group("lojistas").SendAsync("PedidosChanged",
                    new { id = pedido.Id, status = result.Success ? "Pago" : "Pagamento Falhou" });
            }

            return result.Success;
        }

        public async Task ApplyWebhookAsync(HttpRequest request)
        {
            var mp = _gatewayFactory("MercadoPago");
            var parsed = await mp.HandleWebhookAsync(request);
            await ApplyWebhookParsedAsync(parsed);
        }

        private async Task ApplyWebhookParsedAsync((string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra) parsed)
        {
            if (string.IsNullOrWhiteSpace(parsed.providerPaymentId)) return;

            var payment = await _db.Set<PaymentModel>()
                .FirstOrDefaultAsync(p => p.ProviderPaymentId == parsed.providerPaymentId);
            if (payment == null) return;

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

            await _hub.Clients.Group("lojistas").SendAsync("PedidosChanged",
                new { id = payment.PedidoId, status = statusTexto });
        }
    }
}
