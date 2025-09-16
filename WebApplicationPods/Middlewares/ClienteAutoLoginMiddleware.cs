using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services;

namespace WebApplicationPods.Middlewares
{
    public sealed class ClienteAutoLoginMiddleware
    {
        private readonly RequestDelegate _next;
        public ClienteAutoLoginMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(
            HttpContext context,
            IClienteRememberService remember,
            BancoContext db)
        {
            // Já tem sessão populada? segue
            if (!string.IsNullOrEmpty(context.Session.GetString("ClienteTelefone")))
            {
                await _next(context);
                return;
            }

            // Evita custo em assets estáticos e websockets
            if ((HttpMethods.IsGet(context.Request.Method) && Path.HasExtension(context.Request.Path))
                || context.WebSockets.IsWebSocketRequest)
            {
                await _next(context);
                return;
            }

            // Tenta o cookie lembrado
            if (remember.TryGetFromCookie(context.Request, out var tel, out var nome))
            {
                // Valida no banco (evita cookie obsoleto)
                var existe = await db.Clientes.AsNoTracking()
                    .AnyAsync(c => c.Telefone == tel);

                if (existe)
                {
                    context.Session.SetString("ClienteTelefone", tel);
                    // Sinalização opcional para a UI
                    context.Items["ClienteNomeAuto"] = nome;
                    context.Items["ClienteAutoLogin"] = true;
                }
                else
                {
                    // Cookie antigo → limpa
                    remember.ClearCookie(context.Response);
                }
            }

            await _next(context);
        }
    }
}
