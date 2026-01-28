using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class SubdomainTenantResolver : ITenantResolver
    {
        private readonly TenantDbContext _db;

        public SubdomainTenantResolver(TenantDbContext db)
        {
            _db = db;
        }

        public async Task<int?> ResolveLojaIdAsync(HttpContext context)
        {
            var host = context.Request.Host.Host?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(host))
                return null;

            // DEV: localhost/ip normalmente não tem subdomínio real
            if (host == "localhost" || host == "127.0.0.1" || host == "::1")
                return null;

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return null;

            var sub = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(sub))
                return null;

            // reserve subdomínios do sistema
            if (sub is "www" or "admin" or "painel" or "api")
                return null;

            return await _db.Lojas
                .AsNoTracking()
                .Where(l => l.Ativa && l.Subdominio == sub)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();
        }
    }
}
