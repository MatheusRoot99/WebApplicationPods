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
        Task<PixInitResult> CreatePixAsync(PedidoModel pedido);

        /// <summary>Inicializa um pagamento por cartão e retorna o client secret/token, quando aplicável.</summary>
        Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method);

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
        /// <summary>ID do pagamento gerado no provedor.</summary>
        public string ProviderPaymentId { get; set; } = string.Empty;

        /// <summary>
        /// Dado que o front usará para renderizar o QR (em Stripe normalmente é o client_secret;
        /// em outros provedores pode ser o payload do QR Code).
        /// </summary>
        public string QrData { get; set; } = string.Empty;

        /// <summary>PNG do QR em Base64, quando o provedor já devolve renderizado (opcional).</summary>
        public string? QrBase64Png { get; set; }
    }

    /// <summary>Retorno da inicialização de pagamento por cartão.</summary>
    public class CardInitResult
    {
        /// <summary>ID do pagamento gerado no provedor.</summary>
        public string ProviderPaymentId { get; set; } = string.Empty;

        /// <summary>Client secret/token para confirmação no front (quando aplicável).</summary>
        public string? ClientSecretOrToken { get; set; }
    }

    /// <summary>Parâmetros para criar uma sessão de checkout hospedada.</summary>
    public class CheckoutRequest
    {
        /// <summary>Moeda (ex.: BRL).</summary>
        public string Currency { get; set; } = "BRL";

        /// <summary>Descrição do pedido a aparecer no provedor.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Valor total do pedido.</summary>
        public decimal Amount { get; set; }

        /// <summary>URL de sucesso (redirecionamento após pagamento concluído).</summary>
        public string SuccessUrl { get; set; } = string.Empty;

        /// <summary>URL de cancelamento.</summary>
        public string CancelUrl { get; set; } = string.Empty;
    }

    /// <summary>Resultado da confirmação do pagamento por cartão.</summary>
    
}
