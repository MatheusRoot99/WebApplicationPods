using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Middlewares
{
    public class LojaContextMiddleware
    {
        private readonly RequestDelegate _next;

        public LojaContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(
            HttpContext context,
            ITenantResolver tenantResolver,
            ICurrentLojaService currentLoja,
            UserManager<ApplicationUser> userManager,
            TenantDbContext tenantDb)
        {
            var host = (context.Request.Host.Host ?? "").ToLowerInvariant();
            var isAdminHost = host.StartsWith("admin.");
            var isPainelHost = host.StartsWith("painel.");

            // 1) Só resolve por subdomínio se NÃO for admin/painel (fluxo público loja-*)
            if (!isAdminHost && !isPainelHost)
            {
                var lojaIdFromHost = await tenantResolver.ResolveLojaIdAsync(context);

                if (lojaIdFromHost.HasValue)
                {
                    currentLoja.SetLojaId(lojaIdFromHost.Value);
                    await _next(context);
                    return;
                }
            }

            // 2) Sem subdomínio (ou em admin/painel): se estiver autenticado, aplica regra por role
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                // Admin: pode ficar sem loja (ver tudo) OU usar seletor via sessão (futuro)
                if (context.User.IsInRole("Admin"))
                {
                    // Se já tiver loja na sessão (seletor), valida se existe/ativa
                    var lojaId = currentLoja.LojaId;
                    if (lojaId.HasValue)
                    {
                        var loja = await tenantDb.Lojas
                            .AsNoTracking()
                            .FirstOrDefaultAsync(l => l.Id == lojaId.Value);

                        if (loja is null || !loja.Ativa)
                            currentLoja.ClearLoja();
                    }

                    await _next(context);
                    return;
                }

                // Lojista: força LojaId pelo user.LojaId
                if (context.User.IsInRole("Lojista"))
                {
                    var user = await userManager.GetUserAsync(context.User);

                    if (user?.LojaId is int lojaId && lojaId > 0)
                    {
                        var loja = await tenantDb.Lojas
                            .AsNoTracking()
                            .FirstOrDefaultAsync(l => l.Id == lojaId);

                        if (loja is null || !loja.Ativa)
                        {
                            currentLoja.ClearLoja();
                            context.Response.Redirect("/Conta/AcessoNegado");
                            return;
                        }

                        currentLoja.SetLojaId(lojaId);
                        await _next(context);
                        return;
                    }

                    currentLoja.ClearLoja();
                    context.Response.Redirect("/Conta/AcessoNegado");
                    return;
                }

                // Usuário final logado sem subdomínio => sem loja definida
                currentLoja.ClearLoja();
                await _next(context);
                return;
            }

            // 3) Visitante sem subdomínio => sem loja (landing)
            currentLoja.ClearLoja();
            await _next(context);
        }
    }
}
