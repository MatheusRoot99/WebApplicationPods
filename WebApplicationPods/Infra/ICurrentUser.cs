// Infra/ICurrentUser.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

public interface ICurrentUser
{
    int? UserId { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public int? UserId
    {
        get
        {
            var idStr = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }
    }
}
