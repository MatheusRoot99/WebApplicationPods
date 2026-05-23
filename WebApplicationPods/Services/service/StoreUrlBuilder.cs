using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class StoreUrlBuilder : IStoreUrlBuilder
    {
        private readonly IHttpContextAccessor _http;
        private readonly IConfiguration _configuration;

        public StoreUrlBuilder(
            IHttpContextAccessor http,
            IConfiguration configuration)
        {
            _http = http;
            _configuration = configuration;
        }

        public string GetScheme()
        {
            var configured =
                _configuration["DevelopmentSettings:PublicScheme"]
                ?? _configuration["AppSettings:PublicScheme"];

            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim().ToLowerInvariant();

            var scheme = _http.HttpContext?.Request?.Scheme;
            return string.IsNullOrWhiteSpace(scheme) ? "https" : scheme;
        }

        public string GetRootHostWithPort()
        {
            var configuredBase =
                _configuration["DevelopmentSettings:SubdomainBase"]
                ?? _configuration["AppSettings:SubdomainBase"];

            var req = _http.HttpContext?.Request;

            var rootHost = !string.IsNullOrWhiteSpace(configuredBase)
                ? configuredBase.Trim().ToLowerInvariant()
                : GetRootHost(req?.Host.Host ?? "localhost");

            var configuredPort =
                _configuration["DevelopmentSettings:PublicPort"]
                ?? _configuration["AppSettings:PublicPort"];

            if (int.TryParse(configuredPort, out var portFromConfig) && portFromConfig > 0)
                return $"{rootHost}:{portFromConfig}";

            var requestPort = req?.Host.Port;

            if (requestPort.HasValue && DeveManterPorta(requestPort.Value))
                return $"{rootHost}:{requestPort.Value}";

            return rootHost;
        }

        public string BuildPublicStoreUrl(string subdominio)
        {
            var sub = NormalizeSubdomain(subdominio);
            var root = GetRootHostWithPort();
            var scheme = GetScheme();

            if (string.IsNullOrWhiteSpace(sub))
                return $"{scheme}://{root}";

            return $"{scheme}://{sub}.{root}";
        }

        public string BuildPainelUrl()
        {
            var painelSubdomain =
                _configuration["DevelopmentSettings:PainelSubdomain"]
                ?? _configuration["AppSettings:PainelSubdomain"]
                ?? "painel";

            return BuildPublicStoreUrl(painelSubdomain);
        }

        public string BuildAdminUrl()
        {
            var adminSubdomain =
                _configuration["DevelopmentSettings:AdminSubdomain"]
                ?? _configuration["AppSettings:AdminSubdomain"]
                ?? "admin";

            return BuildPublicStoreUrl(adminSubdomain);
        }

        private static bool DeveManterPorta(int port)
        {
            return port != 80 && port != 443;
        }

        private static string NormalizeSubdomain(string? subdominio)
        {
            return (subdominio ?? "")
                .Trim()
                .Trim('.')
                .ToLowerInvariant();
        }

        private static string GetRootHost(string host)
        {
            host = (host ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(host))
                return "localhost";

            if (host == "localhost")
                return host;

            if (IPAddress.TryParse(host, out _))
                return host;

            var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (labels.Length <= 2)
                return host;

            return string.Join('.', labels.Skip(1));
        }
    }
}