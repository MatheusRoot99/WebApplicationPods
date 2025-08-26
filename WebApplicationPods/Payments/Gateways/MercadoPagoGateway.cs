using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options; // <-- credenciais tipadas (MercadoPagoCredentials)

namespace WebApplicationPods.Payments.Gateways
{
    public class MercadoPagoGateway : IPaymentGateway
    {
        private readonly HttpClient _http;
        private readonly IPaymentCredentialsResolver _resolver;
        private readonly IHttpContextAccessor _httpCtx;
        private readonly IConfiguration _cfg;

        public string Provider => "MercadoPago";

        public MercadoPagoGateway(
            HttpClient http,
            IPaymentCredentialsResolver resolver,
            IHttpContextAccessor httpCtx,
            IConfiguration cfg)
        {
            _http = http;
            _resolver = resolver;
            _httpCtx = httpCtx;
            _cfg = cfg;

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri("https://api.mercadopago.com/");
        }

        // >>> Agora assíncrono e usando GetAsync<T>(user,"MercadoPago")
        private async Task<string> ResolveAccessTokenAsync()
        {
            var user = _httpCtx.HttpContext?.User;
            var creds = await _resolver.GetAsync<MercadoPagoOptions>(user!, "MercadoPago");
            return creds?.AccessToken
                   ?? _cfg["Payments:MercadoPago:AccessToken"]
                   ?? string.Empty;
        }

        // --------- PIX ----------
        public async Task<PixInitResult> CreatePixAsync(PedidoModel pedido)
        {
            var token = await ResolveAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Mercado Pago AccessToken não configurado.");

            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/payments");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                transaction_amount = pedido.ValorTotal,
                description = $"Pedido #{pedido.Id}",
                payment_method_id = "pix",
                payer = new { email = "comprador@teste.com" } // ajuste conforme seu fluxo
            };
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            var txt = await resp.Content.ReadAsStringAsync();
            resp.EnsureSuccessStatusCode();

            // TODO: parse real do retorno do MP (qr_code, qr_code_base64, id etc.)
            return new PixInitResult
            {
                ProviderPaymentId = "mp_pix_id_demo",
                QrData = "000201...EMV...",
                QrBase64Png = null
            };
        }

        // --------- Cartão ----------
        public async Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method)
        {
            var token = await ResolveAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Mercado Pago AccessToken não configurado.");

            // Com Bricks, normalmente você usa os dados do front para confirmar no backend.
            return await Task.FromResult(new CardInitResult
            {
                ProviderPaymentId = "mp_card_id_demo",
                ClientSecretOrToken = "client_secret_demo"
            });
        }

        public async Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
        {
            var token = await ResolveAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Mercado Pago AccessToken não configurado.");

            // TODO: confirmar no MP com os dados do clientPayloadJson
            return await Task.FromResult(new ConfirmCardResult
            {
                Success = true,
                Brand = "Visa",
                Last4 = "4242"
            });
        }

        public Task<PaymentStatus> GetStatusAsync(string providerPaymentId)
            => Task.FromResult(PaymentStatus.Paid);

        public Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request)
        {
            // TODO: validar assinatura e parse real do webhook do MP
            var tuple = ("mp_pix_id_demo", PaymentStatus.Paid, (decimal?)null, "");
            return Task.FromResult(tuple);
        }

        // --------- Checkout Pro ----------
        public async Task<string> CreateCheckoutAsync(ClaimsPrincipal user, CheckoutRequest req)
        {
            // Aqui podemos usar diretamente o user que chegou por parâmetro:
            var creds = await _resolver.GetAsync<MercadoPagoOptions>(user, "MercadoPago");
            var token = creds?.AccessToken ?? _cfg["Payments:MercadoPago:AccessToken"];
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Mercado Pago AccessToken não configurado.");

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

            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences");
            reqMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            reqMsg.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(reqMsg);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("init_point", out var initPointEl) &&
                initPointEl.ValueKind == JsonValueKind.String)
                return initPointEl.GetString()!;

            if (doc.RootElement.TryGetProperty("sandbox_init_point", out var sandboxInitEl) &&
                sandboxInitEl.ValueKind == JsonValueKind.String)
                return sandboxInitEl.GetString()!;

            throw new InvalidOperationException("Não foi possível obter a URL de checkout do Mercado Pago.");
        }

        // ------------ DTOs para a Preference ------------
        private sealed class PreferenceCreateRequest
        {
            [JsonPropertyName("items")] public PreferenceItem[] Items { get; set; } = Array.Empty<PreferenceItem>();
            [JsonPropertyName("back_urls")] public PreferenceBackUrls? BackUrls { get; set; }
            [JsonPropertyName("auto_return")] public string? AutoReturn { get; set; }
        }
        private sealed class PreferenceItem
        {
            [JsonPropertyName("title")] public string Title { get; set; } = "";
            [JsonPropertyName("quantity")] public int Quantity { get; set; }
            [JsonPropertyName("unit_price")] public decimal UnitPrice { get; set; }
            [JsonPropertyName("currency_id")] public string CurrencyId { get; set; } = "BRL";
        }
        private sealed class PreferenceBackUrls
        {
            [JsonPropertyName("success")] public string? Success { get; set; }
            [JsonPropertyName("failure")] public string? Failure { get; set; }
            [JsonPropertyName("pending")] public string? Pending { get; set; }
        }
    }
}
