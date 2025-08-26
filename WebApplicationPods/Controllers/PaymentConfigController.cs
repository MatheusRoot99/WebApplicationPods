using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;

[Authorize]
public class PaymentConfigController : Controller
{
    private readonly BancoContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private const string ViewPath = "~/Views/PaymentConfig/Edit.cshtml";

    public PaymentConfigController(BancoContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        var cfg = _db.MerchantPaymentConfigs.FirstOrDefault(x => x.UserId == user.Id);

        var vm = new PaymentConfigEditViewModel();

        if (cfg != null)
        {
            vm.Provider = cfg.Provider;

            // desserializa o JSON salvo
            var doc = JsonDocument.Parse(cfg.ConfigJson);
            var root = doc.RootElement;

            if (cfg.Provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
            {
                vm.StripePublishableKey = root.GetPropertyOrDefault("PublishableKey");
                // por segurança não mostramos SecretKey, deixamos vazio
            }
            else if (cfg.Provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
            {
                vm.MpPublicKey = root.GetPropertyOrDefault("PublicKey");
                // por segurança não mostramos AccessToken, deixamos vazio
            }
        }
        else
        {
            vm.Provider = "Stripe"; // default de exibição
        }

        return View(ViewPath, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentConfigEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View(ViewPath, model);

        var user = await _userManager.GetUserAsync(User);
        var cfg = _db.MerchantPaymentConfigs.FirstOrDefault(x => x.UserId == user.Id);

        // carrega o JSON atual (se houver) para preservar segredos quando campo vier vazio
        string publishableKey = null, secretKey = null, webhookSecret = null;
        string publicKey = null, accessToken = null, mpWebhookSecret = null;

        if (cfg != null)
        {
            using var doc = JsonDocument.Parse(cfg.ConfigJson);
            var root = doc.RootElement;

            publishableKey = root.GetPropertyOrDefault("PublishableKey");
            secretKey = root.GetPropertyOrDefault("SecretKey");
            webhookSecret = root.GetPropertyOrDefault("WebhookSecret");

            publicKey = root.GetPropertyOrDefault("PublicKey");
            accessToken = root.GetPropertyOrDefault("AccessToken");
            mpWebhookSecret = root.GetPropertyOrDefault("WebhookSecret");
        }

        // atualiza conforme o provider selecionado no form
        if (model.Provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        {
            publishableKey = model.StripePublishableKey ?? publishableKey;
            // só troca o secret se o usuário digitou algo
            if (!string.IsNullOrWhiteSpace(model.StripeSecretKey))
                secretKey = model.StripeSecretKey;
            webhookSecret = model.StripeWebhookSecret ?? webhookSecret;

            var json = JsonSerializer.Serialize(new
            {
                Provider = "Stripe",
                PublishableKey = publishableKey,
                SecretKey = secretKey,
                WebhookSecret = webhookSecret
            });

            if (cfg == null)
                _db.MerchantPaymentConfigs.Add(new MerchantPaymentConfig { UserId = user.Id, Provider = "Stripe", ConfigJson = json });
            else
            {
                cfg.Provider = "Stripe";
                cfg.ConfigJson = json;
                _db.MerchantPaymentConfigs.Update(cfg);
            }
        }
        else if (model.Provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
        {
            publicKey = model.MpPublicKey ?? publicKey;
            if (!string.IsNullOrWhiteSpace(model.MpAccessToken))
                accessToken = model.MpAccessToken;
            mpWebhookSecret = model.MpWebhookSecret ?? mpWebhookSecret;

            var json = JsonSerializer.Serialize(new
            {
                Provider = "MercadoPago",
                PublicKey = publicKey,
                AccessToken = accessToken,
                WebhookSecret = mpWebhookSecret
            });

            if (cfg == null)
                _db.MerchantPaymentConfigs.Add(new MerchantPaymentConfig { UserId = user.Id, Provider = "MercadoPago", ConfigJson = json });
            else
            {
                cfg.Provider = "MercadoPago";
                cfg.ConfigJson = json;
                _db.MerchantPaymentConfigs.Update(cfg);
            }
        }
        else
        {
            ModelState.AddModelError(nameof(model.Provider), "Provedor inválido.");
            return View(model);
        }

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Configurações salvas com sucesso.";
        return RedirectToAction(nameof(Edit));
    }
}

static class JsonExt
{
    public static string? GetPropertyOrDefault(this JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
