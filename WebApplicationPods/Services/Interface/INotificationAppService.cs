using WebApplicationPods.Models;

namespace WebApplicationPods.Services.Interface
{
    public interface INotificationAppService
    {
        Task<NotificacaoModel> CriarAsync(
            int lojaId,
            string titulo,
            string mensagem,
            string tipo = "info",
            int? pedidoId = null);

        Task<List<NotificacaoModel>> ObterRecentesAsync(
            int lojaId,
            int take = 8,
            bool incluirLidas = true);

        Task<List<NotificacaoModel>> ObterCentralAsync(
            int lojaId,
            bool somenteNaoLidas = false,
            int take = 100);

        Task<int> ContarNaoLidasAsync(int lojaId);

        Task<NotificacaoModel?> ObterPorIdAsync(int id, int lojaId);

        Task<bool> MarcarComoLidaAsync(int id, int lojaId);

        Task<int> MarcarTodasComoLidasAsync(int lojaId);
    }
}