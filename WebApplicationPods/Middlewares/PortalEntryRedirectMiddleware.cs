using Microsoft.AspNetCore.Http;
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
                var newHost = "admin.lvh.me";

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

            // Só trata entrada "raiz"
            var isRoot = path == "/" || path == "";
            if (!isRoot)
            {
                await _next(ctx);
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
                    // Depois do RoleSubdomainEnforcer, o host já está correto
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
