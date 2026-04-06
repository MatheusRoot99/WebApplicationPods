using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebApplicationPods.Models;
using WebApplicationPods.Options;

namespace WebApplicationPods.Controllers
{
    [ApiController]
    public class WhatsAppMetaWebhookController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly WhatsAppOptions _options;
        private readonly ILogger<WhatsAppMetaWebhookController> _logger;

        public WhatsAppMetaWebhookController(
            IOptions<WhatsAppOptions> options,
            ILogger<WhatsAppMetaWebhookController> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        [HttpGet("/webhooks/whatsapp/meta")]
        [IgnoreAntiforgeryToken]
        public IActionResult Verify(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Modo de verificação inválido.");

            if (string.IsNullOrWhiteSpace(_options.MetaWebhookVerifyToken))
            {
                _logger.LogWarning("Webhook Meta recebido sem MetaWebhookVerifyToken configurado.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (!string.Equals(verifyToken, _options.MetaWebhookVerifyToken, StringComparison.Ordinal))
            {
                _logger.LogWarning("Falha na validação do webhook Meta. Token informado não confere.");
                return Unauthorized();
            }

            _logger.LogInformation("Webhook Meta validado com sucesso.");
            return Content(challenge ?? string.Empty, "text/plain");
        }

        [HttpPost("/webhooks/whatsapp/meta")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Receive()
        {
            string rawBody;

            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                _logger.LogWarning("Webhook Meta recebido com body vazio.");
                return Ok();
            }

            if (!ValidarAssinatura(rawBody))
                return Unauthorized();

            MetaWhatsAppWebhookPayload? payload;

            try
            {
                payload = JsonSerializer.Deserialize<MetaWhatsAppWebhookPayload>(rawBody, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao desserializar payload do webhook Meta.");
                return Ok();
            }

            if (payload == null)
            {
                _logger.LogWarning("Webhook Meta recebido com payload nulo após desserialização.");
                return Ok();
            }

            if (!string.Equals(payload.Object, "whatsapp_business_account", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Webhook Meta ignorado. Object={Object}", payload.Object);
                return Ok();
            }

            foreach (var entry in payload.Entry ?? Enumerable.Empty<MetaWhatsAppWebhookEntry>())
            {
                foreach (var change in entry.Changes ?? Enumerable.Empty<MetaWhatsAppWebhookChange>())
                {
                    var value = change.Value;
                    if (value == null)
                        continue;

                    foreach (var status in value.Statuses ?? Enumerable.Empty<MetaWhatsAppWebhookStatus>())
                    {
                        var erros = status.Errors?.Select(x =>
                                $"{x.Code}: {x.Title ?? x.Message ?? x.ErrorData?.Details}")
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToArray();

                        _logger.LogInformation(
                            "Meta webhook status. Field={Field}, PhoneNumberId={PhoneNumberId}, Recipient={Recipient}, Status={Status}, MessageId={MessageId}, ConversationId={ConversationId}, Category={Category}, Errors={Errors}",
                            change.Field,
                            value.Metadata?.PhoneNumberId,
                            status.RecipientId,
                            status.Status,
                            status.Id,
                            status.Conversation?.Id,
                            status.Pricing?.Category,
                            erros is { Length: > 0 } ? string.Join(" | ", erros) : "-");
                    }

                    foreach (var message in value.Messages ?? Enumerable.Empty<MetaWhatsAppWebhookMessage>())
                    {
                        var contato = value.Contacts?
                            .FirstOrDefault(x => string.Equals(x.WaId, message.From, StringComparison.Ordinal));

                        var resumo = message.Type switch
                        {
                            "text" => message.Text?.Body,
                            "button" => message.Button?.Text ?? message.Button?.Payload,
                            "interactive" => message.Interactive?.Type,
                            _ => null
                        };

                        _logger.LogInformation(
                            "Meta webhook mensagem recebida. From={From}, Name={Name}, Type={Type}, MessageId={MessageId}, Resumo={Resumo}",
                            message.From,
                            contato?.Profile?.Name,
                            message.Type,
                            message.Id,
                            resumo ?? "-");
                    }

                    foreach (var error in value.Errors ?? Enumerable.Empty<MetaWhatsAppWebhookError>())
                    {
                        _logger.LogWarning(
                            "Meta webhook error. Code={Code}, Title={Title}, Details={Details}",
                            error.Code,
                            error.Title ?? error.Message,
                            error.ErrorData?.Details);
                    }
                }
            }

            return Ok();
        }

        private bool ValidarAssinatura(string rawBody)
        {
            var appSecret = _options.MetaAppSecret?.Trim();

            if (string.IsNullOrWhiteSpace(appSecret))
            {
                _logger.LogWarning("Webhook Meta recebido sem MetaAppSecret configurado. Assinatura não será validada.");
                return true;
            }

            if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeaderValues))
            {
                _logger.LogWarning("Webhook Meta recebido sem header X-Hub-Signature-256.");
                return false;
            }

            var signatureHeader = signatureHeaderValues.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(signatureHeader) ||
                !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Header X-Hub-Signature-256 inválido.");
                return false;
            }

            var receivedHex = signatureHeader["sha256=".Length..].Trim();

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

            var valid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(receivedHex.ToLowerInvariant()));

            if (!valid)
                _logger.LogWarning("Assinatura do webhook Meta inválida.");

            return valid;
        }
    }
}