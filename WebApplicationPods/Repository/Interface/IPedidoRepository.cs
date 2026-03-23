using WebApplicationPods.DTO;
using WebApplicationPods.Models;
using static WebApplicationPods.DTO.ReportsDTO;

namespace WebApplicationPods.Repository.Interface
{
    public interface IPedidoRepository
    {
        PedidoModel ObterPorId(int id);
        IEnumerable<PedidoModel> ObterPorCliente(int clienteId);
        void Adicionar(PedidoModel pedido);

        void AtualizarStatus(
            int pedidoId,
            string status,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null);

        IEnumerable<PedidoHistoricoModel> ObterHistorico(int pedidoId);

        decimal ObterTotalVendasHoje();
        PedidoModel? ObterPorToken(string token);

        IEnumerable<PedidoModel> ObterAbertos();
        IEnumerable<PedidoModel> ObterDoDia();

        ResumoVendas ObterResumo(DateTime inicio, DateTime fim);
        IEnumerable<SerieDia> ObterSeriePorDia(DateTime inicio, DateTime fim);
        IEnumerable<MetodoPagamentoResumo> ObterMetodosPagamentoResumo(DateTime inicio, DateTime fim);
        IEnumerable<TopClienteResumo> ObterTopClientes(DateTime inicio, DateTime fim, int take = 5);

        IEnumerable<PedidoModel> Buscar(AdminOrdersFilterDTO f);

        void ExcluirLogico(int id, string? usuario = null);
        int PurgaCanceladosAntigos(int dias = 30);
        void Restaurar(int id);
    }
}