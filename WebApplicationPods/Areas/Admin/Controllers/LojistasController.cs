using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;
using WebApplicationPods.Utils;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Somente ADMIN gerencia lojistas
    public class LojistasController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;

        public LojistasController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /Admin/Lojistas
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var lojistas = new List<ApplicationUser>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (roles.Contains("Lojista"))
                    lojistas.Add(u);
            }

            return View(lojistas);
        }

        // GET: /Admin/Lojistas/Create
        public IActionResult Create() => View(new LojistaCreateViewModel());

        // POST: /Admin/Lojistas/Create
        [HttpPost]
        public async Task<IActionResult> Create(LojistaCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var cpf = vm.CPF.ApenasDigitos();
            var phone = vm.PhoneNumber.ApenasDigitos();

            if (!CpfValidator.EhCpfValido(cpf))
            {
                ModelState.AddModelError(nameof(vm.CPF), "CPF inválido.");
                return View(vm);
            }

            var existsCpf = await _userManager.Users.AnyAsync(u => u.CPF == cpf);
            if (existsCpf)
            {
                ModelState.AddModelError(nameof(vm.CPF), "CPF já cadastrado.");
                return View(vm);
            }

            var existsPhone = await _userManager.Users.AnyAsync(u => u.PhoneNumber == phone);
            if (existsPhone)
            {
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone já cadastrado.");
                return View(vm);
            }

            var user = new ApplicationUser
            {
                UserName = cpf,
                CPF = cpf,
                PhoneNumber = phone,
                Email = vm.Email,
                Nome = vm.Nome
            };

            var res = await _userManager.CreateAsync(user, vm.Password);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            if (!await _roleManager.RoleExistsAsync("Lojista"))
                await _roleManager.CreateAsync(new IdentityRole<int>("Lojista"));

            await _userManager.AddToRoleAsync(user, "Lojista");

            TempData["Sucesso"] = "Lojista criado com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Lojistas/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var u = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();

            var vm = new LojistaEditViewModel
            {
                Id = u.Id,
                Nome = u.Nome,
                CPF = u.CPF,
                PhoneNumber = u.PhoneNumber,
                Email = u.Email
            };
            return View(vm);
        }

        // POST: /Admin/Lojistas/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(LojistaEditViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var u = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (u == null) return NotFound();

            var cpf = vm.CPF.ApenasDigitos();
            var phone = vm.PhoneNumber.ApenasDigitos();

            if (!CpfValidator.EhCpfValido(cpf))
            {
                ModelState.AddModelError(nameof(vm.CPF), "CPF inválido.");
                return View(vm);
            }

            if (await _userManager.Users.AnyAsync(x => x.Id != vm.Id && x.CPF == cpf))
            {
                ModelState.AddModelError(nameof(vm.CPF), "CPF já cadastrado.");
                return View(vm);
            }

            if (await _userManager.Users.AnyAsync(x => x.Id != vm.Id && x.PhoneNumber == phone))
            {
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone já cadastrado.");
                return View(vm);
            }

            u.Nome = vm.Nome;
            u.CPF = cpf;
            u.PhoneNumber = phone;
            u.Email = vm.Email;

            var res = await _userManager.UpdateAsync(u);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            TempData["Sucesso"] = "Lojista atualizado.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Lojistas/ResetSenha/5
        [HttpPost]
        public async Task<IActionResult> ResetSenha(int id, string novaSenha)
        {
            if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6)
            {
                TempData["Erro"] = "Informe uma senha com no mínimo 6 caracteres.";
                return RedirectToAction(nameof(Index));
            }

            var u = await _userManager.FindByIdAsync(id.ToString());
            if (u == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(u);
            var res = await _userManager.ResetPasswordAsync(u, token, novaSenha);
            TempData[res.Succeeded ? "Sucesso" : "Erro"] =
                res.Succeeded ? "Senha redefinida." : string.Join("; ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Lojistas/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var u = await _userManager.FindByIdAsync(id.ToString());
            if (u == null) return NotFound();

            var res = await _userManager.DeleteAsync(u);
            TempData[res.Succeeded ? "Sucesso" : "Erro"] =
                res.Succeeded ? "Lojista removido." : string.Join("; ", res.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }
    }
}
