using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class SubdomainTenantResolver : ITenantResolver
    {
        private readonly IDbContextFactory<BancoContext> _factory;

        public SubdomainTenantResolver(IDbContextFactory<BancoContext> factory)
        {
            _factory = factory;
        }

        public async Task<int?> ResolveLojaIdAsync(HttpContext context)
        {
            var host = context.Request.Host.Host?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(host))
                return null;

            // DEV: localhost/ip normalmente não tem subdomínio real
            if (host == "localhost" || host == "127.0.0.1" || host == "::1")
                return null;

            // Ex: noemi.seusite.com => ["noemi","seusite","com"]
            // Ex: noemi.minhaempresa.com.br => ["noemi","minhaempresa","com","br"]
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return null;

            var sub = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(sub))
                return null;

            // reserve subdomínios do sistema
            if (sub is "www" or "admin" or "painel" or "api")
                return null;

            await using var db = await _factory.CreateDbContextAsync();

            return await db.Lojas
                .AsNoTracking()
                .Where(l => l.Ativa && l.Subdominio.ToLower() == sub)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();
        }
    }
}
