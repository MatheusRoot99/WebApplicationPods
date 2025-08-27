using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebApplicationPods.Hubs
{
    [Authorize(Roles = "Lojista")]
    public class PedidosHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // todos os lojistas entram no mesmo grupo
            await Groups.AddToGroupAsync(Context.ConnectionId, "lojistas");
            await base.OnConnectedAsync();
        }
    }
}
