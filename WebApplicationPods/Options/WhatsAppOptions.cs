namespace WebApplicationPods.Options
{
    public class WhatsAppOptions
    {
        public bool Enabled { get; set; } = true;
        public string Mode { get; set; } = "Stub"; // Stub | MetaCloudApi
        public string DefaultCountryCode { get; set; } = "55";

        public bool SendToCustomer { get; set; } = true;
        public bool SendToEntregador { get; set; } = true;
        public bool SendToLojista { get; set; } = false;

        public string MetaApiVersion { get; set; } = "v25.0";
        public string? MetaPhoneNumberId { get; set; }
        public string? MetaAccessToken { get; set; }
        public string? MetaWebhookVerifyToken { get; set; }

        // App Secret do aplicativo Meta.
        // Quando preenchido, o webhook POST pode validar a assinatura X-Hub-Signature-256.
        public string? MetaAppSecret { get; set; }
    }
}