using Microsoft.AspNetCore.SignalR;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Helper
{
    public class PedidoDomainService
    {
        private readonly IPedidoRepository _repo;
        private readonly IHubContext<PedidosHub> _hub; // <- alinhe com o nome do seu Hub real

        public PedidoDomainService(IPedidoRepository repo, IHubContext<PedidosHub> hub)
        {
            _repo = repo;
            _hub = hub;
        }

        /// <summary>
        /// Marca o pedido como "Pago" (idempotente) e notifica lojistas em tempo real.
        /// </summary>
        public async Task MarcarComoPagoAsync(int pedidoId)
        {
            var pedido = _repo.ObterPorId(pedidoId);
            if (pedido == null) return;

            // Idempotência: não faz nada se já estiver pago
            if (!string.Equals(pedido.Status, "Pago", StringComparison.OrdinalIgnoreCase))
            {
                // Atualiza via método que EXISTE na interface
                _repo.AtualizarStatus(pedido.Id, "Pago");

                // Se você quiser gravar o timestamp DataPagamentoAprovado,
                // crie um método no repositório para isso, ou injete o DbContext aqui
                // e atualize esse campo diretamente.
            }

            await _hub.Clients.Group("lojistas").SendAsync("NewOrder", new
            {
                id = pedido.Id,
                cliente = pedido.Cliente?.Nome ?? $"Cliente #{pedido.ClienteId}",
                valor = pedido.ValorTotal,
                quando = pedido.DataPedido.ToString("o"),
                metodo = pedido.MetodoPagamento,
                status = "Pago",
                retirada = pedido.RetiradaNoLocal
            });
        }
    }
}
