using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApplicationPods.Models;
using WebApplicationPods.Enum;

namespace WebApplicationPods.Payments
{
    public class PixInitResult
    {
        public string ProviderPaymentId { get; set; }
        public string QrData { get; set; }          // EMV "copia e cola"
        public string QrBase64Png { get; set; }     // opcional
    }

    public class CardInitResult
    {
        public string ProviderPaymentId { get; set; }
        // Hosted Fields/Elements: token/secret/intentId para o front concluir
        public string ClientSecretOrToken { get; set; }
    }

    public interface IPaymentGateway
    {
        Task<PixInitResult> CreatePixAsync(PedidoModel pedido);
        Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method /*Credit/Debit*/);

        // Confirmação do cartão (envia o token/nonce/3DS que veio do front)
        Task<bool> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson);

        // Consulta status no provedor
        Task<PaymentStatus> GetStatusAsync(string providerPaymentId);

        // Webhook do provedor → devolve id + novo status (e dados extras)
        Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request);
    }
}
