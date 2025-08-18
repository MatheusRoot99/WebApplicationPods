
using WebApplicationPods.Data;              // BancoContext
using WebApplicationPods.Enum;              // PaymentMethod, PaymentStatus
using WebApplicationPods.Models;            // PaymentModel, PedidoModel
using WebApplicationPods.Repository.Interface;

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

            // 🔸 EVITA NULL EM COLUNAS NOT NULL
            payment.CardBrand = payment.CardBrand ?? string.Empty;
            payment.CardLast4 = payment.CardLast4 ?? string.Empty;
            payment.Provider = payment.Provider ?? string.Empty;
            payment.ProviderPaymentId = payment.ProviderPaymentId ?? string.Empty;
            payment.ProviderOrderId = payment.ProviderOrderId ?? string.Empty;

            _db.Add(payment);
            await _db.SaveChangesAsync();

            if (metodo == PaymentMethod.Pix)
            {
                var r = await _gateway.CreatePixAsync(pedido);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.PixQrData = r.QrData ?? string.Empty;
                payment.PixQrBase64Png = r.QrBase64Png; // pode ficar null, se sua coluna permitir
                payment.Status = PaymentStatus.Pending;
            }
            else if (metodo == PaymentMethod.CardCredit || metodo == PaymentMethod.CardDebit)
            {
                var r = await _gateway.CreateCardPaymentAsync(pedido, metodo);
                payment.ProviderPaymentId = r.ProviderPaymentId ?? string.Empty;
                payment.ClientSecretOrToken = r.ClientSecretOrToken ?? string.Empty;
                payment.Status = PaymentStatus.RequiresAction; // aguardando dados do cartão no front
            }
            else if (metodo == PaymentMethod.Cash)
            {
                payment.Status = PaymentStatus.Pending;
            }

            // 🔸 GARANTIA EXTRA ANTES DE SALVAR
            payment.CardBrand ??= string.Empty;
            payment.CardLast4 ??= string.Empty;

            await _db.SaveChangesAsync();
            return payment;
        }


        public async Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson)
        {
            var payment = await _db.Set<PaymentModel>().FindAsync(paymentId);
            if (payment == null) return false;

            var result = await _gateway.ConfirmCardPaymentAsync(payment.ProviderPaymentId, clientPayloadJson);

            payment.Status = result.Success ? PaymentStatus.Paid : PaymentStatus.Failed;
            if (result.Success)
            {
                payment.PaidAt = DateTime.UtcNow;
                payment.CardBrand = string.IsNullOrWhiteSpace(result.Brand) ? null : result.Brand;
                payment.CardLast4 = string.IsNullOrWhiteSpace(result.Last4) ? null : result.Last4;
            }

            await _db.SaveChangesAsync();

            var pedido = _pedidos.ObterPorId(payment.PedidoId);
            if (pedido != null)
            {
                _pedidos.AtualizarStatus(pedido.Id, result.Success ? "Pago" : "Pagamento Falhou");
            }

            return result.Success;
        }

        public async Task ApplyWebhookAsync(HttpRequest request)
        {
            var (providerPaymentId, newStatus, paidAmount, extra) = await _gateway.HandleWebhookAsync(request);

            var payment = _db.Set<PaymentModel>().FirstOrDefault(p => p.ProviderPaymentId == providerPaymentId);
            if (payment == null) return;

            payment.Status = newStatus;
            if (newStatus == PaymentStatus.Paid) payment.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ✅ mapeia status do provedor para o texto do seu domínio
            var statusTexto =
                newStatus == PaymentStatus.Paid ? "Pago" :
                newStatus == PaymentStatus.Failed ? "Pagamento Falhou" :
                newStatus == PaymentStatus.Canceled ? "Cancelado" :
                                                      "Pendente";

            _pedidos.AtualizarStatus(payment.PedidoId, statusTexto);
        }
    }
}
