using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;
using WebApplicationPods.Utils;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Controllers
{
    public class ContaController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderService _emailSender;

        public ContaController(SignInManager<ApplicationUser> signInManager,
                               UserManager<ApplicationUser> userManager,
                               IEmailSenderService emailSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [HttpGet, AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
            => View(new LoginViewModel { ReturnUrl = returnUrl });

        /////////////////////////////////////////////////////////////////////////////////////////////////////////

        // POST: /Conta/Login
        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            vm.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                // Limpa a entrada (remove caracteres não numéricos)
                var entradaLimpa = LimparDigitos(vm.TelefoneOuCpf);

                if (string.IsNullOrEmpty(entradaLimpa))
                {
                    ModelState.AddModelError(string.Empty, "CPF ou Telefone inválido.");
                    return View(vm);
                }

                // Busca o usuário por CPF, Telefone ou Email
                var user = await EncontrarUsuarioPorCredencial(entradaLimpa, vm.TelefoneOuCpf);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Usuário não encontrado.");
                    return View(vm);
                }

                // Tenta fazer login usando o UserName
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    vm.Senha,
                    vm.LembrarMe,
                    lockoutOnFailure: true
                );

                if (result.Succeeded)
                {
                    TempData["MensagemErro"] = "Usuário {UserName} logou com sucesso.";

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // Redireciona baseado no role
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Admin"))
                        return RedirectToAction("Index", "Home");
                    if (roles.Contains("Lojista"))
                        return RedirectToAction("Index", "Home");

                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    TempData["MensagemErro"] = "Usuário {UserName} está bloqueado.";
                    ModelState.AddModelError(string.Empty, "Usuário temporariamente bloqueado. Tente novamente mais tarde.");
                    return View(vm);
                }

                ModelState.AddModelError(string.Empty, "Senha inválida.");
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"]="Erro durante o login para {TelefoneOuCpf}";
                ModelState.AddModelError(string.Empty, "Ocorreu um erro durante o login. Tente novamente.");
                return View(vm);
            }
        }

        private async Task<ApplicationUser> EncontrarUsuarioPorCredencial(string entradaLimpa, string entradaOriginal)
        {
            // Primeiro tenta por CPF (apenas dígitos)
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.CPF == entradaLimpa);

            if (user != null) return user;

            // Tenta por telefone (apenas dígitos)
            user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == entradaLimpa);

            if (user != null) return user;

            // Tenta por email (formato original)
            if (IsValidEmail(entradaOriginal))
            {
                user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.Email == entradaOriginal);
            }

            return user;
        }

        private string LimparDigitos(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return new string(input.Where(char.IsDigit).ToArray());
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
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


        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet, AllowAnonymous]
        public IActionResult AcessoNegado() => View();

        // ============ Esqueci minha senha ============

        [HttpGet, AllowAnonymous]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null) // Não revele que não existe
                return View("ForgotPasswordConfirmation");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action("ResetPassword", "Conta",
                new { email = vm.Email, token }, Request.Scheme);

            await _emailSender.SendAsync(vm.Email,
                "Redefinição de Senha",
                $"<p>Olá {user.Nome},</p><p>Para redefinir sua senha, clique no link abaixo:</p><p><a href=\"{link}\">Redefinir Senha</a></p>");

            return View("ForgotPasswordConfirmation");
        }

        // GET: /Conta/CheckAccount?input=...
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> CheckAccount(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Json(new { exists = false });

            // Mesmo critério do Login: CPF/Telefone (só dígitos) ou e-mail
            var entradaLimpa = new string(input.Where(char.IsDigit).ToArray());

            var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                   u.CPF == entradaLimpa
                || u.PhoneNumber == entradaLimpa
                || u.Email == input);

            // Não exponho dados sensíveis; devolvo apenas sinal de existência
            // e um "email" para pré-preencher (pode ser o próprio input)
            return Json(new { exists = user != null, email = user?.Email ?? input });
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View();

        [HttpGet, AllowAnonymous]
        public IActionResult ResetPassword(string email, string token)
            => View(new ResetPasswordViewModel { Email = email, Token = token });

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null) return RedirectToAction("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(user, vm.Token, vm.Password);
            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation");

            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
            => View();


        [AllowAnonymous]
        public async Task<IActionResult> DebugUser()
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.CPF == "02121225170");

            if (user != null)
            {
                ViewData["UserName"] = user.UserName;
                ViewData["Email"] = user.Email;
                ViewData["CPF"] = user.CPF;
                ViewData["Phone"] = user.PhoneNumber;
            }

            return View();
        }
    }


}
