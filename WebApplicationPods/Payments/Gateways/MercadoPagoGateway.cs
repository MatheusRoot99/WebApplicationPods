using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
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

        public string Provider => "MercadoPago";

        // ✅ compatível com AddHttpClient<MercadoPagoGateway>()
        public MercadoPagoGateway(HttpClient http, IConfiguration cfg)
        {
            _http = http;

            _accessToken = cfg["Payments:MercadoPago:AccessToken"] ?? "";
            _webhookSecret = cfg["Payments:MercadoPago:WebhookSecret"] ?? "";

            if (_http.BaseAddress == null)
                _http.BaseAddress = new System.Uri("https://api.mercadopago.com/");

            if (!string.IsNullOrWhiteSpace(_accessToken))
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        // --------- PIX ----------
        public Task<PixInitResult> CreatePixAsync(PedidoModel pedido)
        {
            // TODO: chame a API de PIX do MP conforme o fluxo que você escolher
            return Task.FromResult(new PixInitResult
            {
                ProviderPaymentId = "pix_demo_123",
                QrData = "000201...EMV...",
                QrBase64Png = null
            });
        }

        // --------- Cartão (server-side init) ----------
        public Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method)
        {
            // TODO: implemente o fluxo de cartão do MP (Payments / Payment Intent)
            return Task.FromResult(new CardInitResult
            {
                ProviderPaymentId = "card_demo_123",
                ClientSecretOrToken = "client_secret_demo"
            });
        }

        public Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
        {
            // TODO: confirmar com a API do MP e popular brand/last4 quando disponíveis
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
            // TODO: consultar status no MP
            return Task.FromResult(PaymentStatus.Paid);
        }

        public Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request)
        {
            // TODO: validar assinatura do webhook do MP (_webhookSecret) e fazer o parse real
            var tuple = ("pix_demo_123", PaymentStatus.Paid, (decimal?)123.45m, "");
            return Task.FromResult(tuple);
        }

        // --------- Checkout Pro (URL hospedada pelo MP) ----------
        public async Task<string> CreateCheckoutAsync(ClaimsPrincipal user, CheckoutRequest req)
        {
            // monta a preference mínima
            var body = new PreferenceCreateRequest
            {
                Items = new[]
                {
                    new PreferenceItem
                    {
                        Title = string.IsNullOrWhiteSpace(req.Description) ? "Pedido" : req.Description,
                        Quantity = 1,
                        UnitPrice = req.Amount,
                        CurrencyId = (req.Currency ?? "BRL").ToUpper()
                    }
                },
                BackUrls = new PreferenceBackUrls
                {
                    Success = req.SuccessUrl ?? "",
                    Failure = req.CancelUrl ?? "",
                    Pending = req.SuccessUrl ?? ""
                },
                AutoReturn = "approved"
            };

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("checkout/preferences", content);
            resp.EnsureSuccessStatusCode();

            // resposta tem "init_point" (prod) e "sandbox_init_point" (sandbox).
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            // prioriza init_point
            if (doc.RootElement.TryGetProperty("init_point", out var initPointEl) &&
                initPointEl.ValueKind == JsonValueKind.String)
            {
                return initPointEl.GetString()!;
            }

            if (doc.RootElement.TryGetProperty("sandbox_init_point", out var sandboxInitEl) &&
                sandboxInitEl.ValueKind == JsonValueKind.String)
            {
                return sandboxInitEl.GetString()!;
            }

            // fallback: se não veio nenhuma URL, falha de forma clara
            throw new System.InvalidOperationException("Não foi possível obter a URL de checkout do Mercado Pago.");
        }

        // ------------ DTOs (snake_case) para a Preference ------------
        private sealed class PreferenceCreateRequest
        {
            [JsonPropertyName("items")]
            public PreferenceItem[] Items { get; set; } = System.Array.Empty<PreferenceItem>();

            [JsonPropertyName("back_urls")]
            public PreferenceBackUrls? BackUrls { get; set; }

            [JsonPropertyName("auto_return")]
            public string? AutoReturn { get; set; }
        }

        private sealed class PreferenceItem
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = "";

            [JsonPropertyName("quantity")]
            public int Quantity { get; set; }

            [JsonPropertyName("unit_price")]
            public decimal UnitPrice { get; set; }

            [JsonPropertyName("currency_id")]
            public string CurrencyId { get; set; } = "BRL";
        }

        private sealed class PreferenceBackUrls
        {
            [JsonPropertyName("success")]
            public string? Success { get; set; }
            [JsonPropertyName("failure")]
            public string? Failure { get; set; }
            [JsonPropertyName("pending")]
            public string? Pending { get; set; }
        }
    }
}
