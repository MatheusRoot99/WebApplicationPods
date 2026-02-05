// Middlewares/SslDevelopmentMiddleware.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace WebApplicationPods.Middlewares
{
    public class SslDevelopmentMiddleware
    {
        private readonly RequestDelegate _next;

        public SslDevelopmentMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Host.Host.Contains("lvh.me") && !context.Request.IsHttps)
            {
                var httpsUrl = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                context.Response.Redirect(httpsUrl, permanent: false);
                return;
            }

            await _next(context);
        }
    }

    public static class SslDevelopmentMiddlewareExtensions
    {
        public static IApplicationBuilder UseSslDevelopment(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SslDevelopmentMiddleware>();
        }
    }
}