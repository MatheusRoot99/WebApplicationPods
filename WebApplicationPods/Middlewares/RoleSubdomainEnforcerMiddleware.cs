using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WebApplicationPods.Middlewares
{
    public class RoleSubdomainEnforcerMiddleware
    {
        private readonly RequestDelegate _next;

        public RoleSubdomainEnforcerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = (context.Request.Path.Value ?? "").ToLowerInvariant();

            // ✅ BYPASS: estáticos e rotas técnicas/autenticação
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
                await _next(context);
                return;
            }

            var user = context.User;
            var host = (context.Request.Host.Host ?? "").Trim().ToLowerInvariant();
            var port = context.Request.Host.Port;
            var scheme = context.Request.Scheme;

            bool isAdminHost = host.StartsWith("admin.");
            bool isPainelHost = host.StartsWith("painel.");

            bool isAuth = user?.Identity?.IsAuthenticated == true;
            bool isAdmin = isAuth && user.IsInRole("Admin");
            bool isLojista = isAuth && user.IsInRole("Lojista");

            // Se não está autenticado, deixa passar
            if (!isAuth)
            {
                await _next(context);
                return;
            }

            // Se não está em admin/painel (ex: loja-*.dominio), deixa passar
            if (!isAdminHost && !isPainelHost)
            {
                await _next(context);
                return;
            }

            var baseDomain = GetBaseDomain(host);

            string MakeUrl(string sub, string targetPath)
                => port.HasValue
                    ? $"{scheme}://{sub}.{baseDomain}:{port.Value}{targetPath}"
                    : $"{scheme}://{sub}.{baseDomain}{targetPath}";

            // ✅ ADMIN logado em painel.* -> manda para admin.*
            if (isAdmin && isPainelHost)
            {
                context.Response.Redirect(MakeUrl("admin", "/Admin/Dashboard"));
                return;
            }

            // ✅ LOJISTA logado em admin.* -> manda para painel.*
            if (isLojista && isAdminHost)
            {
                context.Response.Redirect(MakeUrl("painel", "/PainelLojista/Dashboard"));
                return;
            }

            // ✅ Autenticado, mas SEM role: desloga e manda pro login do portal atual
            if (!isAdmin && !isLojista && (isAdminHost || isPainelHost))
            {
                context.Response.Cookies.Delete("Pods.Auth");
                context.Response.Cookies.Delete("Pods.AntiForgery");
                context.Response.Cookies.Delete("SitePods.Session");

                if (baseDomain == "lvh.me")
                {
                    context.Response.Cookies.Delete("Pods.Auth", new CookieOptions { Domain = ".lvh.me", Path = "/" });
                    context.Response.Cookies.Delete("Pods.AntiForgery", new CookieOptions { Domain = ".lvh.me", Path = "/" });
                    context.Response.Cookies.Delete("SitePods.Session", new CookieOptions { Domain = ".lvh.me", Path = "/" });
                }

                context.Response.Redirect("/Conta/Login");
                return;
            }

            await _next(context);
        }

        private static string GetBaseDomain(string host)
        {
            if (host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0")
                return "lvh.me";

            if (host == "lvh.me" || host.EndsWith(".lvh.me"))
                return "lvh.me";

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return host;

            var last = parts[^1];
            var secondLast = parts[^2];
            var thirdLast = parts.Length >= 3 ? parts[^3] : null;

            var brSecondLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "com","net","org","gov","edu"
            };

            if (last.Equals("br", StringComparison.OrdinalIgnoreCase) &&
                thirdLast != null &&
                brSecondLevel.Contains(secondLast))
            {
                return $"{thirdLast}.{secondLast}.{last}";
            }

            return $"{secondLast}.{last}";
        }
    }
}
