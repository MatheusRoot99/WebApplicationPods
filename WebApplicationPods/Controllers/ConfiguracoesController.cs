using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Models;
using WebApplicationPods.Services;

namespace WebApplicationPods.Controllers
{
    [Authorize]
    public class ConfiguracoesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILojaConfigService _lojaConfigService;

        public ConfiguracoesController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILojaConfigService lojaConfigService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _lojaConfigService = lojaConfigService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = await MontarViewModelAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarPerfil(ConfiguracoesViewModel vm)
        {
            ModelState.Remove(nameof(vm.SenhaAtual));
            ModelState.Remove(nameof(vm.NovaSenha));
            ModelState.Remove(nameof(vm.ConfirmarSenha));
            ModelState.Remove(nameof(vm.TemaAtual));
            ModelState.Remove(nameof(vm.NomeLoja));
            ModelState.Remove(nameof(vm.Cpf));

            if (string.IsNullOrWhiteSpace(vm.NomeUsuario))
                ModelState.AddModelError(nameof(vm.NomeUsuario), "Informe o nome do usuário.");

            if (!string.IsNullOrWhiteSpace(vm.Email) && !IsValidEmail(vm.Email))
                ModelState.AddModelError(nameof(vm.Email), "Informe um e-mail válido.");

            var telefoneDigitos = LimparDigitos(vm.Telefone ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(vm.Telefone) && telefoneDigitos.Length < 10)
                ModelState.AddModelError(nameof(vm.Telefone), "Informe um telefone válido com DDD.");

            if (!ModelState.IsValid)
            {
                await CompletarViewModelAsync(vm);
                return View("Index", vm);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var nome = vm.NomeUsuario!.Trim();

            user.Nome = nome;
            user.PhoneNumber = string.IsNullOrWhiteSpace(telefoneDigitos) ? null : telefoneDigitos;

            var email = vm.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(email) &&
                !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var emailOwner = await _userManager.FindByEmailAsync(email);
                if (emailOwner != null && emailOwner.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(vm.Email), "Este e-mail já está em uso.");
                    await CompletarViewModelAsync(vm);
                    return View("Index", vm);
                }

                user.Email = email;
                user.NormalizedEmail = _userManager.NormalizeEmail(email);
                user.EmailConfirmed = false;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["MensagemErro"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["MensagemSucesso"] = "Perfil atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarSenha(ConfiguracoesViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.SenhaAtual) ||
                string.IsNullOrWhiteSpace(vm.NovaSenha) ||
                string.IsNullOrWhiteSpace(vm.ConfirmarSenha))
            {
                TempData["MensagemErro"] = "Preencha a senha atual, a nova senha e a confirmação.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(vm.NovaSenha, vm.ConfirmarSenha, StringComparison.Ordinal))
            {
                TempData["MensagemErro"] = "A confirmação da senha não confere.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var result = await _userManager.ChangePasswordAsync(user, vm.SenhaAtual, vm.NovaSenha);

            if (!result.Succeeded)
            {
                TempData["MensagemErro"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["MensagemSucesso"] = "Senha alterada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<ConfiguracoesViewModel> MontarViewModelAsync()
        {
            var vm = new ConfiguracoesViewModel();
            await CompletarViewModelAsync(vm);
            return vm;
        }

        private async Task CompletarViewModelAsync(ConfiguracoesViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            var loja = await _lojaConfigService.GetAsync();

            if (user != null)
            {
                vm.Email = user.Email;
                vm.Telefone = user.PhoneNumber;
                vm.Cpf = user.CPF;
                vm.NomeUsuario = string.IsNullOrWhiteSpace(user.Nome) ? user.UserName : user.Nome;
            }

            vm.NomeLoja = loja?.Nome ?? "Conveniência";
            vm.TemaAtual = "light";
        }

        private static string LimparDigitos(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return new string(input.Where(char.IsDigit).ToArray());
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
