using System.Net;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Collections.Generic;
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
            var user = context.User;
            var host = context.Request.Host.Host?.Trim().ToLowerInvariant() ?? "";
            var port = context.Request.Host.Port;
            var scheme = context.Request.Scheme;

            bool isAdminHost = host.StartsWith("admin.");
            bool isPainelHost = host.StartsWith("painel.");

            bool isAuth = user?.Identity?.IsAuthenticated == true;
            bool isAdmin = isAuth && user.IsInRole("Admin");
            bool isLojista = isAuth && user.IsInRole("Lojista");

            // Se não está autenticado ou não está em admin./painel., deixa passar
            if (!isAuth || (!isAdminHost && !isPainelHost))
            {
                await _next(context);
                return;
            }

            var baseDomain = GetBaseDomain(host);
            string MakeUrl(string sub, string path)
                => port.HasValue
                    ? $"{scheme}://{sub}.{baseDomain}:{port}{path}"
                    : $"{scheme}://{sub}.{baseDomain}{path}";

            // Admin logado acessando painel.lvh.me -> manda para admin.lvh.me
            if (isAdmin && !isLojista && isPainelHost)
            {
                var url = MakeUrl("admin", "/Admin/Dashboard");
                context.Response.Redirect(url);
                return;
            }

            // Lojista logado acessando admin.lvh.me -> manda para painel.lvh.me
            if (isLojista && !isAdmin && isAdminHost)
            {
                var url = MakeUrl("painel", "/PainelLojista/Dashboard");
                context.Response.Redirect(url);
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
