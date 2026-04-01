using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using WebApplicationPods.Services.service;

namespace WebApplicationPods.Hubs
{
    [Authorize(Roles = "Lojista")]
    public class PedidosHub : Hub
    {
        public const string GlobalLojistasGroup = "lojistas";

        public static string LojaGroup(int lojaId) => $"loja:{lojaId}";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GlobalLojistasGroup);

            var lojaId = ObterLojaId();
            if (lojaId.HasValue)
                await Groups.AddToGroupAsync(Context.ConnectionId, LojaGroup(lojaId.Value));

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GlobalLojistasGroup);

            var lojaId = ObterLojaId();
            if (lojaId.HasValue)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, LojaGroup(lojaId.Value));

            await base.OnDisconnectedAsync(ex);
        }

        private int? ObterLojaId()
        {
            var http = Context.GetHttpContext();
            if (http == null) return null;

            var lojaIdSession = http.Session.GetInt32(CurrentLojaService.SessionKey);
            if (lojaIdSession.HasValue && lojaIdSession.Value > 0)
                return lojaIdSession.Value;

            var claimLojaId = http.User?.FindFirst("LojaId")?.Value
                           ?? http.User?.FindFirst("lojaId")?.Value;

            return int.TryParse(claimLojaId, out var lojaIdClaim) && lojaIdClaim > 0
                ? lojaIdClaim
                : null;
        }
    }
}