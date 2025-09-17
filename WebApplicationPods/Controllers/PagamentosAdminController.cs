using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;

[Authorize(Roles = "Lojista")]
public class PagamentosAdminController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly BancoContext _db;

    public PagamentosAdminController(UserManager<ApplicationUser> users, BancoContext db)
    {
        _users = users; _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string provider = "Stripe")
    {
        var uid = int.Parse(_users.GetUserId(User)!);

        var entity = await _db.MerchantPaymentConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == uid && x.Provider == provider);

        var vm = new PaymentConfigEditViewModel { Provider = provider };

        if (entity != null)
        {
            if (provider == "Stripe")
            {
                var o = JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new();
                vm.StripePublishableKey = o.PublishableKey;
                vm.StripeWebhookSecret = o.WebhookSecret;
                vm.StripeSecretKey = ""; // não reexibir
            }
            else if (provider == "MercadoPago")
            {
                var o = JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new();
                vm.MpPublicKey = o.PublicKey;
                vm.MpWebhookSecret = o.WebhookSecret;
                vm.MpAccessToken = ""; // não reexibir
            }
            else if (provider == "PixManual")
            {
                var o = JsonSerializer.Deserialize<PixManualOptions>(entity.ConfigJson) ?? new();
                vm.PixManualKey = o.PixKey;
                vm.PixManualBeneficiaryName = o.BeneficiaryName;
                vm.PixManualCity = o.BeneficiaryCity;
                vm.PixManualTxIdPrefix = o.TxIdPrefix;
                vm.PixManualMerchantName = o.MerchantName;
            }
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentConfigEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var uid = int.Parse(_users.GetUserId(User)!);

        var entity = await _db.MerchantPaymentConfigs
            .SingleOrDefaultAsync(x => x.UserId == uid && x.Provider == vm.Provider);

        string json;

        if (vm.Provider == "Stripe")
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new StripeOptions()
                : JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new StripeOptions();

            current.PublishableKey = vm.StripePublishableKey?.Trim() ?? current.PublishableKey ?? "";
            if (!string.IsNullOrWhiteSpace(vm.StripeSecretKey))
                current.SecretKey = vm.StripeSecretKey!.Trim();
            current.WebhookSecret = vm.StripeWebhookSecret?.Trim() ?? current.WebhookSecret ?? "";

            json = JsonSerializer.Serialize(current);
        }
        else if (vm.Provider == "MercadoPago")
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new MercadoPagoOptions()
                : JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new MercadoPagoOptions();

            current.PublicKey = vm.MpPublicKey?.Trim() ?? current.PublicKey ?? "";
            if (!string.IsNullOrWhiteSpace(vm.MpAccessToken))
                current.AccessToken = vm.MpAccessToken!.Trim();
            current.WebhookSecret = vm.MpWebhookSecret?.Trim() ?? current.WebhookSecret ?? "";

            json = JsonSerializer.Serialize(current);
        }
        else if (vm.Provider == "PixManual")
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new PixManualOptions()
                : JsonSerializer.Deserialize<PixManualOptions>(entity.ConfigJson) ?? new PixManualOptions();

            current.PixKey = vm.PixManualKey?.Trim() ?? "";
            current.BeneficiaryName = vm.PixManualBeneficiaryName?.Trim() ?? "";
            current.BeneficiaryCity = vm.PixManualCity?.Trim() ?? "BRASILIA";
            current.TxIdPrefix = string.IsNullOrWhiteSpace(vm.PixManualTxIdPrefix) ? null : vm.PixManualTxIdPrefix.Trim();
            current.MerchantName = string.IsNullOrWhiteSpace(vm.PixManualMerchantName) ? null : vm.PixManualMerchantName.Trim();

            json = JsonSerializer.Serialize(current);
        }
        else
        {
            TempData["Erro"] = $"Provedor não suportado: {vm.Provider}";
            return View(vm);
        }

        if (entity == null)
            _db.MerchantPaymentConfigs.Add(entity = new MerchantPaymentConfig { UserId = uid, Provider = vm.Provider });

        entity.ConfigJson = json;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Configurações salvas!";
        return RedirectToAction(nameof(Edit), new { provider = vm.Provider });
    }
}
