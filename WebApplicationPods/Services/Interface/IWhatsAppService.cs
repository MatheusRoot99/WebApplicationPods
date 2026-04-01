using WebApplicationPods.Models;

namespace WebApplicationPods.Services.Interface
{
    public interface IWhatsAppService
    {
        Task EnviarNovoPedidoClienteAsync(PedidoModel pedido);
        Task EnviarNovoPedidoLojistaAsync(PedidoModel pedido);

        Task EnviarPagamentoAprovadoClienteAsync(PedidoModel pedido);
        Task EnviarPagamentoFalhouClienteAsync(PedidoModel pedido);
        Task EnviarPedidoCanceladoClienteAsync(PedidoModel pedido);

        Task EnviarSaiuParaEntregaClienteAsync(PedidoModel pedido);
        Task EnviarPedidoEntregueClienteAsync(PedidoModel pedido);

        Task EnviarEntregaAtribuidaEntregadorAsync(PedidoModel pedido, EntregadorModel entregador);
        Task EnviarEntregaAceitaLojistaAsync(PedidoModel pedido);
        Task EnviarEntregaConcluidaLojistaAsync(PedidoModel pedido, string? nomeRecebedor = null);
        Task EnviarFalhaEntregaLojistaAsync(PedidoModel pedido, string motivo);
    }
}