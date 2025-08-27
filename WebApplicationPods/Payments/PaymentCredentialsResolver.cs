using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplicationPods.Data;
using WebApplicationPods.Models;               // ApplicationUser, MerchantPaymentConfig
using WebApplicationPods.Payments.Options;    // PaymentsOptions

namespace WebApplicationPods.Payments
{
    public class PaymentCredentialsResolver : IPaymentCredentialsResolver
    {
        private readonly BancoContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<PaymentsOptions> _defaults;

        public PaymentCredentialsResolver(
            BancoContext db,
            UserManager<ApplicationUser> userManager,
            IOptions<PaymentsOptions> defaults)
        {
            _db = db;
            _userManager = userManager;
            _defaults = defaults;
        }

        /// <summary>
        /// 1) credenciais do usuário;
        /// 2) se anônimo/não achou, usa a PRIMEIRA config do provider (loja);
        /// 3) fallback no appsettings (PaymentsOptions).
        /// </summary>
        public async Task<T> GetAsync<T>(ClaimsPrincipal user, string provider) where T : class, new()
        {
            // --- 1) do usuário atual (se houver) ---
            T? typed = await TryGetFromDbAsync<T>(user, provider);
            if (typed is not null) return typed;

            // --- 2) default da loja (primeira linha do provider) ---
            var anyRow = await _db.MerchantPaymentConfigs
                .AsNoTracking()
                .Where(c => c.Provider == provider)
                .OrderBy(c => c.UserId)
                .FirstOrDefaultAsync();

            if (anyRow is not null && !string.IsNullOrWhiteSpace(anyRow.ConfigJson))
            {
                try
                {
                    typed = JsonSerializer.Deserialize<T>(anyRow.ConfigJson);
                    if (typed is not null) return typed;
                }
                catch { /* fallback */ }
            }

            // --- 3) fallback: appsettings.json ---
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
                        // Se houver criptografia, desencripte aqui antes de deserializar.
                        return JsonSerializer.Deserialize<T>(cfg.ConfigJson);
                    }
                    catch { /* fallback */ }
                }
            }
            return null;
        }
    }
}
