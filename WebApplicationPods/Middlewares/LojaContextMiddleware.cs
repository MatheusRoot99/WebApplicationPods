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
            var path = (context.Request.Path.Value ?? "").ToLowerInvariant();

            // ✅ IMPORTANTE: nunca redirecionar em rotas de Conta (evita loop /Conta/AcessoNegado)
            // e nem em assets/SignalR
            if (path.StartsWith("/conta") ||
                path.StartsWith("/hubs") ||
                path.StartsWith("/_blazor") ||
                (HttpMethods.IsGet(context.Request.Method) && Path.HasExtension(context.Request.Path)))
            {
                await _next(context);
                return;
            }

            var host = (context.Request.Host.Host ?? "").ToLowerInvariant();
            var isAdminHost = host.StartsWith("admin.");
            var isPainelHost = host.StartsWith("painel.");

            // 1) Público: resolve por subdomínio loja-*
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

            // 2) Admin/Painel: se autenticado, resolve por role
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                if (context.User.IsInRole("Admin"))
                {
                    // Admin pode ficar sem loja. Se tiver LojaId setado, valida.
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

                            // ✅ aqui pode redirecionar, porque /Conta já foi liberado acima
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

                currentLoja.ClearLoja();
                await _next(context);
                return;
            }

            // 3) Visitante sem subdomínio => sem loja
            currentLoja.ClearLoja();
            await _next(context);
        }
    }
}
