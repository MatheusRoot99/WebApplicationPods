using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("PainelLojista")]
    [Authorize(Roles = "Lojista,Admin")]
    public class EntregadoresController : Controller
    {
        private readonly BancoContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly ICurrentLojaService _currentLoja;

        public EntregadoresController(
            BancoContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            ICurrentLojaService currentLoja)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _currentLoja = currentLoja;
        }

        private int GetLojaIdOrFail()
        {
            if (_currentLoja?.LojaId is not int lojaId || lojaId <= 0)
                throw new InvalidOperationException("Loja atual não identificada.");

            return lojaId;
        }

        public async Task<IActionResult> Index()
        {
            var lojaId = GetLojaIdOrFail();

            var entregadores = await _context.Entregadores
                .Include(x => x.Usuario)
                .Where(x => x.LojaId == lojaId)
                .OrderByDescending(x => x.Ativo)
                .ThenBy(x => x.Nome)
                .ToListAsync();

            return View(entregadores);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new EntregadorCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EntregadorCreateViewModel vm)
        {
            var lojaId = GetLojaIdOrFail();

            if (!ModelState.IsValid)
                return View(vm);

            var cpf = new string((vm.CPF ?? "").Where(char.IsDigit).ToArray());
            var phone = new string((vm.PhoneNumber ?? "").Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                ModelState.AddModelError(nameof(vm.CPF), "CPF inválido.");

            if (string.IsNullOrWhiteSpace(phone))
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone inválido.");

            if (!ModelState.IsValid)
                return View(vm);

            if (!await _roleManager.RoleExistsAsync("Entregador"))
                await _roleManager.CreateAsync(new IdentityRole<int>("Entregador"));

            if (await _userManager.Users.AnyAsync(x => x.CPF == cpf))
                ModelState.AddModelError(nameof(vm.CPF), "CPF já cadastrado.");

            if (await _userManager.Users.AnyAsync(x => x.PhoneNumber == phone))
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone já cadastrado.");

            if (!string.IsNullOrWhiteSpace(vm.Email) &&
                await _userManager.Users.AnyAsync(x => x.Email == vm.Email))
            {
                ModelState.AddModelError(nameof(vm.Email), "E-mail já cadastrado.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            var user = new ApplicationUser
            {
                UserName = cpf,
                Nome = vm.Nome.Trim(),
                CPF = cpf,
                PhoneNumber = phone,
                Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
                LojaId = lojaId
            };

            var createUserResult = await _userManager.CreateAsync(user, vm.Password);
            if (!createUserResult.Succeeded)
            {
                foreach (var e in createUserResult.Errors)
                    ModelState.AddModelError("", e.Description);

                return View(vm);
            }

            await _userManager.AddToRoleAsync(user, "Entregador");

            var entregador = new EntregadorModel
            {
                LojaId = lojaId,
                UserId = user.Id,
                Nome = vm.Nome.Trim(),
                Telefone = phone,
                Veiculo = vm.Veiculo?.Trim(),
                PlacaVeiculo = vm.PlacaVeiculo?.Trim(),
                Observacoes = vm.Observacoes?.Trim(),
                Ativo = vm.Ativo,
                DataCadastro = DateTime.Now
            };

            _context.Entregadores.Add(entregador);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Entregador cadastrado com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var entregador = await _context.Entregadores
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.Id == id && x.LojaId == lojaId);

            if (entregador == null)
                return NotFound();

            var vm = new EntregadorEditViewModel
            {
                Id = entregador.Id,
                UserId = entregador.UserId,
                Nome = entregador.Nome,
                CPF = entregador.Usuario?.CPF ?? "",
                PhoneNumber = entregador.Usuario?.PhoneNumber ?? entregador.Telefone,
                Email = entregador.Usuario?.Email,
                Veiculo = entregador.Veiculo,
                PlacaVeiculo = entregador.PlacaVeiculo,
                Observacoes = entregador.Observacoes,
                Ativo = entregador.Ativo
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EntregadorEditViewModel vm)
        {
            var lojaId = GetLojaIdOrFail();

            if (!ModelState.IsValid)
                return View(vm);

            var entregador = await _context.Entregadores
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.Id == vm.Id && x.LojaId == lojaId);

            if (entregador == null)
                return NotFound();

            var cpf = new string((vm.CPF ?? "").Where(char.IsDigit).ToArray());
            var phone = new string((vm.PhoneNumber ?? "").Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                ModelState.AddModelError(nameof(vm.CPF), "CPF inválido.");

            if (string.IsNullOrWhiteSpace(phone))
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone inválido.");

            if (!ModelState.IsValid)
                return View(vm);

            if (await _userManager.Users.AnyAsync(x => x.Id != entregador.UserId && x.CPF == cpf))
                ModelState.AddModelError(nameof(vm.CPF), "CPF já cadastrado.");

            if (await _userManager.Users.AnyAsync(x => x.Id != entregador.UserId && x.PhoneNumber == phone))
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Telefone já cadastrado.");

            if (!string.IsNullOrWhiteSpace(vm.Email) &&
                await _userManager.Users.AnyAsync(x => x.Id != entregador.UserId && x.Email == vm.Email))
            {
                ModelState.AddModelError(nameof(vm.Email), "E-mail já cadastrado.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            entregador.Nome = vm.Nome.Trim();
            entregador.Telefone = phone;
            entregador.Veiculo = vm.Veiculo?.Trim();
            entregador.PlacaVeiculo = vm.PlacaVeiculo?.Trim();
            entregador.Observacoes = vm.Observacoes?.Trim();
            entregador.Ativo = vm.Ativo;

            if (entregador.Usuario != null)
            {
                entregador.Usuario.Nome = vm.Nome.Trim();
                entregador.Usuario.CPF = cpf;
                entregador.Usuario.PhoneNumber = phone;
                entregador.Usuario.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
                entregador.Usuario.LojaId = lojaId;

                var updateUserResult = await _userManager.UpdateAsync(entregador.Usuario);
                if (!updateUserResult.Succeeded)
                {
                    foreach (var e in updateUserResult.Errors)
                        ModelState.AddModelError("", e.Description);

                    return View(vm);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Entregador atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAtivo(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var entregador = await _context.Entregadores
                .FirstOrDefaultAsync(x => x.Id == id && x.LojaId == lojaId);

            if (entregador == null)
                return NotFound();

            entregador.Ativo = !entregador.Ativo;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = entregador.Ativo
                ? "Entregador ativado com sucesso."
                : "Entregador inativado com sucesso.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var entregador = await _context.Entregadores
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.Id == id && x.LojaId == lojaId);

            if (entregador == null)
                return NotFound();

            if (entregador.Usuario != null)
            {
                var roles = await _userManager.GetRolesAsync(entregador.Usuario);
                if (roles.Contains("Entregador"))
                    await _userManager.RemoveFromRoleAsync(entregador.Usuario, "Entregador");

                await _userManager.DeleteAsync(entregador.Usuario);
            }

            _context.Entregadores.Remove(entregador);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Entregador removido com sucesso.";
            return RedirectToAction(nameof(Index));
        }
    }
}