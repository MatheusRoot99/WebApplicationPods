namespace WebApplicationPods.Services.Interface
{
    public interface IEntregaAppService
    {
        Task<bool> AtribuirEntregadorAsync(int pedidoId, int entregadorId, string? responsavel = null);
        Task<bool> AceitarEntregaAsync(int pedidoId, int entregadorUserId);
        Task<bool> MarcarColetadaAsync(int pedidoId, int entregadorUserId);
        Task<bool> MarcarSaiuParaEntregaAsync(int pedidoId, int entregadorUserId);
        Task<bool> MarcarEntregueAsync(int pedidoId, int entregadorUserId, string nomeRecebedor, string? observacaoEntrega = null, string? comprovanteEntregaUrl = null);
        Task<bool> MarcarNaoEntregueAsync(int pedidoId, int entregadorUserId, string? motivo = null);
    }
}