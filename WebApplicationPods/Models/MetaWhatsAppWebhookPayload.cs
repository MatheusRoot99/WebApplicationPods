using System.Text.Json.Serialization;

namespace WebApplicationPods.Models
{
    public class MetaWhatsAppWebhookPayload
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("entry")]
        public List<MetaWhatsAppWebhookEntry>? Entry { get; set; }
    }

    public class MetaWhatsAppWebhookEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("changes")]
        public List<MetaWhatsAppWebhookChange>? Changes { get; set; }
    }

    public class MetaWhatsAppWebhookChange
    {
        [JsonPropertyName("field")]
        public string? Field { get; set; }

        [JsonPropertyName("value")]
        public MetaWhatsAppWebhookValue? Value { get; set; }
    }

    public class MetaWhatsAppWebhookValue
    {
        [JsonPropertyName("messaging_product")]
        public string? MessagingProduct { get; set; }

        [JsonPropertyName("metadata")]
        public MetaWhatsAppWebhookMetadata? Metadata { get; set; }

        [JsonPropertyName("contacts")]
        public List<MetaWhatsAppWebhookContact>? Contacts { get; set; }

        [JsonPropertyName("messages")]
        public List<MetaWhatsAppWebhookMessage>? Messages { get; set; }

        [JsonPropertyName("statuses")]
        public List<MetaWhatsAppWebhookStatus>? Statuses { get; set; }

        [JsonPropertyName("errors")]
        public List<MetaWhatsAppWebhookError>? Errors { get; set; }
    }

    public class MetaWhatsAppWebhookMetadata
    {
        [JsonPropertyName("display_phone_number")]
        public string? DisplayPhoneNumber { get; set; }

        [JsonPropertyName("phone_number_id")]
        public string? PhoneNumberId { get; set; }
    }

    public class MetaWhatsAppWebhookContact
    {
        [JsonPropertyName("profile")]
        public MetaWhatsAppWebhookProfile? Profile { get; set; }

        [JsonPropertyName("wa_id")]
        public string? WaId { get; set; }
    }

    public class MetaWhatsAppWebhookProfile
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class MetaWhatsAppWebhookMessage
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public MetaWhatsAppWebhookText? Text { get; set; }

        [JsonPropertyName("button")]
        public MetaWhatsAppWebhookButton? Button { get; set; }

        [JsonPropertyName("interactive")]
        public MetaWhatsAppWebhookInteractive? Interactive { get; set; }

        [JsonPropertyName("errors")]
        public List<MetaWhatsAppWebhookError>? Errors { get; set; }
    }

    public class MetaWhatsAppWebhookText
    {
        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }

    public class MetaWhatsAppWebhookButton
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("payload")]
        public string? Payload { get; set; }
    }

    public class MetaWhatsAppWebhookInteractive
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class MetaWhatsAppWebhookStatus
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("recipient_id")]
        public string? RecipientId { get; set; }

        [JsonPropertyName("conversation")]
        public MetaWhatsAppWebhookConversation? Conversation { get; set; }

        [JsonPropertyName("pricing")]
        public MetaWhatsAppWebhookPricing? Pricing { get; set; }

        [JsonPropertyName("errors")]
        public List<MetaWhatsAppWebhookError>? Errors { get; set; }
    }

    public class MetaWhatsAppWebhookConversation
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("origin")]
        public MetaWhatsAppWebhookConversationOrigin? Origin { get; set; }
    }

    public class MetaWhatsAppWebhookConversationOrigin
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class MetaWhatsAppWebhookPricing
    {
        [JsonPropertyName("billable")]
        public bool? Billable { get; set; }

        [JsonPropertyName("pricing_model")]
        public string? PricingModel { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }

    public class MetaWhatsAppWebhookError
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error_data")]
        public MetaWhatsAppWebhookErrorData? ErrorData { get; set; }
    }

    public class MetaWhatsAppWebhookErrorData
    {
        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }
}