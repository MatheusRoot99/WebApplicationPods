namespace WebApplicationPods.Payments.Options
{
    public class MercadoPagoOptions
    {
        public string AccessToken { get; set; } = "";  // do painel do MP
        public string PublicKey { get; set; } = "";   // se usar hosted fields
        public string? BaseUrl { get; set; } = "https://api.mercadopago.com/";
        public string? WebhookSecret { get; set; }     // se for validar assinatura
    }
}
