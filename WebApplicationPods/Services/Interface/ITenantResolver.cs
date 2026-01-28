using Microsoft.AspNetCore.Http;

namespace WebApplicationPods.Services.Interface
{
    public interface ITenantResolver
    {
        Task<int?> ResolveLojaIdAsync(HttpContext context);
    }
}
