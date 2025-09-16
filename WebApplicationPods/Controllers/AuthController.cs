using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services;

public class AuthController : Controller
{
    private readonly BancoContext _context;
    private readonly ICarrinhoRepository _carrinhoRepository;
    private readonly IClienteRememberService _remember;
    // helper para normalizar e (opcional) formatar
    private static string SoDigitos(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());
    private static string FormataTelBR(string tel)
    {
        var d = SoDigitos(tel);
        if (d.Length == 11) return $"({d[..2]}) {d.Substring(2, 5)}-{d.Substring(7)}";
        if (d.Length == 10) return $"({d[..2]}) {d.Substring(2, 4)}-{d.Substring(6)}";
        return tel ?? "";
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
        var tel = new string((vm.Telefone ?? "").Where(char.IsDigit).ToArray());
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

        // >>> NOVO: loga direto, sem sheet no login
        HttpContext.Session.SetString("ClienteTelefone", tel);
        HttpContext.Session.SetString("ClienteConfirmado", "1");
        _remember.SetCookie(Response, tel, cliente.Nome, TimeSpan.FromDays(90));

        var dest = vm.ReturnUrl ?? TempData["ReturnUrl"]?.ToString() ?? Url.Action("Resumo", "Carrinho")!;
        return Redirect(dest);
    }


    [HttpGet]
    public IActionResult CadastroRapido(string telefone, string? returnUrl = null)
    {
        telefone = new string(telefone?.Where(char.IsDigit).ToArray());
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
    public IActionResult CadastroRapido(CadastroRapidoViewModel model, string? returnUrl)
    {
        if (!ModelState.IsValid) return View(model);

        if (_context.Clientes.Any(c => c.Telefone == model.Telefone))
        {
            ModelState.AddModelError("Telefone", "Telefone já cadastrado");
            return View(model);
        }

        var cliente = new ClienteModel
        {
            Telefone = model.Telefone,
            Nome = model.Nome,
            Email = model.Email,
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
            Estado = model.Estado,
            Principal = true,
            Cliente = cliente
        };
        cliente.Enderecos.Add(endereco);

        _context.Clientes.Add(cliente);
        _context.SaveChanges();

        // login + cookie lembrado
        HttpContext.Session.SetString("ClienteTelefone", cliente.Telefone);
        HttpContext.Session.SetString("ClienteConfirmado", "1"); // <- adiciona
        _remember.SetCookie(Response, cliente.Telefone, cliente.Nome, TimeSpan.FromDays(90));

        return Redirect(string.IsNullOrWhiteSpace(returnUrl)
            ? Url.Action("Resumo", "Carrinho")!
            : returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TrocarUsuario()
    {
        HttpContext.Session.Remove("ClienteTelefone");
        HttpContext.Session.Remove("ClienteConfirmado"); // <- adiciona
        _remember.ClearCookie(Response);
        return RedirectToAction("Login");
    }


    // GET: /Auth/Editar
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

    // POST: /Auth/Editar
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

