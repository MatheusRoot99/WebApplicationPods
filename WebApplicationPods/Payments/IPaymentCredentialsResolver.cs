using System.Security.Claims;

namespace WebApplicationPods.Payments
{
    public interface IPaymentCredentialsResolver
    {
        Task<T> GetAsync<T>(ClaimsPrincipal user, string provider) where T : class, new();
        Task<T> GetForLojaAsync<T>(int lojaId, string provider) where T : class, new();
    }
}