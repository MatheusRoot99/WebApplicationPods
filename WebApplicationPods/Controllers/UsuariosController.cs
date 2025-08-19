using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;
using WebApplicationPods.Utils;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Admin")] // só Admin pode criar usuários
    public class UsuariosController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;

        public UsuariosController(UserManager<ApplicationUser> userManager,
                                  RoleManager<IdentityRole<int>> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = new[] { "Admin", "Lojista", "Cliente" };
            return View(new UserCreateViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserCreateViewModel vm)
        {
            ViewBag.Roles = new[] { "Admin", "Lojista", "Cliente" };

            if (!ModelState.IsValid) return View(vm);

            var cpf = vm.CPF.ApenasDigitos();
            var phone = vm.PhoneNumber.ApenasDigitos();

            if (!CpfValidator.EhCpfValido(cpf))
            {
                ModelState.AddModelError(nameof(vm.CPF), "CPF inválido.");
                return View(vm);
            }

            // unicidade de CPF e Telefone
            if (await _userManager.Users.AnyAsync(u => u.CPF == cpf))
            {
                ModelState.AddModelError(nameof(vm.CPF), "CPF já cadastrado.");
                return View(vm);
            }

            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == phone))
            {
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone já cadastrado.");
                return View(vm);
            }

            var user = new ApplicationUser
            {
                UserName = cpf,  // usa o CPF como username
                CPF = cpf,
                Nome = vm.Nome,
                PhoneNumber = phone,
                Email = vm.Email
            };

            var res = await _userManager.CreateAsync(user, vm.Password);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            // Role opcional
            if (!string.IsNullOrWhiteSpace(vm.Role))
            {
                if (!await _roleManager.RoleExistsAsync(vm.Role))
                    await _roleManager.CreateAsync(new IdentityRole<int>(vm.Role));

                await _userManager.AddToRoleAsync(user, vm.Role);
            }

            TempData["Sucesso"] = "Usuário criado com sucesso.";
            return RedirectToAction(nameof(Create));
        }
    }
}
