using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebApplicationPods.Hubs
{
    [Authorize(Roles = "Admin,Lojista")]
    public class PedidosHub : Hub
    {
        public const string AdminGroup = "admins";

        public static string LojaGroup(int lojaId)
        {
            return $"loja:{lojaId}";
        }

        public static IReadOnlyList<string> DestinosPedido(int lojaId)
        {
            return lojaId > 0
                ? new[] { LojaGroup(lojaId), AdminGroup }
                : new[] { AdminGroup };
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole("Admin") == true)
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

            var lojaIdClaim = Context.User?.FindFirst("LojaId")?.Value
                           ?? Context.User?.FindFirst("lojaId")?.Value;

            if (int.TryParse(lojaIdClaim, out var lojaId) && lojaId > 0)
                await Groups.AddToGroupAsync(Context.ConnectionId, LojaGroup(lojaId));

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);

            var lojaIdClaim = Context.User?.FindFirst("LojaId")?.Value
                           ?? Context.User?.FindFirst("lojaId")?.Value;

            if (int.TryParse(lojaIdClaim, out var lojaId) && lojaId > 0)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, LojaGroup(lojaId));

            await base.OnDisconnectedAsync(ex);
        }
    }
}