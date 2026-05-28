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
            ModelState.Remove(nameof(vm.Email));
            ModelState.Remove(nameof(vm.NomeLoja));

            if (string.IsNullOrWhiteSpace(vm.NomeUsuario))
                ModelState.AddModelError(nameof(vm.NomeUsuario), "Informe o nome do usuário.");

            if (!ModelState.IsValid)
            {
                await CompletarViewModelAsync(vm);
                return View("Index", vm);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var nome = vm.NomeUsuario!.Trim();

            var alterouNome = SetStringPropertyIfExists(user, nome,
                "Nome",
                "NomeCompleto",
                "NomeUsuario",
                "FullName",
                "DisplayName");

            if (!alterouNome)
            {
                TempData["MensagemErro"] = "Não encontrei um campo de nome no usuário. Se quiser, no próximo passo ajustamos o ApplicationUser para ter Nome.";
                return RedirectToAction(nameof(Index));
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
                vm.NomeUsuario = GetStringPropertyIfExists(user,
                    "Nome",
                    "NomeCompleto",
                    "NomeUsuario",
                    "FullName",
                    "DisplayName") ?? user.UserName;
            }

            vm.NomeLoja = loja?.Nome ?? "Conveniência";
            vm.TemaAtual = "light";
        }

        private static string? GetStringPropertyIfExists(object obj, params string[] names)
        {
            var type = obj.GetType();

            foreach (var name in names)
            {
                var prop = type.GetProperty(name);

                if (prop == null || prop.PropertyType != typeof(string))
                    continue;

                var value = prop.GetValue(obj) as string;

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        private static bool SetStringPropertyIfExists(object obj, string value, params string[] names)
        {
            var type = obj.GetType();

            foreach (var name in names)
            {
                var prop = type.GetProperty(name);

                if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(string))
                    continue;

                prop.SetValue(obj, value);
                return true;
            }

            return false;
        }
    }
}