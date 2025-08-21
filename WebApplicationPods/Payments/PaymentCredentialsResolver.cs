using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using WebApplicationPods.Data;
using WebApplicationPods.Models;                 // ApplicationUser, MerchantPaymentConfig
using WebApplicationPods.Payments.Options;

namespace WebApplicationPods.Payments
{
    public class PaymentCredentialsResolver : IPaymentCredentialsResolver
    {
        private readonly UserManager<ApplicationUser> _userManager;   // << ApplicationUser (Identity<int>)
        private readonly BancoContext _db;
        private readonly IOptionsSnapshot<PaymentsOptions> _defaults;

        public PaymentCredentialsResolver(
            UserManager<ApplicationUser> userManager,
            BancoContext db,
            IOptionsSnapshot<PaymentsOptions> defaults)
        {
            _userManager = userManager;
            _db = db;
            _defaults = defaults;
        }

        public async Task<T> GetAsync<T>(ClaimsPrincipal user, string provider) where T : class, new()
        {
            // GetUserId retorna string; convertemos para int
            var userIdStr = _userManager.GetUserId(user);
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var userId))
            {
                var cfg = await _db.MerchantPaymentConfigs
                    .AsNoTracking()
                    .SingleOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

                if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.ConfigJson))
                {
                    // Se houver criptografia, desencripte aqui antes de deserializar.
                    var typed = System.Text.Json.JsonSerializer.Deserialize<T>(cfg.ConfigJson);
                    if (typed is not null) return typed;
                }
            }

            // Fallback: defaults do appsettings.json (PaymentsOptions)
            return provider switch
            {
                "MercadoPago" => _defaults.Value.MercadoPago as T ?? new T(),
                "Stripe" => _defaults.Value.Stripe as T ?? new T(),
                _ => new T()
            };
        }
    }
}
