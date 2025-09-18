namespace WebApplicationPods.Services.Interface
{
    public interface IEstoqueService
    {
        Task BaixarEstoquePedidoAsync(int pedidoId);
    }
}
