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
            var host = (ctx.Request.Host.Host ?? "").Trim().ToLowerInvariant();

            if (Path.HasExtension(path) ||
                path.StartsWith("/css") ||
                path.StartsWith("/js") ||
                path.StartsWith("/lib") ||
                path.StartsWith("/images") ||
                path.StartsWith("/imagens") ||
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

            var isRoot = path == "/" || path == "";
            if (!isRoot)
            {
                await _next(ctx);
                return;
            }

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

            if (host.StartsWith("admin."))
            {
                if (ctx.User?.Identity?.IsAuthenticated == true)
                {
                    ctx.Response.Redirect("/Admin/Dashboard");
                    return;
                }

                ctx.Response.Redirect("/Conta/Login");
                return;
            }

            if (host.StartsWith("painel."))
            {
                if (ctx.User?.Identity?.IsAuthenticated == true)
                {
                    ctx.Response.Redirect("/PainelLojista/Dashboard");
                    return;
                }

                ctx.Response.Redirect("/Conta/Login");
                return;
            }

            if (EhSubdominioDeLoja(host))
            {
                ctx.Response.Redirect("/Home/Index");
                return;
            }

            await _next(ctx);
        }

        private static bool EhSubdominioDeLoja(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            if (host is "localhost" or "127.0.0.1" or "::1")
                return false;

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                return false;

            var sub = parts[0].Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(sub))
                return false;

            if (sub is "www" or "admin" or "painel" or "api")
                return false;

            return true;
        }
    }
}