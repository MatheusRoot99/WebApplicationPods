using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Controllers
{
    public class ContaController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderService _emailSender;

        public ContaController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSenderService emailSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        // =========================
        // LOGIN
        // =========================
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var isAdmin = roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
                    var isLojista = roles.Any(r => r.Equals("Lojista", StringComparison.OrdinalIgnoreCase));
                    var isEntregador = roles.Any(r => r.Equals("Entregador", StringComparison.OrdinalIgnoreCase));

                    // se estiver autenticado, mas sem role válida, derruba sessão
                    if (!isAdmin && !isLojista && !isEntregador)
                    {
                        await _signInManager.SignOutAsync();
                        HttpContext.Session.Clear();

                        Response.Cookies.Delete("Pods.Auth");
                        Response.Cookies.Delete("Pods.AntiForgery");
                        Response.Cookies.Delete("SitePods.Session");

                        TempData["Erro"] = "Sua sessão anterior não tem permissão para este portal. Faça login novamente.";
                        ViewData["ReturnUrl"] = returnUrl;
                        return View(new LoginViewModel { ReturnUrl = returnUrl });
                    }
                }

                return RedirectToAppropriatePage();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
        {
            returnUrl = !string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : vm.ReturnUrl;
            ViewData["ReturnUrl"] = returnUrl;
            vm.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                var entradaOriginal = vm.TelefoneOuCpf?.Trim() ?? string.Empty;
                var entradaDigitos = LimparDigitos(entradaOriginal);
                var ehEmail = IsValidEmail(entradaOriginal);

                if (string.IsNullOrWhiteSpace(entradaDigitos) && !ehEmail)
                {
                    ModelState.AddModelError(string.Empty, "CPF/Telefone/E-mail inválido.");
                    return View(vm);
                }

                var user = await EncontrarUsuarioPorCredencial(entradaDigitos, entradaOriginal);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Usuário não encontrado.");
                    return View(vm);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    vm.Senha,
                    vm.LembrarMe,
                    lockoutOnFailure: true
                );

                if (!result.Succeeded)
                {
                    if (result.IsLockedOut)
                    {
                        ModelState.AddModelError(string.Empty, "Usuário temporariamente bloqueado. Tente novamente mais tarde.");
                        return View(vm);
                    }

                    ModelState.AddModelError(string.Empty, "Senha inválida.");
                    return View(vm);
                }

                var roles = await _userManager.GetRolesAsync(user);
                var isAdmin = roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
                var isLojista = roles.Any(r => string.Equals(r, "Lojista", StringComparison.OrdinalIgnoreCase));
                var isEntregador = roles.Any(r => string.Equals(r, "Entregador", StringComparison.OrdinalIgnoreCase));

                // bloqueia usuário sem perfil
                if (!isAdmin && !isLojista && !isEntregador)
                {
                    await _signInManager.SignOutAsync();
                    HttpContext.Session.Clear();

                    ModelState.AddModelError(string.Empty, "Seu usuário está sem perfil de acesso (Admin/Lojista/Entregador). Fale com o administrador.");
                    return View(vm);
                }

                // respeita returnUrl somente se for local e compatível com a role
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    if (IsReturnUrlCompatibleWithRole(returnUrl, isAdmin, isLojista, isEntregador))
                        return Redirect(returnUrl);
                }

                // fallbacks corretos
                if (isAdmin)
                    return RedirectToAction("Index", "Lojistas", new { area = "Admin" });

                if (isLojista)
                    return RedirectToAction("Index", "Dashboard", new { area = "PainelLojista" });

                return RedirectToAction("Index", "Entregador");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no login: {ex}");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro durante o login. Tente novamente.");
                return View(vm);
            }
        }

        private static bool IsReturnUrlCompatibleWithRole(string returnUrl, bool isAdmin, bool isLojista, bool isEntregador)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return false;

            if (!returnUrl.StartsWith("/", StringComparison.Ordinal))
                return false;

            if (isAdmin && returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (isLojista && returnUrl.StartsWith("/PainelLojista", StringComparison.OrdinalIgnoreCase))
                return true;

            if (isEntregador && returnUrl.StartsWith("/Entregador", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private IActionResult RedirectToAppropriatePage()
        {
            var isAdmin = User.IsInRole("Admin");
            var isLojista = User.IsInRole("Lojista");
            var isEntregador = User.IsInRole("Entregador");

            if (isAdmin)
                return RedirectToAction("Index", "Lojistas", new { area = "Admin" });

            if (isLojista)
                return RedirectToAction("Index", "Dashboard", new { area = "PainelLojista" });

            if (isEntregador)
                return RedirectToAction("Index", "Entregador");

            return RedirectToAction("Index", "Home");
        }

        // =========================
        // LOGOUT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();

            Response.Cookies.Delete("Pods.Auth");
            Response.Cookies.Delete("Pods.AntiForgery");
            Response.Cookies.Delete("SitePods.Session");

            return RedirectToAction("Login", "Conta");
        }

        // =========================
        // ACESSO NEGADO + RECUPERAÇÃO
        // =========================
        [HttpGet, AllowAnonymous]
        public IActionResult AcessoNegado() => View();

        [HttpGet, AllowAnonymous]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null)
                return View("ForgotPasswordConfirmation");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action("ResetPassword", "Conta", new { email = vm.Email, token }, Request.Scheme);

            await _emailSender.SendAsync(
                vm.Email,
                "Redefinição de Senha",
                $"<p>Olá {user.Nome},</p><p>Para redefinir sua senha, clique no link abaixo:</p><p><a href=\"{link}\">Redefinir Senha</a></p>"
            );

            return View("ForgotPasswordConfirmation");
        }

        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> CheckAccount(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Json(new { exists = false });

            var entradaDigitos = new string(input.Where(char.IsDigit).ToArray());

            var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                   u.CPF == entradaDigitos
                || u.PhoneNumber == entradaDigitos
                || u.Email == input);

            return Json(new { exists = user != null, email = user?.Email ?? input });
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View();

        [HttpGet, AllowAnonymous]
        public IActionResult ResetPassword(string email, string token)
            => View(new ResetPasswordViewModel { Email = email, Token = token });

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null)
                return RedirectToAction("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(user, vm.Token, vm.Password);
            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation");

            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);

            return View(vm);
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ResetPasswordConfirmation() => View();

        // =========================
        // HELPERS
        // =========================
        private async Task<ApplicationUser?> EncontrarUsuarioPorCredencial(string entradaDigitos, string entradaOriginal)
        {
            if (!string.IsNullOrWhiteSpace(entradaDigitos) && entradaDigitos.Length == 11)
            {
                var userCpf = await _userManager.Users.FirstOrDefaultAsync(u => u.CPF == entradaDigitos);
                if (userCpf != null) return userCpf;
            }

            if (!string.IsNullOrWhiteSpace(entradaDigitos) && entradaDigitos.Length >= 10)
            {
                var userPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == entradaDigitos);
                if (userPhone != null) return userPhone;
            }

            if (IsValidEmail(entradaOriginal))
            {
                var userEmail = await _userManager.Users.FirstOrDefaultAsync(u =>
                    u.Email != null && u.Email.Equals(entradaOriginal, StringComparison.OrdinalIgnoreCase));
                if (userEmail != null) return userEmail;
            }

            return await _userManager.FindByNameAsync(entradaOriginal);
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