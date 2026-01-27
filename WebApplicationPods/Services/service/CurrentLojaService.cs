using Microsoft.AspNetCore.Http;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class CurrentLojaService : ICurrentLojaService
    {
        public const string SessionKey = "CurrentLojaId";

        private readonly IHttpContextAccessor _http;

        public CurrentLojaService(IHttpContextAccessor http)
        {
            _http = http;
        }

        public int? LojaId
        {
            get
            {
                var ctx = _http.HttpContext;
                if (ctx == null) return null;
                return ctx.Session.GetInt32(SessionKey);
            }
        }

        public bool HasLoja => LojaId.HasValue;

        public void SetLojaId(int lojaId)
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return;
            ctx.Session.SetInt32(SessionKey, lojaId);
        }

        public void ClearLoja()
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return;
            ctx.Session.Remove(SessionKey);
        }
    }
}
