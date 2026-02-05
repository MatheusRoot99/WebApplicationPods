using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
                // se autenticado mas sem role -> faz logout e mostra tela de login
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var isAdmin = roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
                    var isLojista = roles.Any(r => r.Equals("Lojista", StringComparison.OrdinalIgnoreCase));

                    if (!isAdmin && !isLojista)
                    {
                        await _signInManager.SignOutAsync();
                        HttpContext.Session.Clear();
                        Response.Cookies.Delete("Pods.Auth", new CookieOptions { Domain = ".lvh.me", Path = "/" });
                        Response.Cookies.Delete("SitePods.Session", new CookieOptions { Domain = ".lvh.me", Path = "/" });

                        TempData["Erro"] = "Sua sessão anterior não tem permissão para este portal. Faça login novamente.";
                        ViewData["ReturnUrl"] = returnUrl;
                        return View(new LoginViewModel { ReturnUrl = returnUrl });
                    }
                }

                return RedirectToAppropriatePage();
            }

            // (resto do seu código permanece)
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

                // ✅ Obtém roles do usuário
                var roles = await _userManager.GetRolesAsync(user);
                var isAdmin = roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
                var isLojista = roles.Any(r => string.Equals(r, "Lojista", StringComparison.OrdinalIgnoreCase));

                // ✅ Bloqueia login se não tiver perfil (evita logar e cair em AcessoNegado)
                if (!isAdmin && !isLojista)
                {
                    await _signInManager.SignOutAsync();
                    HttpContext.Session.Clear();

                    ModelState.AddModelError(string.Empty, "Seu usuário está sem perfil de acesso (Admin/Lojista). Fale com o administrador.");
                    return View(vm);
                }

                var currentHost = (Request.Host.Host ?? "").ToLowerInvariant();
                var isAdminHost = currentHost.StartsWith("admin.");
                var isPainelHost = currentHost.StartsWith("painel.");

                // ✅ Admin logando no painel.*
                if (isAdmin && isPainelHost)
                {
                    var targetUrl = BuildSubdomainUrl("admin", FixReturnUrlForRole(returnUrl, isAdmin: true, isLojista: false) ?? "/Admin/Lojistas");
                    return Redirect(targetUrl);
                }

                // ✅ Lojista logando no admin.*
                if (isLojista && isAdminHost)
                {
                    var targetUrl = BuildSubdomainUrl("painel", FixReturnUrlForRole(returnUrl, isAdmin: false, isLojista: true) ?? "/PainelLojista/Dashboard");
                    return Redirect(targetUrl);
                }

                // ✅ Se não está em admin.* nem painel.*, manda para portal correto baseado na role
                if (!isAdminHost && !isPainelHost)
                {
                    if (isAdmin)
                        return Redirect(BuildSubdomainUrl("admin", FixReturnUrlForRole(returnUrl, isAdmin: true, isLojista: false) ?? "/Admin/Lojistas"));

                    return Redirect(BuildSubdomainUrl("painel", FixReturnUrlForRole(returnUrl, isAdmin: false, isLojista: true) ?? "/PainelLojista/Dashboard"));
                }

                // ✅ Está no host correto: respeita returnUrl se for local e compatível
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    if (IsReturnUrlCompatibleWithRole(returnUrl, isAdmin, isLojista))
                        return Redirect(returnUrl);
                }

                // ✅ fallback padrão
                if (isAdmin) return Redirect("/Admin/Lojistas");
                return Redirect("/PainelLojista/Dashboard");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no login: {ex.Message}");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro durante o login. Tente novamente.");
                return View(vm);
            }
        }

        private static string? FixReturnUrlForRole(string? returnUrl, bool isAdmin, bool isLojista)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) return null;
            if (!returnUrl.StartsWith("/", StringComparison.Ordinal)) return null;

            if (isAdmin)
            {
                // Admin só deve cair em /Admin/*
                if (returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                    return returnUrl;
                return null;
            }

            if (isLojista)
            {
                // Lojista só deve cair em /PainelLojista/*
                if (returnUrl.StartsWith("/PainelLojista", StringComparison.OrdinalIgnoreCase) ||
                    returnUrl.StartsWith("/painel", StringComparison.OrdinalIgnoreCase))
                    return returnUrl;

                return null;
            }

            return null;
        }

        private static bool IsReturnUrlCompatibleWithRole(string returnUrl, bool isAdmin, bool isLojista)
        {
            if (isAdmin && returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (isLojista && (returnUrl.StartsWith("/PainelLojista", StringComparison.OrdinalIgnoreCase) ||
                              returnUrl.StartsWith("/painel", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        private IActionResult RedirectToAppropriatePage()
        {
            var isAdmin = User.IsInRole("Admin");
            var isLojista = User.IsInRole("Lojista");

            var currentHost = (Request.Host.Host ?? "").ToLowerInvariant();
            var isAdminHost = currentHost.StartsWith("admin.");
            var isPainelHost = currentHost.StartsWith("painel.");

            // Garante que está no host certo
            if (isAdmin && !isAdminHost)
                return Redirect(BuildSubdomainUrl("admin", "/Admin/Lojistas"));

            if (isLojista && !isPainelHost)
                return Redirect(BuildSubdomainUrl("painel", "/PainelLojista/Dashboard"));

            // ✅ Rotas corretas (PainelLojista é prefixo/area)
            if (isAdmin) return Redirect("/Admin/Lojistas");
            if (isLojista) return Redirect("/PainelLojista/Dashboard");

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

            // Deleta sem domain (host atual)
            Response.Cookies.Delete("Pods.Auth");
            Response.Cookies.Delete("Pods.AntiForgery");
            Response.Cookies.Delete("SitePods.Session");

            // Deleta com domain compartilhado
            Response.Cookies.Delete("Pods.Auth", new CookieOptions { Domain = ".lvh.me", Path = "/" });
            Response.Cookies.Delete("Pods.AntiForgery", new CookieOptions { Domain = ".lvh.me", Path = "/" });
            Response.Cookies.Delete("SitePods.Session", new CookieOptions { Domain = ".lvh.me", Path = "/" });

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

        private string BuildSubdomainUrl(string subdomain, string path)
        {
            var scheme = Request.Scheme;
            var hostOnly = (Request.Host.Host ?? "localhost").Trim().ToLowerInvariant();
            var port = Request.Host.Port;

            var baseDomain = ExtractBaseDomain(hostOnly);

            var finalHost = $"{subdomain}.{baseDomain}";
            var finalPath = string.IsNullOrWhiteSpace(path) ? "/" : (path.StartsWith("/") ? path : "/" + path);

            return port.HasValue
                ? $"{scheme}://{finalHost}:{port.Value}{finalPath}"
                : $"{scheme}://{finalHost}{finalPath}";
        }

        private static string ExtractBaseDomain(string host)
        {
            if (host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0")
                return "lvh.me";

            if (host == "lvh.me" || host.EndsWith(".lvh.me"))
                return "lvh.me";

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return host;

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

            return $"{secondLast}.{last}";
        }
    }
}
