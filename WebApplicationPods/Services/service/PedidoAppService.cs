using Microsoft.AspNetCore.SignalR;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class PedidoAppService : IPedidoAppService
    {
        private readonly IPedidoRepository _pedidoRepository;
        private readonly IHubContext<PedidosHub> _hub;

        public PedidoAppService(
            IPedidoRepository pedidoRepository,
            IHubContext<PedidosHub> hub)
        {
            _pedidoRepository = pedidoRepository;
            _hub = hub;
        }

        public async Task<PedidoModel> CriarPedidoAsync(PedidoModel pedido, string? origem = null)
        {
            if (pedido == null)
                throw new ArgumentNullException(nameof(pedido));

            if (pedido.PedidoItens == null || !pedido.PedidoItens.Any())
                throw new ArgumentException("O pedido precisa ter itens.");

            _pedidoRepository.Adicionar(pedido);

            var pedidoCriado = _pedidoRepository.ObterPorId(pedido.Id) ?? pedido;

            var group = pedidoCriado.LojaId > 0
                ? PedidosHub.LojaGroup(pedidoCriado.LojaId)
                : PedidosHub.GlobalLojistasGroup;

            await _hub.Clients.Group(group).SendAsync("NewOrder", new
            {
                id = pedidoCriado.Id,
                status = pedidoCriado.Status,
                total = pedidoCriado.ValorTotal,
                metodo = pedidoCriado.MetodoPagamento,
                data = pedidoCriado.DataPedido,
                cliente = new
                {
                    id = pedidoCriado.ClienteId,
                    nome = pedidoCriado.Cliente?.Nome ?? $"Cliente #{pedidoCriado.ClienteId}"
                },
                origem = origem
            });

            await _hub.Clients.Group(group).SendAsync("PedidosChanged", new
            {
                id = pedidoCriado.Id,
                status = pedidoCriado.Status
            });

            return pedidoCriado;
        }

        public async Task<bool> AtualizarStatusAsync(
            int pedidoId,
            string novoStatus,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null)
        {
            var pedido = _pedidoRepository.ObterPorId(pedidoId);
            if (pedido == null)
                return false;

            if (string.Equals(pedido.Status, novoStatus, StringComparison.OrdinalIgnoreCase))
                return true;

            _pedidoRepository.AtualizarStatus(
                pedidoId,
                novoStatus,
                nomeResponsavel,
                usuarioResponsavelId,
                observacao,
                origem);

            var atualizado = _pedidoRepository.ObterPorId(pedidoId);
            if (atualizado == null)
                return false;

            var group = atualizado.LojaId > 0
                ? PedidosHub.LojaGroup(atualizado.LojaId)
                : PedidosHub.GlobalLojistasGroup;

            await _hub.Clients.Group(group).SendAsync("PedidosChanged", new
            {
                id = atualizado.Id,
                status = atualizado.Status
            });

            return true;
        }

        public async Task<bool> MarcarComoPagoAsync(
            int pedidoId,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null)
        {
            return await AtualizarStatusAsync(
                pedidoId,
                "Pago",
                nomeResponsavel,
                usuarioResponsavelId,
                observacao,
                origem);
        }
    }
}