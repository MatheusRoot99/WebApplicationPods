using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Repository.Repository
{
    public class LojaConfigRepository : ILojaConfigRepository
    {
        private readonly BancoContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _http;
        private readonly ICurrentLojaService _currentLoja;

        public LojaConfigRepository(
            BancoContext db,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor http,
            ICurrentLojaService currentLoja)
        {
            _db = db;
            _userManager = userManager;
            _http = http;
            _currentLoja = currentLoja;
        }

        public LojaConfig? ObterDoLojistaAtual()
        {
            if (_currentLoja.LojaId is int lojaId && lojaId > 0)
            {
                var porLojaAtual = ObterPorLojaId(lojaId);
                if (porLojaAtual != null)
                    return porLojaAtual;
            }

            var principal = _http.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var userIdStr = _userManager.GetUserId(principal);
                if (int.TryParse(userIdStr, out var userId))
                {
                    var porLojista = _db.LojaConfigs
                        .FirstOrDefault(l => l.LojistaUserId == userId);

                    if (porLojista != null)
                        return porLojista;
                }
            }

            return null;
        }

        public LojaConfig? ObterPorUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            if (!int.TryParse(userId, out var userIdInt))
                return null;

            return _db.LojaConfigs.FirstOrDefault(l => l.LojistaUserId == userIdInt);
        }

        public LojaConfig? ObterPorLojaId(int lojaId)
        {
            if (lojaId <= 0)
                return null;

            return _db.LojaConfigs.FirstOrDefault(l => l.LojaId == lojaId);
        }

        public LojaConfig Salvar(LojaConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            config.UpdatedAt = DateTime.UtcNow;

            if (config.Id == 0)
                _db.LojaConfigs.Add(config);
            else
                _db.LojaConfigs.Update(config);

            _db.SaveChanges();
            return config;
        }
    }
}