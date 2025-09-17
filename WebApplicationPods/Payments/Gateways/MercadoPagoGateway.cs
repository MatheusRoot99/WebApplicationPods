using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;

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

        private async Task<string> ResolveAccessTokenAsync()
        {
            var user = _httpCtx.HttpContext?.User;

            var creds = await _resolver.GetAsync<MercadoPagoOptions>(user!, "MercadoPago");
            var token = creds?.AccessToken;

            token ??= _cfg["Payments:MercadoPago:AccessToken"];

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Mercado Pago AccessToken não configurado (vazio).");

            if (token.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("APP_USR-", StringComparison.OrdinalIgnoreCase))
            {
                return token;
            }

            throw new InvalidOperationException(
                "AccessToken do Mercado Pago parece inválido. " +
                "Ele deve começar com 'TEST-' (sandbox) ou 'APP_USR-' (produção).");
        }

        // =================== PIX ===================
        public async Task<PixInitResult> CreatePixAsync(PedidoModel pedido, decimal amount)
        {
            var token = await ResolveAccessTokenAsync();

            var body = new
            {
                transaction_amount = amount,                // <<< usa o total calculado
                description = $"Pedido #{pedido.Id}",
                payment_method_id = "pix"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "v1/payments");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            var txt = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Mercado Pago /v1/payments falhou: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {txt}");
            }

            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idEl)
                ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString() : idEl.GetString() ?? "")
                : "";

            string qr = "";
            string? qrBase64 = null;

            if (root.TryGetProperty("point_of_interaction", out var poi) &&
                poi.ValueKind == JsonValueKind.Object &&
                poi.TryGetProperty("transaction_data", out var td) &&
                td.ValueKind == JsonValueKind.Object)
            {
                if (td.TryGetProperty("qr_code", out var qrEl) && qrEl.ValueKind == JsonValueKind.String)
                    qr = qrEl.GetString() ?? "";

                if (td.TryGetProperty("qr_code_base64", out var b64El) && b64El.ValueKind == JsonValueKind.String)
                    qrBase64 = b64El.GetString();
            }

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(qr))
                throw new InvalidOperationException("Não foi possível obter os dados do QR do PIX.");

            return new PixInitResult
            {
                ProviderPaymentId = id,
                QrData = qr,
                QrBase64Png = qrBase64
            };
        }

        // =================== CARTÃO (stub, se usar Stripe p/ cartão) ===================
        public Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method, decimal amount)
            => Task.FromResult(new CardInitResult
            {
                ProviderPaymentId = "mp_card_id_demo",
                ClientSecretOrToken = "client_secret_demo"
            });

        public Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
            => Task.FromResult(new ConfirmCardResult
            {
                Success = true,
                Brand = "Visa",
                Last4 = "4242"
            });

        // =================== STATUS ===================
        public async Task<PaymentStatus> GetStatusAsync(string providerPaymentId)
        {
            var token = await ResolveAccessTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{providerPaymentId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req);
            var txt = await resp.Content.ReadAsStringAsync();
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(txt);
            var status = doc.RootElement.GetProperty("status").GetString() ?? "";
            return MapMpStatus(status);
        }

        // =================== WEBHOOK ===================
        public async Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request)
        {
            string? id = null;
            string? type = null;
            try
            {
                using var sr = new StreamReader(request.Body, Encoding.UTF8);
                var body = await sr.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.String)
                        type = tEl.GetString();

                    if (root.TryGetProperty("data", out var dEl) &&
                        dEl.ValueKind == JsonValueKind.Object &&
                        dEl.TryGetProperty("id", out var idEl))
                    {
                        id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString() : idEl.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(id) && root.TryGetProperty("resource", out var resEl) &&
                        resEl.ValueKind == JsonValueKind.String)
                    {
                        var res = resEl.GetString() ?? "";
                        var lastSlash = res.LastIndexOf('/');
                        if (lastSlash >= 0 && lastSlash + 1 < res.Length)
                            id = res.Substring(lastSlash + 1);
                        type ??= "payment";
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(id))
                id = request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(type))
                type = request.Query["type"].FirstOrDefault();

            if (!string.Equals(type, "payment", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(id))
                return ("", PaymentStatus.Pending, null, "evento_ignorado");

            var status = await GetStatusAsync(id);
            return (id, status, null, "");
        }

        private static PaymentStatus MapMpStatus(string s) => s?.ToLowerInvariant() switch
        {
            "approved" => PaymentStatus.Paid,
            "authorized" => PaymentStatus.Pending,
            "in_process" => PaymentStatus.Pending,
            "in_mediation" => PaymentStatus.Pending,
            "rejected" => PaymentStatus.Failed,
            "cancelled" or "canceled" => PaymentStatus.Canceled,
            "refunded" => PaymentStatus.Canceled,
            "charged_back" => PaymentStatus.Canceled,
            _ => PaymentStatus.Pending
        };

        // ===== Checkout Pro (mantido) =====
        public async Task<string> CreateCheckoutAsync(System.Security.Claims.ClaimsPrincipal user, CheckoutRequest req)
        {
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

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences");
            reqMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            reqMsg.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(reqMsg);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("init_point", out var initPointEl) && initPointEl.ValueKind == JsonValueKind.String)
                return initPointEl.GetString()!;

            if (doc.RootElement.TryGetProperty("sandbox_init_point", out var sandboxInitEl) && sandboxInitEl.ValueKind == JsonValueKind.String)
                return sandboxInitEl.GetString()!;

            throw new InvalidOperationException("Não foi possível obter a URL de checkout do Mercado Pago.");
        }

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
