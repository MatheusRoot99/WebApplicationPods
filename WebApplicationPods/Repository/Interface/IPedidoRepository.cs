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
        void AtualizarStatus(int pedidoId, string status);
        decimal ObterTotalVendasHoje();
        PedidoModel? ObterPorToken(string token);

        // ─── novos ───
        IEnumerable<PedidoModel> ObterAbertos();   // Pago / Em Preparação / Pronto / Saiu p/ Entrega / Aguardando Confirmação (Dinheiro)
        IEnumerable<PedidoModel> ObterDoDia();     // somente pedidos de hoje

        // --- NOVOS: Relatórios ---
        ResumoVendas ObterResumo(DateTime inicio, DateTime fim);
        IEnumerable<SerieDia> ObterSeriePorDia(DateTime inicio, DateTime fim);
        IEnumerable<MetodoPagamentoResumo> ObterMetodosPagamentoResumo(DateTime inicio, DateTime fim);
        IEnumerable<TopClienteResumo> ObterTopClientes(DateTime inicio, DateTime fim, int take = 5);

        // NOVO
        IEnumerable<PedidoModel> Buscar(AdminOrdersFilterDTO f);

        // ↓↓↓ NOVOS ↓↓↓
        // ↓↓↓ NOVOS ↓↓↓
        void ExcluirLogico(int id, string? usuario = null);     // soft delete
        int PurgaCanceladosAntigos(int dias = 30);              // opcional: limpeza em lote
    }
}
