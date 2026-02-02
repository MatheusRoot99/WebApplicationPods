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

            // ✅ sempre permite rotas de conta e páginas estáticas
            if (path.StartsWith("/conta") ||
                path.StartsWith("/home") ||
                path.StartsWith("/hubs") ||
                path.StartsWith("/_blazor") ||
                path.Contains("login") ||
                path.Contains("logout") ||
                path.Contains("forgotpassword") ||
                path.Contains("resetpassword") ||
                path.Contains("error"))
            {
                await _next(context);
                return;
            }

            // evita custo em arquivos estáticos
            if (HttpMethods.IsGet(context.Request.Method) && Path.HasExtension(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var user = context.User;
            var host = context.Request.Host.Host?.Trim().ToLowerInvariant() ?? "";
            var port = context.Request.Host.Port;
            var scheme = context.Request.Scheme;

            bool isAdminHost = host.StartsWith("admin.");
            bool isPainelHost = host.StartsWith("painel.");

            bool isAuth = user?.Identity?.IsAuthenticated == true;
            bool isAdmin = isAuth && user.IsInRole("Admin");
            bool isLojista = isAuth && user.IsInRole("Lojista");

            // Se não está autenticado, deixa passar (será tratado pelo auth do ASP.NET)
            if (!isAuth)
            {
                await _next(context);
                return;
            }

            // Se está autenticado mas não está em admin./painel., deixa passar
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

            // ✅ Admin logado tentando acessar painel.*
            if (isAdmin && !isLojista && isPainelHost)
            {
                // Se está tentando acessar algo específico do lojista, redireciona para admin
                if (path.StartsWith("/painellojista") || path.StartsWith("/painel"))
                {
                    context.Response.Redirect(MakeUrl("admin", "/Admin/Dashboard"));
                    return;
                }
            }

            // ✅ Lojista logado tentando acessar admin.*
            if (isLojista && !isAdmin && isAdminHost)
            {
                // Se está tentando acessar admin area, redireciona para painel
                if (path.StartsWith("/admin") || path.StartsWith("/administracao"))
                {
                    context.Response.Redirect(MakeUrl("painel", "/PainelLojista/Dashboard"));
                    return;
                }
            }

            // ✅ Usuário autenticado sem role tentando acessar admin/painel
            if (isAuth && !isAdmin && !isLojista && (isAdminHost || isPainelHost))
            {
                // Usuário comum não pode acessar admin/painel
                context.Response.Redirect("/Conta/AcessoNegado");
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