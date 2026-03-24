using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class EntregaAppService : IEntregaAppService
    {
        private readonly BancoContext _context;
        private readonly IPedidoAppService _pedidoAppService;
        private readonly IHubContext<PedidosHub> _hub;

        public EntregaAppService(
            BancoContext context,
            IPedidoAppService pedidoAppService,
            IHubContext<PedidosHub> hub)
        {
            _context = context;
            _pedidoAppService = pedidoAppService;
            _hub = hub;
        }

        public async Task<bool> AtribuirEntregadorAsync(int pedidoId, int entregadorId, string? responsavel = null)
        {
            var pedido = await _context.Pedidos
                .Include(x => x.Cliente)
                .FirstOrDefaultAsync(x => x.Id == pedidoId);

            if (pedido == null)
                return false;

            var entregador = await _context.Entregadores
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.Id == entregadorId && x.Ativo);

            if (entregador == null)
                return false;

            pedido.EntregadorId = entregadorId;
            pedido.DataAtribuicaoEntregador = DateTime.Now;

            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.Atribuido,
                nomeResponsavel: responsavel,
                observacao: $"Entregador atribuído: {entregador.Nome}",
                origem: "PainelLojista");

            if (entregador.Usuario != null)
            {
                await _hub.Clients.User(entregador.Usuario.Id.ToString()).SendAsync("EntregaAtribuida", new
                {
                    pedidoId = pedido.Id,
                    cliente = pedido.Cliente?.Nome,
                    total = pedido.ValorTotal,
                    status = PedidoEntregaStatus.Atribuido
                });
            }

            return true;
        }

        public async Task<bool> MarcarSaiuParaEntregaAsync(int pedidoId, int entregadorUserId)
        {
            var pedido = await _context.Pedidos
                .Include(x => x.Entregador)
                .ThenInclude(x => x!.Usuario)
                .FirstOrDefaultAsync(x => x.Id == pedidoId);

            if (pedido == null || pedido.Entregador?.Usuario == null)
                return false;

            if (pedido.Entregador.Usuario.Id != entregadorUserId)
                return false;

            pedido.DataSaiuParaEntrega = DateTime.Now;
            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.SaiuParaEntrega,
                nomeResponsavel: pedido.Entregador.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                origem: "PainelEntregador");

            return true;
        }

        public async Task<bool> MarcarEntregueAsync(int pedidoId, int entregadorUserId)
        {
            var pedido = await _context.Pedidos
                .Include(x => x.Entregador)
                .ThenInclude(x => x!.Usuario)
                .FirstOrDefaultAsync(x => x.Id == pedidoId);

            if (pedido == null || pedido.Entregador?.Usuario == null)
                return false;

            if (pedido.Entregador.Usuario.Id != entregadorUserId)
                return false;

            pedido.DataEntregue = DateTime.Now;
            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.Entregue,
                nomeResponsavel: pedido.Entregador.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                origem: "PainelEntregador");

            return true;
        }
    }
}