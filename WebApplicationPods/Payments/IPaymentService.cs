using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApplicationPods.Models;
using WebApplicationPods.Enum;

namespace WebApplicationPods.Payments
{
    public interface IPaymentService
    {
        Task<PaymentModel> StartPaymentAsync(PedidoModel pedido, PaymentMethod metodo);
        Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson);
        Task ApplyWebhookAsync(HttpRequest request);
    }
}
