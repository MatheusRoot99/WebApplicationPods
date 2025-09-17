using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public async Task<IActionResult> Edit(string provider = "Stripe")
    {
        var user = await _userManager.GetUserAsync(User);

        // carrega SOMENTE a config do provider selecionado
        var entity = await _db.MerchantPaymentConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == provider);

        var vm = new PaymentConfigEditViewModel { Provider = provider };

        if (entity != null)
        {
            try
            {
                if (provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
                {
                    var o = JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new();
                    vm.StripePublishableKey = o.PublishableKey;
                    vm.StripeWebhookSecret = o.WebhookSecret;
                    vm.StripeSecretKey = ""; // nunca reexiba
                }
                else if (provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
                {
                    var o = JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new();
                    vm.MpPublicKey = o.PublicKey;
                    vm.MpWebhookSecret = o.WebhookSecret;
                    vm.MpAccessToken = ""; // nunca reexiba
                }
                else if (provider.Equals("PixManual", StringComparison.OrdinalIgnoreCase))
                {
                    var o = JsonSerializer.Deserialize<PixManualOptions>(entity.ConfigJson) ?? new();
                    vm.PixManualKey = o.PixKey;
                    vm.PixManualBeneficiaryName = o.BeneficiaryName;
                    vm.PixManualCity = o.BeneficiaryCity;
                    vm.PixManualTxIdPrefix = o.TxIdPrefix;
                    vm.PixManualMerchantName = o.MerchantName;
                }
            }
            catch { /* tolera formatos antigos */ }
        }

        return View(ViewPath, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentConfigEditViewModel model)
    {
        if (!ModelState.IsValid) return View(ViewPath, model);

        var user = await _userManager.GetUserAsync(User);

        // upsert por (UserId, Provider)
        var entity = await _db.MerchantPaymentConfigs
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == model.Provider);

        string json;

        if (model.Provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new StripeOptions()
                : JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new StripeOptions();

            current.PublishableKey = model.StripePublishableKey?.Trim() ?? current.PublishableKey ?? "";
            if (!string.IsNullOrWhiteSpace(model.StripeSecretKey))
                current.SecretKey = model.StripeSecretKey!.Trim();
            current.WebhookSecret = model.StripeWebhookSecret?.Trim() ?? current.WebhookSecret ?? "";

            json = JsonSerializer.Serialize(current);
        }
        else if (model.Provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new MercadoPagoOptions()
                : JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new MercadoPagoOptions();

            current.PublicKey = model.MpPublicKey?.Trim() ?? current.PublicKey ?? "";
            if (!string.IsNullOrWhiteSpace(model.MpAccessToken))
                current.AccessToken = model.MpAccessToken!.Trim();
            current.WebhookSecret = model.MpWebhookSecret?.Trim() ?? current.WebhookSecret ?? "";

            json = JsonSerializer.Serialize(current);
        }
        else if (model.Provider.Equals("PixManual", StringComparison.OrdinalIgnoreCase))
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new PixManualOptions()
                : JsonSerializer.Deserialize<PixManualOptions>(entity.ConfigJson) ?? new PixManualOptions();

            current.PixKey = model.PixManualKey?.Trim() ?? "";
            current.BeneficiaryName = model.PixManualBeneficiaryName?.Trim() ?? "";
            current.BeneficiaryCity = model.PixManualCity?.Trim() ?? "BRASILIA";
            current.TxIdPrefix = string.IsNullOrWhiteSpace(model.PixManualTxIdPrefix) ? null : model.PixManualTxIdPrefix.Trim();
            current.MerchantName = string.IsNullOrWhiteSpace(model.PixManualMerchantName) ? null : model.PixManualMerchantName.Trim();

            json = JsonSerializer.Serialize(current);
        }
        else
        {
            ModelState.AddModelError(nameof(model.Provider), "Provedor inválido.");
            return View(ViewPath, model);
        }

        if (entity == null)
        {
            _db.MerchantPaymentConfigs.Add(new MerchantPaymentConfig
            {
                UserId = user.Id,
                Provider = model.Provider,
                ConfigJson = json,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            entity.ConfigJson = json;
            entity.UpdatedAt = DateTime.UtcNow;
            _db.MerchantPaymentConfigs.Update(entity);
        }

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Configurações salvas com sucesso.";
        return RedirectToAction(nameof(Edit), new { provider = model.Provider });
    }
}
