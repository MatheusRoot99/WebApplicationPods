using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LojasController : Controller
    {
        private readonly BancoContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentLojaService _currentLoja;

        public LojasController(
            BancoContext context,
            UserManager<ApplicationUser> userManager,
            ICurrentLojaService currentLoja)
        {
            _context = context;
            _userManager = userManager;
            _currentLoja = currentLoja;
        }

        // GET: /Admin/Lojas
        public async Task<IActionResult> Index()
        {
            var lojas = await _context.Lojas
                .Include(l => l.Dono)
                .Include(l => l.Config)
                .OrderByDescending(l => l.CriadaEm)
                .ToListAsync();

            return View(lojas);
        }

        // GET: /Admin/Lojas/Create
        public async Task<IActionResult> Create()
        {
            var vm = new LojaFormViewModel
            {
                Ativa = true,
                Plano = "Basic",
                Lojistas = await GetLojistasSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCurrent(int? lojaId, string? returnUrl = null)
        {
            if (lojaId.HasValue && lojaId.Value > 0)
                _currentLoja.SetLojaId(lojaId.Value);
            else
                _currentLoja.ClearLoja();

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }


        // POST: /Admin/Lojas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LojaFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Lojistas = await GetLojistasSelectListAsync(vm.DonoUserId);
                return View(vm);
            }

            var sub = NormalizeSubdominio(vm.Subdominio);

            var existsSub = await _context.Lojas.AnyAsync(l => l.Subdominio == sub);
            if (existsSub)
            {
                ModelState.AddModelError(nameof(vm.Subdominio), "Já existe uma loja com esse subdomínio.");
                vm.Lojistas = await GetLojistasSelectListAsync(vm.DonoUserId);
                return View(vm);
            }

            var loja = new LojaModel
            {
                Nome = vm.Nome,
                Subdominio = sub,
                Plano = vm.Plano,
                Ativa = vm.Ativa,
                DonoUserId = vm.DonoUserId,
                CriadaEm = DateTime.UtcNow
            };

            _context.Lojas.Add(loja);
            await _context.SaveChangesAsync();

            // cria config padrão da loja
            var config = new LojaConfig
            {
                LojaId = loja.Id,
                Nome = loja.Nome,
                LojistaUserId = vm.DonoUserId
            };

            _context.LojaConfigs.Add(config);
            await _context.SaveChangesAsync();

            // vincula lojista à loja (ApplicationUser.LojaId)
            if (vm.DonoUserId.HasValue)
            {
                var user = await _userManager.FindByIdAsync(vm.DonoUserId.Value.ToString());
                if (user != null)
                {
                    user.LojaId = loja.Id;
                    await _userManager.UpdateAsync(user);
                }
            }

            TempData["Sucesso"] = "Loja criada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Lojas/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var loja = await _context.Lojas
                .Include(l => l.Config)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loja == null) return NotFound();

            var vm = new LojaFormViewModel
            {
                Id = loja.Id,
                Nome = loja.Nome,
                Subdominio = loja.Subdominio,
                Plano = loja.Plano,
                Ativa = loja.Ativa,
                DonoUserId = loja.DonoUserId,
                Lojistas = await GetLojistasSelectListAsync(loja.DonoUserId)
            };

            return View(vm);
        }

        // POST: /Admin/Lojas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LojaFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Lojistas = await GetLojistasSelectListAsync(vm.DonoUserId);
                return View(vm);
            }

            var loja = await _context.Lojas
                .Include(l => l.Config)
                .FirstOrDefaultAsync(l => l.Id == vm.Id);

            if (loja == null) return NotFound();

            var sub = NormalizeSubdominio(vm.Subdominio);

            var existsSub = await _context.Lojas
                .AnyAsync(l => l.Id != loja.Id && l.Subdominio == sub);

            if (existsSub)
            {
                ModelState.AddModelError(nameof(vm.Subdominio), "Já existe uma loja com esse subdomínio.");
                vm.Lojistas = await GetLojistasSelectListAsync(vm.DonoUserId);
                return View(vm);
            }

            var oldDonoId = loja.DonoUserId;

            loja.Nome = vm.Nome;
            loja.Subdominio = sub;
            loja.Plano = vm.Plano;
            loja.Ativa = vm.Ativa;
            loja.DonoUserId = vm.DonoUserId;

            if (loja.Config != null)
            {
                loja.Config.Nome = vm.Nome;
                loja.Config.LojistaUserId = vm.DonoUserId;
                loja.Config.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // se trocou o dono, atualiza LojaId dos usuários
            if (oldDonoId.HasValue && oldDonoId != vm.DonoUserId)
            {
                var oldUser = await _userManager.FindByIdAsync(oldDonoId.Value.ToString());
                if (oldUser != null && oldUser.LojaId == loja.Id)
                {
                    oldUser.LojaId = null;
                    await _userManager.UpdateAsync(oldUser);
                }
            }

            if (vm.DonoUserId.HasValue)
            {
                var newUser = await _userManager.FindByIdAsync(vm.DonoUserId.Value.ToString());
                if (newUser != null)
                {
                    newUser.LojaId = loja.Id;
                    await _userManager.UpdateAsync(newUser);
                }
            }

            TempData["Sucesso"] = "Loja atualizada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Lojas/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var loja = await _context.Lojas
                .Include(l => l.Config)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loja == null) return NotFound();

            // limpa LojaId de usuários que estejam apontando para essa loja
            var users = await _userManager.Users
                .Where(u => u.LojaId == loja.Id)
                .ToListAsync();

            foreach (var u in users)
            {
                u.LojaId = null;
                await _userManager.UpdateAsync(u);
            }

            _context.Lojas.Remove(loja);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Loja removida.";
            return RedirectToAction(nameof(Index));
        }

        // ===== helpers =====

        private async Task<List<SelectListItem>> GetLojistasSelectListAsync(int? selectedId = null)
        {
            var lojistas = await _userManager.GetUsersInRoleAsync("Lojista");

            return lojistas
                .OrderBy(u => u.Nome)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.Nome} ({u.CPF})",
                    Selected = selectedId.HasValue && u.Id == selectedId.Value
                })
                .ToList();
        }

        private static string NormalizeSubdominio(string sub)
        {
            sub = (sub ?? "").Trim().ToLowerInvariant();

            sub = sub.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(sub.Length);
            foreach (var ch in sub)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            sub = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

            sub = System.Text.RegularExpressions.Regex.Replace(sub, @"\s+", "-");
            sub = System.Text.RegularExpressions.Regex.Replace(sub, @"[^a-z0-9-]", "");
            sub = System.Text.RegularExpressions.Regex.Replace(sub, @"-{2,}", "-");
            sub = sub.Trim('-');

            return sub;
        }

    }
}
