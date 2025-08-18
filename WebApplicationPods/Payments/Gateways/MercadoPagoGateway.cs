using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Models;
using WebApplicationPods.Enum;
using WebApplicationPods.Payments;

namespace WebApplicationPods.Payments.Gateways
{
    public class MercadoPagoGateway : IPaymentGateway
    {
        private readonly string _accessToken;
        private readonly string _webhookSecret;

        public MercadoPagoGateway(IConfiguration cfg)
        {
            _accessToken = cfg["Payments:MercadoPago:AccessToken"];
            _webhookSecret = cfg["Payments:MercadoPago:WebhookSecret"];
            // TODO: inicializar SDK do MP com _accessToken
        }

        public Task<PixInitResult> CreatePixAsync(PedidoModel pedido)
        {
            // TODO: chamar API do MP para criar cobrança PIX e retornar EMV / QR
            return Task.FromResult(new PixInitResult
            {
                ProviderPaymentId = "pix_demo_123",
                QrData = "000201...EMV...",
                QrBase64Png = null
            });
        }

        public Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method)
        {
            // TODO: criar intent/charge de cartão e retornar client secret/token p/ front
            return Task.FromResult(new CardInitResult
            {
                ProviderPaymentId = "card_demo_123",
                ClientSecretOrToken = "client_secret_demo"
            });
        }

        public Task<bool> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
        {
            // TODO: confirmar pagamento de cartão usando o token/nonce do front
            return Task.FromResult(true);
        }

        public Task<PaymentStatus> GetStatusAsync(string providerPaymentId)
        {
            // TODO: consultar status no MP
            return Task.FromResult(PaymentStatus.Paid);
        }

        public Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request)
        {
            // TODO: validar assinatura (se aplicável), ler JSON do MP e mapear status
            var result = ("pix_demo_123", PaymentStatus.Paid, (decimal?)123.45m, (string)null);
            return Task.FromResult(result);
        }
    }
}
