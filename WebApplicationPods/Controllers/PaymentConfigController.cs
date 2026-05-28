using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Services.Interface;

[Authorize(Roles = "Admin,Lojista")]
public class PaymentConfigController : Controller
{
    private readonly BancoContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentLojaService _currentLoja;

    private const string ViewPath = "~/Views/PaymentConfig/Edit.cshtml";

    public PaymentConfigController(
        BancoContext db,
        UserManager<ApplicationUser> userManager,
        ICurrentLojaService currentLoja)
    {
        _db = db;
        _userManager = userManager;
        _currentLoja = currentLoja;
    }

    private static string NormalizeProvider(string? provider)
    {
        if (string.Equals(provider, "MercadoPago", StringComparison.OrdinalIgnoreCase))
            return "MercadoPago";

        if (string.Equals(provider, "PixManual", StringComparison.OrdinalIgnoreCase))
            return "PixManual";

        return "Stripe";
    }

    private async Task<int?> ResolverUsuarioDonoDasCredenciaisAsync(ApplicationUser user)
    {
        if (!User.IsInRole("Admin"))
            return user.Id;

        if (_currentLoja?.LojaId is not int lojaId || lojaId <= 0)
            return user.Id;

        var loja = await _db.Lojas
            .AsNoTracking()
            .Include(x => x.Config)
            .FirstOrDefaultAsync(x => x.Id == lojaId && x.Ativa);

        if (loja == null)
            return user.Id;

        return loja.DonoUserId
            ?? loja.Config?.LojistaUserId
            ?? user.Id;
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string provider = "Stripe")
    {
        provider = NormalizeProvider(provider);

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        var ownerUserId = await ResolverUsuarioDonoDasCredenciaisAsync(user);

        if (!ownerUserId.HasValue || ownerUserId.Value <= 0)
        {
            TempData["Erro"] = "Não foi possível identificar o lojista responsável pelas credenciais.";
            return RedirectToAction("Index", "Home");
        }

        var entity = await _db.MerchantPaymentConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == ownerUserId.Value && x.Provider == provider);

        var vm = new PaymentConfigEditViewModel
        {
            Provider = provider
        };

        if (entity != null)
        {
            try
            {
                if (provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
                {
                    var o = JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new StripeOptions();

                    vm.StripePublishableKey = o.PublishableKey;
                    vm.StripeWebhookSecret = o.WebhookSecret;
                    vm.StripeSecretKey = string.Empty;
                }
                else if (provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
                {
                    var o = JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new MercadoPagoOptions();

                    vm.MpPublicKey = o.PublicKey;
                    vm.MpWebhookSecret = o.WebhookSecret;
                    vm.MpAccessToken = string.Empty;
                }
                else if (provider.Equals("PixManual", StringComparison.OrdinalIgnoreCase))
                {
                    var o = JsonSerializer.Deserialize<PixManualOptions>(entity.ConfigJson) ?? new PixManualOptions();

                    vm.PixManualKey = o.PixKey;
                    vm.PixManualBeneficiaryName = o.BeneficiaryName;
                    vm.PixManualCity = o.BeneficiaryCity;
                    vm.PixManualTxIdPrefix = o.TxIdPrefix;
                    vm.PixManualMerchantName = o.MerchantName;
                }
            }
            catch
            {
                TempData["Erro"] = "Não foi possível carregar as credenciais salvas. Verifique a configuração.";
            }
        }

        return View(ViewPath, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentConfigEditViewModel model)
    {
        model.Provider = NormalizeProvider(model.Provider);

        if (!ModelState.IsValid)
            return View(ViewPath, model);

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        var ownerUserId = await ResolverUsuarioDonoDasCredenciaisAsync(user);

        if (!ownerUserId.HasValue || ownerUserId.Value <= 0)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível identificar o lojista responsável pelas credenciais.");
            return View(ViewPath, model);
        }

        var entity = await _db.MerchantPaymentConfigs
            .SingleOrDefaultAsync(x => x.UserId == ownerUserId.Value && x.Provider == model.Provider);

        string json;

        if (model.Provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new StripeOptions()
                : JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new StripeOptions();

            current.PublishableKey = model.StripePublishableKey?.Trim() ?? current.PublishableKey ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(model.StripeSecretKey))
                current.SecretKey = model.StripeSecretKey.Trim();

            current.WebhookSecret = model.StripeWebhookSecret?.Trim() ?? current.WebhookSecret ?? string.Empty;

            json = JsonSerializer.Serialize(current);
        }
        else if (model.Provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new MercadoPagoOptions()
                : JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new MercadoPagoOptions();

            current.PublicKey = model.MpPublicKey?.Trim() ?? current.PublicKey ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(model.MpAccessToken))
                current.AccessToken = model.MpAccessToken.Trim();

            current.WebhookSecret = model.MpWebhookSecret?.Trim() ?? current.WebhookSecret ?? string.Empty;

            json = JsonSerializer.Serialize(current);
        }
        else if (model.Provider.Equals("PixManual", StringComparison.OrdinalIgnoreCase))
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new PixManualOptions()
                : JsonSerializer.Deserialize<PixManualOptions>(entity.ConfigJson) ?? new PixManualOptions();

            current.PixKey = model.PixManualKey?.Trim() ?? string.Empty;
            current.BeneficiaryName = model.PixManualBeneficiaryName?.Trim() ?? string.Empty;
            current.BeneficiaryCity = model.PixManualCity?.Trim() ?? "BRASILIA";
            current.TxIdPrefix = string.IsNullOrWhiteSpace(model.PixManualTxIdPrefix)
                ? null
                : model.PixManualTxIdPrefix.Trim();
            current.MerchantName = string.IsNullOrWhiteSpace(model.PixManualMerchantName)
                ? null
                : model.PixManualMerchantName.Trim();

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
                UserId = ownerUserId.Value,
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