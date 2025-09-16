namespace WebApplicationPods.Services
{
    public interface IClienteRememberService
    {
        void SetCookie(HttpResponse response, string telefone, string? nome, TimeSpan? ttl = null);
        bool TryGetFromCookie(HttpRequest request, out string telefone, out string? nome);
        void ClearCookie(HttpResponse response);
    }
}
