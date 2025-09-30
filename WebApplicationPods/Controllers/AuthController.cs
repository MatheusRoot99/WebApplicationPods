using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services;

namespace WebApplicationPods.Controllers
{
    public class AuthController : Controller
    {
        private readonly BancoContext _context;
        private readonly ICarrinhoRepository _carrinhoRepository;
        private readonly IClienteRememberService _remember;

        // ===== Helpers =====
        private static string SoDigitos(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

        private static string FormataTelBR(string tel)
        {
            var d = SoDigitos(tel);
            if (d.Length == 11) return $"({d[..2]}) {d.Substring(2, 5)}-{d.Substring(7)}";
            if (d.Length == 10) return $"({d[..2]}) {d.Substring(2, 4)}-{d.Substring(6)}";
            return tel ?? "";
        }

        private static int Idade(DateTime nascimento)
        {
            var hoje = DateTime.Today;
            var idade = hoje.Year - nascimento.Year;
            if (nascimento.Date > hoje.AddYears(-idade)) idade--;
            return idade;
        }

        private static bool CpfValido(string? cpf)
        {
            var d = SoDigitos(cpf);
            if (string.IsNullOrWhiteSpace(d) || d.Length != 11) return false;
            if (d.Distinct().Count() == 1) return false; // evita 000... / 111...

            int Calc(string src, int[] mult)
            {
                var soma = 0;
                for (int i = 0; i < mult.Length; i++)
                    soma += (src[i] - '0') * mult[i];
                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }

            var d1 = Calc(d, new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 });
            var d2 = Calc(d, new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 });
            return (d[9] - '0') == d1 && (d[10] - '0') == d2;
        }

        public AuthController(
            BancoContext context,
            ICarrinhoRepository carrinhoRepository,
            IClienteRememberService remember)
        {
            _context = context;
            _carrinhoRepository = carrinhoRepository;
            _remember = remember;
        }

        // ============================== LOGIN ==============================

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // 1) se já tem sessão, redireciona direto
            var telSess = HttpContext.Session.GetString("ClienteTelefone");
            if (!string.IsNullOrWhiteSpace(telSess))
            {
                return Redirect(returnUrl ?? Url.Action("Index", "Home")!);
            }

            // 2) tenta cookie lembrado
            var vm = new AuthLoginViewModel { ReturnUrl = returnUrl };
            if (_remember.TryGetFromCookie(Request, out var telCookie, out var nomeCookie))
            {
                var cliente = _context.Clientes.AsNoTracking().FirstOrDefault(c => c.Telefone == telCookie);
                if (cliente != null)
                {
                    vm.Telefone = telCookie;
                    vm.ClienteNome = cliente.Nome ?? nomeCookie;
                    // mostra bottom-sheet prontinho
                    return View(vm);
                }
                // cookie ficou “stale” -> limpa
                _remember.ClearCookie(Response);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(AuthLoginViewModel vm)
        {
            var tel = SoDigitos(vm.Telefone);
            if (string.IsNullOrWhiteSpace(tel) || tel.Length < 11)
            {
                ModelState.AddModelError(nameof(vm.Telefone), "Telefone inválido");
                vm.Telefone = tel;
                return View(vm);
            }

            // se veio para confirmar (caso você ainda use esse fluxo em outro lugar)
            if (vm.Confirmar)
            {
                HttpContext.Session.SetString("ClienteTelefone", tel);
                HttpContext.Session.SetString("ClienteConfirmado", "1");
                _remember.SetCookie(Response, tel, vm.ClienteNome, TimeSpan.FromDays(90));
                var returnUrl = vm.ReturnUrl ?? TempData["ReturnUrl"]?.ToString() ?? Url.Action("Index", "Home")!;
                return Redirect(returnUrl);
            }

            var cliente = _context.Clientes
                                  .Include(c => c.Enderecos)
                                  .FirstOrDefault(c => c.Telefone == tel);

            if (cliente == null)
            {
                return RedirectToAction("CadastroRapido", new { telefone = tel, returnUrl = vm.ReturnUrl });
            }

            // loga direto
            HttpContext.Session.SetString("ClienteTelefone", tel);
            HttpContext.Session.SetString("ClienteConfirmado", "1");
            _remember.SetCookie(Response, tel, cliente.Nome, TimeSpan.FromDays(90));

            var dest = vm.ReturnUrl ?? TempData["ReturnUrl"]?.ToString() ?? Url.Action("Resumo", "Carrinho")!;
            return Redirect(dest);
        }

        // ======================= CADASTRO RÁPIDO ==========================

        [HttpGet]
        public IActionResult CadastroRapido(string telefone, string? returnUrl = null)
        {
            telefone = SoDigitos(telefone);
            if (string.IsNullOrWhiteSpace(telefone) || telefone.Length < 11)
            {
                TempData["Erro"] = "Telefone inválido";
                return RedirectToAction("Login");
            }

            var vm = new CadastroRapidoViewModel { Telefone = telefone };
            ViewBag.ReturnUrl = returnUrl;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CadastroRapido(CadastroRapidoViewModel model, string? returnUrl)
        {
            // ====== validações adicionais (18+ e CPF) ======
            // normaliza CPF para somente dígitos
            model.CPF = SoDigitos(model.CPF);

            if (!model.DataNascimento.HasValue)
                ModelState.AddModelError(nameof(model.DataNascimento), "A data de nascimento é obrigatória.");
            else
            {
                if (model.DataNascimento.Value.Date > DateTime.Today)
                    ModelState.AddModelError(nameof(model.DataNascimento), "Data de nascimento inválida.");
                else if (Idade(model.DataNascimento.Value) < 18)
                    ModelState.AddModelError(nameof(model.DataNascimento), "É necessário ser maior de 18 anos.");
            }

            if (!CpfValido(model.CPF))
                ModelState.AddModelError(nameof(model.CPF), "CPF inválido.");

            if (!ModelState.IsValid) return View(model);

            // duplicidade de telefone
            if (_context.Clientes.Any(c => c.Telefone == model.Telefone))
            {
                ModelState.AddModelError(nameof(model.Telefone), "Telefone já cadastrado");
                return View(model);
            }

            // duplicidade de CPF (sempre compare com a propriedade mapeada 'Cpf')
            var cpfDigitos = model.CPF; // já normalizado acima
            var cpfEmUso = _context.Clientes.AsNoTracking().Any(c => c.Cpf == cpfDigitos);
            if (cpfEmUso)
            {
                ModelState.AddModelError(nameof(model.CPF), "Já existe cadastro com este CPF.");
                return View(model);
            }

            // monta cliente + endereço principal
            var cliente = new ClienteModel
            {
                Telefone = model.Telefone,
                Nome = model.Nome?.Trim() ?? string.Empty,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                Cpf = cpfDigitos, // <- use sempre a propriedade mapeada
                DataNascimento = model.DataNascimento!.Value,
                DataCadastro = DateTime.Now,
                Enderecos = new List<EnderecoModel>()
            };

            var endereco = new EnderecoModel
            {
                CEP = model.CEP,
                Logradouro = model.Logradouro,
                Numero = model.Numero,
                Complemento = model.Complemento,
                Bairro = model.Bairro,
                Cidade = model.Cidade,
                Estado = (model.Estado ?? "").Trim().ToUpper(),
                Principal = true,
                Cliente = cliente
            };
            cliente.Enderecos.Add(endereco);

            try
            {
                _context.Clientes.Add(cliente);
                _context.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // se houve corrida, índice único do BD pode disparar aqui
                ModelState.AddModelError(nameof(model.CPF), "Já existe cadastro com este CPF.");
                return View(model);
            }

            // login + cookie lembrado
            HttpContext.Session.SetString("ClienteTelefone", cliente.Telefone);
            HttpContext.Session.SetString("ClienteConfirmado", "1");
            _remember.SetCookie(Response, cliente.Telefone, cliente.Nome, TimeSpan.FromDays(90));

            return Redirect(string.IsNullOrWhiteSpace(returnUrl)
                ? Url.Action("Resumo", "Carrinho")!
                : returnUrl);
        }

        // ============================ TROCAR ==============================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TrocarUsuario()
        {
            HttpContext.Session.Remove("ClienteTelefone");
            HttpContext.Session.Remove("ClienteConfirmado");
            _remember.ClearCookie(Response);
            return RedirectToAction("Login");
        }

        // ===================== EDITAR INFORMAÇÕES ========================

        [HttpGet]
        public IActionResult Editar(string? returnUrl = null)
        {
            // tenta sessão e, se faltou, cookie lembrado
            var telSess = HttpContext.Session.GetString("ClienteTelefone");
            if (string.IsNullOrWhiteSpace(telSess) &&
                _remember.TryGetFromCookie(Request, out var telCookie, out _))
            {
                telSess = telCookie;
                HttpContext.Session.SetString("ClienteTelefone", telCookie); // re-hidrata sessão
            }

            if (string.IsNullOrWhiteSpace(telSess))
                return RedirectToAction("Login", new { returnUrl });

            var cliente = _context.Clientes.AsNoTracking().FirstOrDefault(c => c.Telefone == telSess);
            if (cliente == null)
                return RedirectToAction("Login", new { returnUrl });

            var vm = new EditarInfoViewModel
            {
                Nome = cliente.Nome ?? "",
                Telefone = FormataTelBR(cliente.Telefone ?? ""),
                Email = cliente.Email,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action("Resumo", "Carrinho")
                    : returnUrl
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(EditarInfoViewModel vm)
        {
            var telNovo = SoDigitos(vm.Telefone);
            if (string.IsNullOrWhiteSpace(telNovo) || telNovo.Length < 11)
                ModelState.AddModelError(nameof(vm.Telefone), "Telefone inválido (use DDD + 9 dígitos).");

            if (!ModelState.IsValid)
                return View(vm);

            // quem está logado agora?
            var telAtual = HttpContext.Session.GetString("ClienteTelefone");
            if (string.IsNullOrWhiteSpace(telAtual))
                return RedirectToAction("Login", new { returnUrl = vm.ReturnUrl });

            var cliente = _context.Clientes.FirstOrDefault(c => c.Telefone == telAtual);
            if (cliente == null)
                return RedirectToAction("Login", new { returnUrl = vm.ReturnUrl });

            // impedimos duplicidade de telefone em outro cliente
            var telEmUso = _context.Clientes.Any(c => c.Telefone == telNovo && c.Id != cliente.Id);
            if (telEmUso)
            {
                ModelState.AddModelError(nameof(vm.Telefone), "Este telefone já está em uso.");
                return View(vm);
            }

            // aplica alterações
            cliente.Nome = vm.Nome?.Trim();
            cliente.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email!.Trim();
            cliente.Telefone = telNovo;

            _context.SaveChanges();

            // atualiza sessão + cookie se o telefone trocou
            HttpContext.Session.SetString("ClienteTelefone", telNovo);
            _remember.SetCookie(Response, telNovo, cliente.Nome, TimeSpan.FromDays(90));

            TempData["Sucesso"] = "Informações atualizadas com sucesso!";
            return Redirect(string.IsNullOrWhiteSpace(vm.ReturnUrl)
                ? Url.Action("Resumo", "Carrinho")!
                : vm.ReturnUrl!);
        }
    }
}
