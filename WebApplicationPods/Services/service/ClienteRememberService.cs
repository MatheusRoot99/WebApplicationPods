using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using System.Text;
using WebApplicationPods.Services;

public sealed class ClienteRememberService : IClienteRememberService
{
    private const string CookieName = "pods_cli";
    private readonly IDataProtector _prot;
    private readonly IWebHostEnvironment _env;

    public ClienteRememberService(IDataProtectionProvider dp, IWebHostEnvironment env)
    {
        _prot = dp.CreateProtector("remember.cliente.v1");
        _env = env;
    }

    public void SetCookie(HttpResponse response, string telefone, string? nome, TimeSpan? ttl = null)
    {
        var payload = $"{telefone}|{(nome ?? "")}";
        var protectedBytes = _prot.Protect(Encoding.UTF8.GetBytes(payload));
        var value = Convert.ToBase64String(protectedBytes);

        var opts = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromDays(90)),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = !_env.IsDevelopment() // produção: true
        };
        response.Cookies.Append(CookieName, value, opts);

        // evita cache agressivo do static web assets em dev
        response.Headers["Cache-Control"] = "no-cache, no-store";
    }

    public bool TryGetFromCookie(HttpRequest request, out string telefone, out string? nome)
    {
        telefone = "";
        nome = null;

        if (!request.Cookies.TryGetValue(CookieName, out var raw)) return false;

        try
        {
            var data = Convert.FromBase64String(raw);
            var unp = Encoding.UTF8.GetString(_prot.Unprotect(data));
            var parts = unp.Split('|', 2);
            telefone = parts.ElementAtOrDefault(0) ?? "";
            nome = parts.ElementAtOrDefault(1);
            telefone = new string(telefone.Where(char.IsDigit).ToArray());
            return telefone.Length >= 10; // 10/11 dígitos
        }
        catch { return false; }
    }

    public void ClearCookie(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, new CookieOptions { SameSite = SameSiteMode.Lax, Secure = !_env.IsDevelopment() });
    }
}
