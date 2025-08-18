using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;

namespace WebApplicationPods.Payments
{
    public interface IPaymentGateway
    {
        Task<PixInitResult> CreatePixAsync(PedidoModel pedido);
        Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method);

        // ✅ alterado: de Task<bool> para Task<ConfirmCardResult>
        Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson);

        Task<PaymentStatus> GetStatusAsync(string providerPaymentId);
        Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
            HandleWebhookAsync(HttpRequest request);
    }

    public class PixInitResult
    {
        public string ProviderPaymentId { get; set; }
        public string QrData { get; set; }
        public string? QrBase64Png { get; set; }
    }

    public class CardInitResult
    {
        public string ProviderPaymentId { get; set; }
        public string? ClientSecretOrToken { get; set; }
    }
}
