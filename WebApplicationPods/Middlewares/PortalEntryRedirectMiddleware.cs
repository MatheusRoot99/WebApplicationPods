using Microsoft.AspNetCore.Http;
using System.IO;
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

            // ✅ 1) BYPASS para estáticos e rotas técnicas
            // (isso impede redirecionar /css/*.css, /js/*.js, imagens, etc.)
            if (Path.HasExtension(path) ||
                path.StartsWith("/css") ||
                path.StartsWith("/js") ||
                path.StartsWith("/lib") ||
                path.StartsWith("/images") ||
                path.StartsWith("/uploads") ||
                path.StartsWith("/favicon") ||
                path.StartsWith("/hubs") ||
                path.StartsWith("/_blazor") ||
                path.StartsWith("/conta") ||
                path.StartsWith("/identity") ||
                path.Contains("login") ||
                path.Contains("logout") ||
                path.Contains("forgotpassword") ||
                path.Contains("resetpassword") ||
                path.Contains("error"))
            {
                await _next(ctx);
                return;
            }

            // ✅ 2) Só trata ENTRADA ("/")
            var isRoot = path == "/" || path == "";
            if (!isRoot)
            {
                await _next(ctx);
                return;
            }

            // ✅ 3) Redireciona localhost APENAS na raiz (/)
            // (não mexe em /css, /js, etc porque já bypassou acima)
            if (host == "localhost" || host == "127.0.0.1")
            {
                var port = ctx.Request.Host.Port;
                var scheme = ctx.Request.Scheme;
                var newHost = "admin.lvh.me";

                var newPath = "/Conta/Login";
                var queryString = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : "";

                var newUrl = port.HasValue
                    ? $"{scheme}://{newHost}:{port.Value}{newPath}{queryString}"
                    : $"{scheme}://{newHost}{newPath}{queryString}";

                ctx.Response.Redirect(newUrl, permanent: false);
                return;
            }

            // loja-*.lvh.me => catálogo
            if (host.StartsWith("loja-") || host.StartsWith("loja."))
            {
                ctx.Response.Redirect("/Home/Index");
                return;
            }

            // admin.* e painel.* => Login ou Dashboard
            if (host.StartsWith("admin.") || host.StartsWith("painel."))
            {
                if (ctx.User?.Identity?.IsAuthenticated == true)
                {
                    ctx.Response.Redirect(host.StartsWith("admin.")
                        ? "/Admin/Dashboard"
                        : "/PainelLojista/Dashboard");
                    return;
                }

                ctx.Response.Redirect("/Conta/Login");
                return;
            }

            // qualquer outro host => Login
            ctx.Response.Redirect("/Conta/Login");
        }
    }
}
