using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Stripe;
using Stripe.Checkout;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Options;
using PaymentMethod = WebApplicationPods.Enum.PaymentMethod;

public class StripeGateway : IPaymentGateway
{
    private readonly IPaymentCredentialsResolver _resolver;

    public string Provider => "Stripe";

    public StripeGateway(IPaymentCredentialsResolver resolver)
    {
        _resolver = resolver;
    }

    // Carrega credenciais do lojista (ou defaults do appsettings) e seta ApiKey
    private async Task<StripeOptions> GetCfgAsync(ClaimsPrincipal? user = null)
    {
        var cfg = await _resolver.GetAsync<StripeOptions>(user ?? new ClaimsPrincipal(), Provider)
                  ?? new StripeOptions();
        StripeConfiguration.ApiKey = cfg.SecretKey;
        return cfg;
    }

    private static long ToCents(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static PaymentStatus MapStatus(string s) =>
        s switch
        {
            "succeeded" => PaymentStatus.Paid,
            "processing" => PaymentStatus.Pending,
            "requires_action" => PaymentStatus.Pending,
            "requires_payment_method" => PaymentStatus.Pending,
            "canceled" => PaymentStatus.Canceled,
            _ => PaymentStatus.Pending
        };

    // ---------------- PIX ----------------
    // (assinatura nova com 'amount')
    public async Task<PixInitResult> CreatePixAsync(PedidoModel pedido, decimal amount)
    {
        await GetCfgAsync();
        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = ToCents(amount),            // usa o total calculado (itens+frete−descontos)
            Currency = "brl",
            PaymentMethodTypes = new List<string> { "pix" },
            Description = $"Pedido #{pedido.Id}"
        });

        return new PixInitResult
        {
            ProviderPaymentId = intent.Id,
            // No Stripe, o client_secret é usado no front para renderizar o QR
            QrData = intent.ClientSecret ?? string.Empty,
            QrBase64Png = null
        };
    }

    // --------------- Cartão ---------------
    // (assinatura nova com 'amount')
    public async Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method, decimal amount)
    {
        await GetCfgAsync();
        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = ToCents(amount),            // usa o total calculado
            Currency = "brl",
            Description = $"Pedido #{pedido.Id}",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
        });

        return new CardInitResult
        {
            ProviderPaymentId = intent.Id,
            ClientSecretOrToken = intent.ClientSecret
        };
    }

    public async Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson)
    {
        await GetCfgAsync();
        var intentService = new PaymentIntentService();

        // (opcional) aceitar paymentMethodId vindo do front no payload JSON
        string? paymentMethodId = null;
        if (!string.IsNullOrWhiteSpace(clientPayloadJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(clientPayloadJson);
                if (doc.RootElement.TryGetProperty("paymentMethodId", out var pm))
                    paymentMethodId = pm.GetString();
            }
            catch { /* ignore */ }
        }

        PaymentIntent intent;
        if (!string.IsNullOrWhiteSpace(paymentMethodId))
        {
            intent = await intentService.ConfirmAsync(
                providerPaymentId,
                new PaymentIntentConfirmOptions { PaymentMethod = paymentMethodId }
            );
        }
        else
        {
            intent = await intentService.ConfirmAsync(providerPaymentId);
        }

        // Buscar dados do cartão via ChargeService (Stripe.NET v40+)
        string? brand = null, last4 = null;
        try
        {
            var chargeService = new ChargeService();
            var charges = await chargeService.ListAsync(new ChargeListOptions
            {
                PaymentIntent = intent.Id,
                Limit = 1
            });

            if (charges.Data.Count > 0)
            {
                var charge = charges.Data[0];
                brand = charge.PaymentMethodDetails?.Card?.Brand;
                last4 = charge.PaymentMethodDetails?.Card?.Last4;
            }
        }
        catch { }

        return new ConfirmCardResult
        {
            Success = intent.Status == "succeeded",
            Brand = brand,
            Last4 = last4,
            FailureReason = intent.LastPaymentError?.Message
        };
    }

    public async Task<PaymentStatus> GetStatusAsync(string providerPaymentId)
    {
        await GetCfgAsync();
        var service = new PaymentIntentService();
        var intent = await service.GetAsync(providerPaymentId);
        return MapStatus(intent.Status);
    }

    public async Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)>
        HandleWebhookAsync(HttpRequest request)
    {
        var cfg = await GetCfgAsync();

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        var signature = request.Headers["Stripe-Signature"];
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, cfg.WebhookSecret);
        }
        catch
        {
            return ("", PaymentStatus.Pending, null, "invalid-signature");
        }

        if (stripeEvent.Data.Object is PaymentIntent pi)
        {
            var cents = pi.AmountReceived > 0 ? pi.AmountReceived : pi.Amount;
            decimal? paidAmount = cents > 0 ? cents / 100m : (decimal?)null;
            return (pi.Id, MapStatus(pi.Status), paidAmount, stripeEvent.Type ?? "");
        }

        return ("", PaymentStatus.Pending, null, stripeEvent.Type ?? "");
    }

    // Checkout hospedado (Stripe Checkout)
    public async Task<string> CreateCheckoutAsync(ClaimsPrincipal user, CheckoutRequest req)
    {
        var creds = await _resolver.GetAsync<StripeOptions>(user, Provider) ?? new StripeOptions();
        StripeConfiguration.ApiKey = creds.SecretKey;

        var service = new SessionService();
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = req.SuccessUrl,
            CancelUrl = req.CancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = (req.Currency ?? "BRL").ToLower(),
                        UnitAmount = ToCents(req.Amount), // usa centavos (long)
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrWhiteSpace(req.Description) ? "Pedido" : req.Description
                        }
                    },
                    Quantity = 1
                }
            }
        });

        return session.Url!;
    }
}
