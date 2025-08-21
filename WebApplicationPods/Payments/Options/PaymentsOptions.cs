namespace WebApplicationPods.Payments.Options
{
    public class PaymentsOptions
    {
        public MercadoPagoOptions MercadoPago { get; set; } = new();
        public StripeOptions Stripe { get; set; } = new();
    }
}
