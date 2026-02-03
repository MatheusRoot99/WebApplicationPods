using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "Admin")]
    public class DashboardController : Controller
    {
        private readonly BancoContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(BancoContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // Lojas (reais)
            var lojasAtivas = await _db.Lojas.AsNoTracking().CountAsync(l => l.Ativa);
            var lojasCriadasMes = await _db.Lojas.AsNoTracking().CountAsync(l => l.CriadaEm >= monthStart);

            // Lojistas (reais)
            var lojistas = await _userManager.GetUsersInRoleAsync("Lojista");
            var lojistasAtivos = lojistas.Count;

            // Lojas recentes (reais)
            var lojasRecentes = await _db.Lojas
                .AsNoTracking()
                .Include(l => l.Dono)
                .OrderByDescending(l => l.CriadaEm)
                .Take(5)
                .Select(l => new LojaResumoItem
                {
                    Id = l.Id,
                    Nome = l.Nome,
                    DonoNome = l.Dono != null ? l.Dono.Nome : null,
                    Ativa = l.Ativa,
                    CriadaEm = l.CriadaEm
                })
                .ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                LojasAtivas = lojasAtivas,
                LojasCriadasMes = lojasCriadasMes,
                LojistasAtivos = lojistasAtivos,

                // ✅ quando você me mandar a tabela de pedidos pagos, a gente calcula isso real também
                ReceitaTotal = 0m,
                ReceitaMesAtual = 0m,

                LojasRecentes = lojasRecentes
            };

            return View(vm);
        }
    }
}
