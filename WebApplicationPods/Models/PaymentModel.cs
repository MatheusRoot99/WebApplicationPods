using WebApplicationPods.Enum;

namespace WebApplicationPods.Models
{
    public class PaymentModel
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public PaymentMethod Metodo { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Created;

        public decimal Amount { get; set; }
        public string Provider { get; set; }          // "MercadoPago", "PagarMe", etc.
        public string ProviderPaymentId { get; set; } // id/intent/charge do gateway
        public string ProviderOrderId { get; set; }   // se o provedor separar order/charge

        // Pix
        public string PixQrData { get; set; }         // texto EMV (copia e cola)
        public string PixQrBase64Png { get; set; }    // se o provedor retornar a imagem (opcional)

        // Cartão
        public string CardBrand { get; set; }
        public string CardLast4 { get; set; }
        public int? Installments { get; set; }

        // Auditoria / logs
        public string FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? CanceledAt { get; set; }

        // Navegação
        public PedidoModel Pedido { get; set; }
    }
}
