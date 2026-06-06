using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class SubdomainTenantResolver : ITenantResolver
    {
        private readonly TenantDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public SubdomainTenantResolver(
            TenantDbContext db,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _db = db;
            _configuration = configuration;
            _env = env;
        }

        public async Task<int?> ResolveLojaIdAsync(HttpContext context)
        {
            var host = context.Request.Host.Host?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(host))
                return await ResolveDefaultLojaDevAsync();

            if (_env.IsDevelopment() && IsNgrokHost(host))
                return await ResolveDefaultLojaDevAsync();

            if (_env.IsDevelopment() && IsLocalHost(host))
                return await ResolveDefaultLojaDevAsync();

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                return await ResolveDefaultLojaDevAsync();

            var sub = parts[0].Trim();

            if (string.IsNullOrWhiteSpace(sub))
                return await ResolveDefaultLojaDevAsync();

            if (sub is "www" or "admin" or "painel" or "api")
                return null;

            var lojaId = await _db.Lojas
                .AsNoTracking()
                .Where(l => l.Ativa && l.Subdominio == sub)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();

            if (lojaId.HasValue)
                return lojaId;

            return await ResolveDefaultLojaDevAsync();
        }

        private async Task<int?> ResolveDefaultLojaDevAsync()
        {
            if (!_env.IsDevelopment())
                return null;

            var defaultLojaId =
                _configuration.GetValue<int?>("DevelopmentSettings:DefaultLojaId")
                ?? _configuration.GetValue<int?>("AppSettings:DefaultLojaId");

            if (!defaultLojaId.HasValue || defaultLojaId.Value <= 0)
                return null;

            return await _db.Lojas
                .AsNoTracking()
                .Where(l => l.Ativa && l.Id == defaultLojaId.Value)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();
        }

        private static bool IsNgrokHost(string host)
        {
            return host.EndsWith(".ngrok-free.dev", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".ngrok.io", StringComparison.OrdinalIgnoreCase)
                || host.Contains(".ngrok.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalHost(string host)
        {
            return host == "localhost"
                || host == "127.0.0.1"
                || host == "::1";
        }
    }
}