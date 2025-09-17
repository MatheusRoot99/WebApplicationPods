using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;

namespace WebApplicationPods.Payments.Gateways
{
    /// <summary>
    /// Gera BR Code (PIX copia-e-cola) localmente a partir de uma chave Pix informada pelo lojista.
    /// Não possui conciliação automática; status permanece Pending até baixa manual.
    /// </summary>
    public class PixManualGateway : IPaymentGateway
    {
        public string Provider => "PixManual";

        private readonly IPaymentCredentialsResolver _resolver;
        private readonly IHttpContextAccessor _httpCtx;

        public PixManualGateway(IPaymentCredentialsResolver resolver, IHttpContextAccessor httpCtx)
        {
            _resolver = resolver;
            _httpCtx = httpCtx;
        }

        public Task<string> CreateCheckoutAsync(ClaimsPrincipal user, CheckoutRequest req)
    => Task.FromResult<string>(string.Empty);

        public Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method)
            => Task.FromResult(new CardInitResult());

        public Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
            => Task.FromResult(new ConfirmCardResult { Success = false });

        public Task<PaymentStatus> GetStatusAsync(string providerPaymentId)
            => Task.FromResult(PaymentStatus.Pending);

        public Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request)
            => Task.FromResult<(string, PaymentStatus, decimal?, string)>(("", PaymentStatus.Pending, null, "no_webhook"));

        // === PIX ===
        public async Task<PixInitResult> CreatePixAsync(PedidoModel pedido)
        {
            var user = _httpCtx.HttpContext?.User;
            var opts = await _resolver.GetAsync<PixManualOptions>(user!, Provider);
            if (opts == null || string.IsNullOrWhiteSpace(opts.PixKey))
                throw new InvalidOperationException("PixManual não configurado (PixKey ausente).");

            var amount = pedido.ValorTotal;
            var txid = (opts.TxIdPrefix ?? "PED").Trim();
            txid = (txid + pedido.Id).PadRight(1).Substring(0, Math.Min(25, (txid + pedido.Id).Length)); // máx 25

            var payload = BuildPixEmv(
                pixKey: opts.PixKey.Trim(),
                amount: amount,
                merchantName: Truncate(opts.MerchantName ?? opts.BeneficiaryName, 25),
                merchantCity: Truncate(string.IsNullOrWhiteSpace(opts.BeneficiaryCity) ? "BRASILIA" : opts.BeneficiaryCity, 15),
                txid: txid,
                description: $"Pedido {pedido.Id}"

            );

            return new PixInitResult
            {
                ProviderPaymentId = $"pixmanual-{pedido.Id}",
                QrData = payload,
                QrBase64Png = null // deixe a View gerar o QR (já fizemos fallback com qrcode.js)
            };
        }

        // ===== Geração de BR Code Pix (EMVco) =====
        private static string BuildPixEmv(string pixKey, decimal amount, string merchantName, string merchantCity, string txid, string? description)
        {
            // Campos EMV
            string PayloadFormatIndicator() => Emv("00", "01");
            string PointOfInitiationMethod() => Emv("01", "12"); // 12 = dinâmico / 11 = estático
            string MerchantAccountInfo()
            {
                var gui = Emv("00", "br.gov.bcb.pix");
                var key = Emv("01", pixKey);
                var desc = string.IsNullOrWhiteSpace(description) ? "" : Emv("02", description!);
                return Emv("26", gui + key + desc);
            }
            string MerchantCategoryCode() => Emv("52", "0000");
            string TransactionCurrency() => Emv("53", "986"); // BRL
            string TransactionAmount() => Emv("54", amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            string CountryCode() => Emv("58", "BR");
            string MerchantName() => Emv("59", merchantName);
            string MerchantCity() => Emv("60", merchantCity);
            string AdditionalDataField() => Emv("62", Emv("05", txid)); // txid

            var withoutCrc = PayloadFormatIndicator()
                           + PointOfInitiationMethod()
                           + MerchantAccountInfo()
                           + MerchantCategoryCode()
                           + TransactionCurrency()
                           + TransactionAmount()
                           + CountryCode()
                           + MerchantName()
                           + MerchantCity()
                           + AdditionalDataField()
                           + "6304"; // ID 63 (CRC) + length "04"

            var crc = Crc16(withoutCrc);
            return withoutCrc + crc;
        }

        private static string Emv(string id, string value)
        {
            var len = value.Length;
            return id + len.ToString("D2") + value;
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

        private static string Crc16(string input)
        {
            // CRC-16/CCITT-FALSE
            ushort polynomial = 0x1021;
            ushort crc = 0xFFFF;
            var bytes = Encoding.ASCII.GetBytes(input);

            foreach (var b in bytes)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ polynomial : crc << 1);
                }
            }
            return crc.ToString("X4");
        }
    }
}
