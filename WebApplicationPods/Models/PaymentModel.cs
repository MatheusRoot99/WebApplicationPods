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
        public string Provider { get; set; } = string.Empty;
        public string ProviderPaymentId { get; set; } = string.Empty;
        public string ProviderOrderId { get; set; } = string.Empty;

        // PIX
        public string PixQrData { get; set; } = string.Empty;
        public string? PixQrBase64Png { get; set; }

        // Cartão (✅ agora opcionais)
        public string? ClientSecretOrToken { get; set; }
        public string? CardBrand { get; set; }      // <--
        public string? CardLast4 { get; set; }      // <--
        public int? Installments { get; set; }

        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? CanceledAt { get; set; }
        // ⏳ expiração do PIX
        public DateTime? ExpiresAt { get; set; }
        public PedidoModel Pedido { get; set; }
    }
}
