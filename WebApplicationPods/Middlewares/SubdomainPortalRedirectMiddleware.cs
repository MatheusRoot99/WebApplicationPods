namespace WebApplicationPods.Middlewares
{
    public class SubdomainPortalRedirectMiddleware
    {
        private readonly RequestDelegate _next;

        public SubdomainPortalRedirectMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            var host = context.Request.Host.Host?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(host))
            {
                await _next(context);
                return;
            }

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length >= 3 ? parts[0] : null; // ex: admin.lvh.me

            // Só redireciona se estiver na raiz "/"
            if (context.Request.Path == "/" && (sub == "admin" || sub == "painel"))
            {
                context.Response.Redirect(sub == "admin"
                    ? "/Admin/Dashboard"
                    : "/Painel/Dashboard");
                return;
            }

            await _next(context);
        }
    }
}
