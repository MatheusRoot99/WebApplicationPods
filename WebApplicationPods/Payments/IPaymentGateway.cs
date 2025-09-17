#nullable enable
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;

namespace WebApplicationPods.Payments
{
    /// <summary>
    /// Contrato para gateways de pagamento (Stripe, MercadoPago, etc.).
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>Inicializa um pagamento via PIX e retorna dados para exibir o QR no front.</summary>
        Task<PixInitResult> CreatePixAsync(PedidoModel pedido, decimal amount);

        /// <summary>Inicializa um pagamento por cartão e retorna o client secret/token, quando aplicável.</summary>
        Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method, decimal amount);

        /// <summary>Confirma um pagamento de cartão (server side), usando o ID do provedor e payload do cliente.</summary>
        Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson);

        /// <summary>Consulta o status atual no provedor.</summary>
        Task<PaymentStatus> GetStatusAsync(string providerPaymentId);

        /// <summary>Processa o webhook do provedor e retorna dados resumidos do evento.</summary>
        Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request);

        /// <summary>Nome do provedor (ex.: "MercadoPago", "Stripe").</summary>
        string Provider { get; }

        /// <summary>Cria uma sessão de checkout hospedada no provedor (quando suportado) e retorna a URL.</summary>
        Task<string> CreateCheckoutAsync(ClaimsPrincipal user, CheckoutRequest req);
    }

    /// <summary>Retorno da inicialização do PIX.</summary>
    public class PixInitResult
    {
        public string ProviderPaymentId { get; set; } = string.Empty;
        public string QrData { get; set; } = string.Empty;
        public string? QrBase64Png { get; set; }
    }

    /// <summary>Retorno da inicialização de pagamento por cartão.</summary>
    public class CardInitResult
    {
        public string ProviderPaymentId { get; set; } = string.Empty;
        public string? ClientSecretOrToken { get; set; }
    }

    /// <summary>Parâmetros para criar uma sessão de checkout hospedada.</summary>
    public class CheckoutRequest
    {
        public string Currency { get; set; } = "BRL";
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }
}
