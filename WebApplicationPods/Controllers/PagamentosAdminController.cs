using MercadoPago.Resource.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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
        var cfg = await _db.MerchantPaymentConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == uid && x.Provider == provider);

        var vm = new PaymentConfigEditViewModel { Provider = provider };

        if (cfg != null)
        {
            if (provider == "Stripe")
            {
                var o = JsonSerializer.Deserialize<StripeOptions>(cfg.ConfigJson) ?? new();
                vm.StripePublishableKey = o.PublishableKey;
                vm.StripeSecretKey = "";                 // nunca reexiba
                vm.StripeWebhookSecret = o.WebhookSecret;
            }
            else if (provider == "MercadoPago")
            {
                var o = JsonSerializer.Deserialize<MercadoPagoOptions>(cfg.ConfigJson) ?? new();
                vm.MpPublicKey = o.PublicKey;
                vm.MpAccessToken = "";
                vm.MpWebhookSecret = o.WebhookSecret;
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

        if (vm.Provider == "Stripe")
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new StripeOptions()
                : JsonSerializer.Deserialize<StripeOptions>(entity.ConfigJson) ?? new StripeOptions();

            current.PublishableKey = vm.StripePublishableKey?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(vm.StripeSecretKey))
                current.SecretKey = vm.StripeSecretKey!.Trim();
            current.WebhookSecret = vm.StripeWebhookSecret?.Trim() ?? "";

            if (entity == null)
                _db.MerchantPaymentConfigs.Add(entity = new MerchantPaymentConfig { UserId = uid, Provider = "Stripe" });

            entity.ConfigJson = JsonSerializer.Serialize(current);
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else if (vm.Provider == "MercadoPago")
        {
            var current = entity == null || string.IsNullOrWhiteSpace(entity.ConfigJson)
                ? new MercadoPagoOptions()
                : JsonSerializer.Deserialize<MercadoPagoOptions>(entity.ConfigJson) ?? new MercadoPagoOptions();

            current.PublicKey = vm.MpPublicKey?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(vm.MpAccessToken))
                current.AccessToken = vm.MpAccessToken!.Trim();
            current.WebhookSecret = vm.MpWebhookSecret?.Trim() ?? "";

            if (entity == null)
                _db.MerchantPaymentConfigs.Add(entity = new MerchantPaymentConfig { UserId = uid, Provider = "MercadoPago" });

            entity.ConfigJson = JsonSerializer.Serialize(current);
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            TempData["Erro"] = $"Provedor não suportado: {vm.Provider}";
            return View(vm);
        }

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Configurações salvas!";
        return RedirectToAction(nameof(Edit), new { provider = vm.Provider });
    }
}
