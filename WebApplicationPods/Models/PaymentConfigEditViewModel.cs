using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class PaymentConfigEditViewModel
    {
        [Required] public string Provider { get; set; } = "Stripe"; // ou MercadoPago

        // Stripe
        public string? StripePublishableKey { get; set; }
        public string? StripeSecretKey { get; set; }
        public string? StripeWebhookSecret { get; set; }

        // Mercado Pago
        public string? MpPublicKey { get; set; }
        public string? MpAccessToken { get; set; }
        public string? MpWebhookSecret { get; set; }
    }
}
