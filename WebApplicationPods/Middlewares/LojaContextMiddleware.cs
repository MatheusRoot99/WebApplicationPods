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
            BancoContext db)
        {
            // 1) Primeiro tenta resolver pelo subdomínio (fluxo público)
            var lojaIdFromHost = await tenantResolver.ResolveLojaIdAsync(context);

            if (lojaIdFromHost.HasValue)
            {
                currentLoja.SetLojaId(lojaIdFromHost.Value);
                await _next(context);
                return;
            }

            // 2) Sem subdomínio: se estiver autenticado, aplica regra por role
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                // Admin pode navegar no "www" e escolher loja pela sessão (mais tarde faremos UI)
                if (context.User.IsInRole("Admin"))
                {
                    var lojaId = currentLoja.LojaId;
                    if (lojaId.HasValue)
                    {
                        var loja = await db.Lojas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lojaId.Value);
                        if (loja is null || !loja.Ativa)
                            currentLoja.ClearLoja();
                    }

                    await _next(context);
                    return;
                }

                // Lojista: força LojaId pelo user.LoJaId (painel)
                if (context.User.IsInRole("Lojista"))
                {
                    var user = await userManager.GetUserAsync(context.User);

                    if (user?.LojaId is int lojaId)
                    {
                        var loja = await db.Lojas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lojaId);
                        if (loja is null || !loja.Ativa)
                        {
                            context.Response.Redirect("/Conta/AcessoNegado");
                            return;
                        }

                        currentLoja.SetLojaId(lojaId);
                        await _next(context);
                        return;
                    }

                    context.Response.Redirect("/Conta/AcessoNegado");
                    return;
                }

                // Usuário final logado sem subdomínio: não tem loja definida
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
