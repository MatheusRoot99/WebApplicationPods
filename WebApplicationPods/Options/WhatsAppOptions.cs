namespace WebApplicationPods.Options
{
    public class WhatsAppOptions
    {
        public bool Enabled { get; set; } = true;
        public string Mode { get; set; } = "Stub"; // Stub | GenericWebhook
        public string DefaultCountryCode { get; set; } = "55";

        public bool SendToCustomer { get; set; } = true;
        public bool SendToEntregador { get; set; } = true;
        public bool SendToLojista { get; set; } = false;

        public string? GenericWebhookUrl { get; set; }
        public string? GenericWebhookToken { get; set; }
    }
}