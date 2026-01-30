using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
            => View(new LoginViewModel { ReturnUrl = returnUrl });

        // POST: /Conta/Login
        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
        {
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

                if (result.Succeeded)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var isAdmin = roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
                    var isLojista = roles.Any(r => string.Equals(r, "Lojista", StringComparison.OrdinalIgnoreCase));

                    // 1) Se tem returnUrl local, respeita (ajustando host se for admin/lojista)
                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        if (isAdmin)
                            return Redirect(BuildSubdomainUrl("admin", returnUrl));

                        if (isLojista)
                            return Redirect(BuildSubdomainUrl("painel", returnUrl));

                        return Redirect(returnUrl);
                    }

                    // 2) Sem returnUrl: manda pro portal correto
                    if (isAdmin)
                        // 👉 já abre direto a listagem de lojistas
                        return Redirect(BuildSubdomainUrl("admin", "/Admin/Lojistas"));

                    if (isLojista)
                        return Redirect(BuildSubdomainUrl("painel", "/Painel/Dashboard"));


                    // 3) Cliente / outros
                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Usuário temporariamente bloqueado. Tente novamente mais tarde.");
                    return View(vm);
                }

                ModelState.AddModelError(string.Empty, "Senha inválida.");
                return View(vm);
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Ocorreu um erro durante o login. Tente novamente.");
                return View(vm);
            }
        }


        private async Task<ApplicationUser?> EncontrarUsuarioPorCredencial(string entradaDigitos, string entradaOriginal)
        {
            if (!string.IsNullOrWhiteSpace(entradaDigitos))
            {
                var userCpf = await _userManager.Users.FirstOrDefaultAsync(u => u.CPF == entradaDigitos);
                if (userCpf != null) return userCpf;

                var userPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == entradaDigitos);
                if (userPhone != null) return userPhone;
            }

            if (IsValidEmail(entradaOriginal))
            {
                var userEmail = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == entradaOriginal);
                if (userEmail != null) return userEmail;
            }

            return null;
        }

        private static string LimparDigitos(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return new string(input.Where(char.IsDigit).ToArray());
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

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

        // Ex: http://loja-1.lvh.me:44320 -> http://admin.lvh.me:44320/Admin/Dashboard
        private string BuildSubdomainUrl(string subdomain, string path)
        {
            var scheme = Request.Scheme;

            var hostOnly = (Request.Host.Host ?? "localhost").Trim().ToLowerInvariant();
            var port = Request.Host.Port;

            var baseDomain = GetBaseDomain(hostOnly);

            var finalHost = $"{subdomain}.{baseDomain}";
            var finalPath = string.IsNullOrWhiteSpace(path) ? "/" : (path.StartsWith("/") ? path : "/" + path);

            return port.HasValue
                ? $"{scheme}://{finalHost}:{port.Value}{finalPath}"
                : $"{scheme}://{finalHost}{finalPath}";
        }

        private static string GetBaseDomain(string host)
        {
            // Dev: se estiver em localhost/ip/0.0.0.0, força lvh.me
            if (host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0")
                return "lvh.me";

            // lvh.me já é base
            if (host == "lvh.me" || host.EndsWith(".lvh.me"))
                return "lvh.me";

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return host;

            // Heurística pro Brasil (ex: minhaempresa.com.br)
            var last = parts[^1];
            var secondLast = parts[^2];
            var thirdLast = parts.Length >= 3 ? parts[^3] : null;

            var brSecondLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "com","net","org","gov","edu"
            };

            if (last.Equals("br", StringComparison.OrdinalIgnoreCase) &&
                thirdLast != null &&
                brSecondLevel.Contains(secondLast))
            {
                return $"{thirdLast}.{secondLast}.{last}";
            }

            // padrão: 2 últimos
            return $"{secondLast}.{last}";
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null)
                return View("ForgotPasswordConfirmation");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action("ResetPassword", "Conta", new { email = vm.Email, token }, Request.Scheme);

            await _emailSender.SendAsync(vm.Email,
                "Redefinição de Senha",
                $"<p>Olá {user.Nome},</p><p>Para redefinir sua senha, clique no link abaixo:</p><p><a href=\"{link}\">Redefinir Senha</a></p>");

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
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null) return RedirectToAction("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(user, vm.Token, vm.Password);
            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation");

            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);

            return View(vm);
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ResetPasswordConfirmation() => View();

        [AllowAnonymous]
        public async Task<IActionResult> DebugUser()
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.CPF == "02121225170");

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
