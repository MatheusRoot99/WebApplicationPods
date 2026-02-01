using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class StoreUrlBuilder : IStoreUrlBuilder
    {
        private readonly IHttpContextAccessor _http;

        public StoreUrlBuilder(IHttpContextAccessor http)
        {
            _http = http;
        }

        public string GetRootHostWithPort()
        {
            var req = _http.HttpContext?.Request;
            if (req == null) return "localhost";

            var host = string.IsNullOrWhiteSpace(req.Host.Host) ? "localhost" : req.Host.Host!;
            var port = req.Host.Port;

            var rootHost = GetRootHost(host);
            return port.HasValue ? $"{rootHost}:{port.Value}" : rootHost;
        }

        public string GetScheme()
        {
            var scheme = _http.HttpContext?.Request?.Scheme;
            return string.IsNullOrWhiteSpace(scheme) ? "https" : scheme!;
        }

        public string BuildPublicStoreUrl(string subdominio)
        {
            var scheme = GetScheme();
            var root = GetRootHostWithPort();

            var sub = (subdominio ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sub))
                return $"{scheme}://{root}";

            return $"{scheme}://{sub}.{root}";
        }

        private static string GetRootHost(string host)
        {
            host = (host ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(host)) return "localhost";

            if (host == "localhost") return host;
            if (IPAddress.TryParse(host, out _)) return host;

            var labels = host.Split('.', System.StringSplitOptions.RemoveEmptyEntries);
            if (labels.Length <= 2) return host;

            // Remove o primeiro label (admin / painel / lojaX / etc)
            return string.Join('.', labels.Skip(1));
        }
    }
}
