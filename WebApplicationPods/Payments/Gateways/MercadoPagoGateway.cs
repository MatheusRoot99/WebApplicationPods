using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;

namespace WebApplicationPods.Payments.Gateways
{
    public class MercadoPagoGateway : IPaymentGateway
    {
        private readonly HttpClient _http;
        private readonly string _accessToken;
        private readonly string _webhookSecret;

        // ✅ Construtor compatível com AddHttpClient<TService, TImpl>
        public MercadoPagoGateway(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _accessToken = cfg["Payments:MercadoPago:AccessToken"] ?? "";
            _webhookSecret = cfg["Payments:MercadoPago:WebhookSecret"] ?? "";

            // Base do MP (pode ajustar conforme API usada)
            if (_http.BaseAddress == null)
                _http.BaseAddress = new System.Uri("https://api.mercadopago.com/");

            if (!string.IsNullOrWhiteSpace(_accessToken))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        public Task<PixInitResult> CreatePixAsync(PedidoModel pedido)
        {
            // TODO: chamar API do MP usando _http
            return Task.FromResult(new PixInitResult
            {
                ProviderPaymentId = "pix_demo_123",
                QrData = "000201...EMV...",
                QrBase64Png = null
            });
        }

        public Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method)
        {
            // TODO: criar intenção/charge de cartão com _http
            return Task.FromResult(new CardInitResult
            {
                ProviderPaymentId = "card_demo_123",
                ClientSecretOrToken = "client_secret_demo"
            });
        }

        public Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
        {
            // TODO: confirmar com _http e retornar brand/last4 quando possível
            return Task.FromResult(new ConfirmCardResult
            {
                Success = true,
                Brand = "Visa",
                Last4 = "4242",
                FailureReason = null
            });
        }

        public Task<PaymentStatus> GetStatusAsync(string providerPaymentId)
        {
            // TODO: consultar _http
            return Task.FromResult(PaymentStatus.Paid);
        }

        public Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request)
        {
            // TODO: validar assinatura e parse do webhook
            var tuple = ("pix_demo_123", PaymentStatus.Paid, (decimal?)123.45m, (string)null);
            return Task.FromResult(tuple);
        }
    }
}
