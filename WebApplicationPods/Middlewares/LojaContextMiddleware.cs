using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;
using WebApplicationPods.Services.service;

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
            UserManager<ApplicationUser> userManager,
            BancoContext db,
            ICurrentLojaService currentLoja)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                // Se não for Admin: garante LojaId na session
                if (!context.User.IsInRole("Admin"))
                {
                    var user = await userManager.GetUserAsync(context.User);

                    if (user?.LojaId is int lojaId)
                    {
                        context.Session.SetInt32(CurrentLojaService.SessionKey, lojaId);

                        // valida se loja existe e ativa
                        var loja = await db.Lojas
                            .FirstOrDefaultAsync(x => x.Id == lojaId);

                        if (loja is null || !loja.Ativa)
                        {
                            context.Response.Redirect("/Conta/AcessoNegado");
                            return;
                        }
                    }
                    else
                    {
                        context.Response.Redirect("/Conta/AcessoNegado");
                        return;
                    }
                }
                else
                {
                    // Admin: se escolheu loja, valida se ativa
                    var lojaId = context.Session.GetInt32(CurrentLojaService.SessionKey);
                    if (lojaId.HasValue)
                    {
                        var loja = await db.Lojas
                            .FirstOrDefaultAsync(x => x.Id == lojaId.Value);

                        if (loja is null || !loja.Ativa)
                        {
                            currentLoja.ClearLoja();
                        }
                    }
                }
            }

            await _next(context);
        }
    }
}
