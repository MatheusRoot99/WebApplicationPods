using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore; // se usar EF
using WebApplicationPods.Models;
using WebApplicationPods.Enum;
using WebApplicationPods.Payments;
using WebApplicationPods.Repository.Interface; // IPedidoRepository
using WebApplicationPods.Data; // BancoContext

namespace WebApplicationPods.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentGateway _gateway;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;

        public PaymentService(IPaymentGateway gateway, IPedidoRepository pedidos, BancoContext db)
        {
            _gateway = gateway;
            _pedidos = pedidos;
            _db = db;
        }

        public async Task<PaymentModel> StartPaymentAsync(PedidoModel pedido, PaymentMethod metodo)
        {
            var payment = new PaymentModel
            {
                PedidoId = pedido.Id,
                Metodo = metodo,
                Amount = pedido.ValorTotal,
                Provider = _gateway.GetType().Name,
                Status = metodo == PaymentMethod.Cash ? PaymentStatus.Pending : PaymentStatus.Created
            };
            _db.Add(payment);
            await _db.SaveChangesAsync();

            if (metodo == PaymentMethod.Pix)
            {
                var r = await _gateway.CreatePixAsync(pedido);
                payment.ProviderPaymentId = r.ProviderPaymentId;
                payment.PixQrData = r.QrData;
                payment.PixQrBase64Png = r.QrBase64Png;
                payment.Status = PaymentStatus.Pending;
            }
            else if (metodo == PaymentMethod.CardCredit || metodo == PaymentMethod.CardDebit)
            {
                var r = await _gateway.CreateCardPaymentAsync(pedido, metodo);
                payment.ProviderPaymentId = r.ProviderPaymentId;
                payment.Status = PaymentStatus.RequiresAction;
            }
            else if (metodo == PaymentMethod.Cash)
            {
                payment.Status = PaymentStatus.Pending;
            }

            await _db.SaveChangesAsync();
            return payment;
        }

        public async Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson)
        {
            var payment = await _db.Set<PaymentModel>().FindAsync(paymentId);
            if (payment == null) return false;

            var ok = await _gateway.ConfirmCardPaymentAsync(payment.ProviderPaymentId, clientPayloadJson);
            payment.Status = ok ? PaymentStatus.Paid : PaymentStatus.Failed;
            if (ok) payment.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Ajuste conforme seu repositório (async/sync)
            var pedido = await _pedidos.ObterPorIdAsync(payment.PedidoId);
            if (pedido != null)
            {
                pedido.Status = ok ? "Pago" : "Pagamento Falhou";
                await _pedidos.AtualizarAsync(pedido);
            }

            return ok;
        }

        public async Task ApplyWebhookAsync(HttpRequest request)
        {
            var (providerPaymentId, newStatus, paidAmount, extra) = await _gateway.HandleWebhookAsync(request);

            var payment = _db.Set<PaymentModel>().FirstOrDefault(p => p.ProviderPaymentId == providerPaymentId);
            if (payment == null) return;

            payment.Status = newStatus;
            if (newStatus == PaymentStatus.Paid) payment.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var pedido = await _pedidos.ObterPorIdAsync(payment.PedidoId);
            if (pedido != null)
            {
                pedido.Status = newStatus == PaymentStatus.Paid ? "Pago" :
                                newStatus == PaymentStatus.Failed ? "Pagamento Falhou" :
                                newStatus == PaymentStatus.Canceled ? "Cancelado" :
                                "Pendente";
                await _pedidos.AtualizarAsync(pedido);
            }
        }
    }
}
