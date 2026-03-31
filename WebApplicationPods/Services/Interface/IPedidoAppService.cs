using WebApplicationPods.Models;

namespace WebApplicationPods.Services.Interface
{
    public interface IPedidoAppService
    {
        Task<PedidoModel> CriarPedidoAsync(PedidoModel pedido, string? origem = null);

        Task<bool> AtualizarStatusAsync(
            int pedidoId,
            string novoStatus,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null);

        Task<bool> MarcarComoPagoAsync(
            int pedidoId,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null);
    }
}