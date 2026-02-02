using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplicationPods.Middlewares
{
    public class PortalEntryRedirectMiddleware
    {
        private readonly RequestDelegate _next;

        public PortalEntryRedirectMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext ctx)
        {
            var path = (ctx.Request.Path.Value ?? "").ToLowerInvariant();
            var host = (ctx.Request.Host.Host ?? "").ToLowerInvariant();

            // ✅ REDIRECIONA localhost para admin.lvh.me
            if (host == "localhost" || host == "127.0.0.1")
            {
                var port = ctx.Request.Host.Port;
                var scheme = ctx.Request.Scheme;
                var returnUrl = ctx.Request.Query["ReturnUrl"].ToString();
                var newHost = "admin.lvh.me";

                // Constrói a nova URL mantendo path e query string
                var newPath = string.IsNullOrEmpty(path) || path == "/"
                    ? "/Conta/Login"
                    : path;

                var queryString = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : "";

                var newUrl = port.HasValue
                    ? $"{scheme}://{newHost}:{port.Value}{newPath}{queryString}"
                    : $"{scheme}://{newHost}{newPath}{queryString}";

                ctx.Response.Redirect(newUrl, permanent: false);
                return;
            }

            // Só trata entrada "raiz" para outros hosts
            var isRoot = path == "/" || path == "";
            if (!isRoot)
            {
                await _next(ctx);
                return;
            }

            // loja-*.lvh.me => abre site da loja (catálogo)
            if (host.StartsWith("loja-") || host.StartsWith("loja."))
            {
                ctx.Response.Redirect("/Home/Index");
                return;
            }

            // admin.* e painel.* => sempre vai pro Login (sem Landing)
            if (host.StartsWith("admin.") || host.StartsWith("painel."))
            {
                // Se já estiver autenticado, manda pro portal correto
                if (ctx.User?.Identity?.IsAuthenticated == true)
                {
                    if (host.StartsWith("admin."))
                        ctx.Response.Redirect("/Admin/Dashboard");
                    else
                        ctx.Response.Redirect("/PainelLojista/Dashboard");

                    return;
                }

                ctx.Response.Redirect("/Conta/Login");
                return;
            }

            // Qualquer outro host (lvh.me, www etc) => Login direto
            ctx.Response.Redirect("/Conta/Login");
        }
    }
}