namespace WebApplicationPods.Services.Interface
{
    public interface IEntregaAppService
    {
        Task<bool> AtribuirEntregadorAsync(int pedidoId, int entregadorId, string? responsavel = null);
        Task<bool> MarcarSaiuParaEntregaAsync(int pedidoId, int entregadorUserId);
        Task<bool> MarcarEntregueAsync(int pedidoId, int entregadorUserId);
    }
}