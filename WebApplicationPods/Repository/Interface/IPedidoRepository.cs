
using WebApplicationPods.Models;

namespace WebApplicationPods.Repository.Interface
{
    public interface IPedidoRepository
    {
        PedidoModel ObterPorId(int id);
        IEnumerable<PedidoModel> ObterPorCliente(int clienteId);
        void Adicionar(PedidoModel pedido);
        void AtualizarStatus(int pedidoId, string status);
        decimal ObterTotalVendasHoje();
    }
}
