using WebApplicationPods.Models;

namespace WebApplicationPods.Services
{
    public interface ILojaConfigService
    {
        Task<LojaConfig?> GetAsync();
        Task<LojaConfig?> UpsertAsync(LojaConfig input);
        StoreHeaderViewModel? BuildHeader(LojaConfig? cfg, string baseUrl, string? perfilUrl);
        bool EstaAberto(LojaConfig cfg, DateTime agoraLocal);
    }
}
