using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Payments
{
    public class PaymentCredentialsResolver : IPaymentCredentialsResolver
    {
        private readonly BancoContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<PaymentsOptions> _defaults;
        private readonly ICurrentLojaService _currentLoja;

        public PaymentCredentialsResolver(
            BancoContext db,
            UserManager<ApplicationUser> userManager,
            IOptions<PaymentsOptions> defaults,
            ICurrentLojaService currentLoja)
        {
            _db = db;
            _userManager = userManager;
            _defaults = defaults;
            _currentLoja = currentLoja;
        }

        public async Task<T> GetAsync<T>(ClaimsPrincipal user, string provider) where T : class, new()
        {
            var typed = await TryGetFromDbAsync<T>(user, provider);
            if (typed is not null) return typed;

            typed = await TryGetFromCurrentStoreAsync<T>(provider);
            if (typed is not null) return typed;

            return provider switch
            {
                "MercadoPago" => _defaults.Value.MercadoPago as T ?? new T(),
                "Stripe" => _defaults.Value.Stripe as T ?? new T(),
                _ => new T()
            };
        }

        private async Task<T?> TryGetFromDbAsync<T>(ClaimsPrincipal user, string provider) where T : class
        {
            var userIdStr = _userManager.GetUserId(user);

            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var userId))
            {
                var cfg = await _db.MerchantPaymentConfigs
                    .AsNoTracking()
                    .SingleOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

                if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.ConfigJson))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<T>(cfg.ConfigJson);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private async Task<T?> TryGetFromCurrentStoreAsync<T>(string provider) where T : class
        {
            if (_currentLoja?.LojaId is not int lojaId || lojaId <= 0)
                return null;

            var loja = await _db.Lojas
                .AsNoTracking()
                .Include(x => x.Config)
                .FirstOrDefaultAsync(x => x.Id == lojaId && x.Ativa);

            if (loja == null)
                return null;

            var userIds = new[] { loja.DonoUserId, loja.Config?.LojistaUserId }
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            foreach (var userId in userIds)
            {
                var cfg = await _db.MerchantPaymentConfigs
                    .AsNoTracking()
                    .SingleOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

                if (cfg == null || string.IsNullOrWhiteSpace(cfg.ConfigJson))
                    continue;

                try
                {
                    var typed = JsonSerializer.Deserialize<T>(cfg.ConfigJson);
                    if (typed is not null)
                        return typed;
                }
                catch
                {
                }
            }

            return null;
        }
    }
}