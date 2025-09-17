using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Repository.Repository
{
    public class LojaConfigRepository : ILojaConfigRepository
    {
        private readonly BancoContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _http;

        public LojaConfigRepository(
            BancoContext db,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor http)
        {
            _db = db;
            _userManager = userManager;
            _http = http;
        }

        /// <summary>
        /// Tenta obter a config da loja do lojista autenticado.
        /// Fallback: retorna a LojaConfig mais recente (UpdatedAt desc).
        /// </summary>
        public LojaConfig? ObterDoLojistaAtual()
        {
            var principal = _http.HttpContext?.User;

            // ⚠️ correção: checagem clara de autenticação
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(principal); // string (ex.: "12")
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var porLojista = _db.LojaConfigs.FirstOrDefault(l => l.LojistaUserId == userId);
                    if (porLojista != null)
                        return porLojista;
                }
            }

            // Fallback robusto: pega a mais recente
            return _db.LojaConfigs
                      .OrderByDescending(x => x.UpdatedAt)
                      .FirstOrDefault();
        }

        public LojaConfig? ObterPorUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            return _db.LojaConfigs.FirstOrDefault(l => l.LojistaUserId == userId);
        }

        public LojaConfig Salvar(LojaConfig config)
        {
            if (config.Id == 0) _db.LojaConfigs.Add(config);
            else _db.LojaConfigs.Update(config);

            _db.SaveChanges();
            return config;
        }
    }
}
