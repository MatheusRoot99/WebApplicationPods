using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;

namespace WebApplicationPods.Controllers
{
    public class AuthController : Controller
    {
        private readonly BancoContext _context;

        public AuthController(BancoContext context)
        {
            _context = context;
        }

        // GET: /Auth/Login (para validar por telefone)
        

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string telefone)
        {
            telefone = new string(telefone.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(telefone) || telefone.Length < 11)
            {
                ModelState.AddModelError("", "Telefone inválido");
                return View();
            }

            var cliente = _context.Clientes
                .Include(c => c.Enderecos)
                .FirstOrDefault(c => c.Telefone == telefone);

            if (cliente == null)
            {
                // Redireciona para cadastro com telefone pré-preenchido
                return RedirectToAction("CadastroRapido", new { telefone });
            }

            // Login (usando sessão)
            HttpContext.Session.SetString("ClienteTelefone", telefone);

            // Redireciona para o resumo ou URL original
            var returnUrl = TempData["ReturnUrl"]?.ToString() ?? Url.Action("Index", "Home");
            return Redirect(returnUrl);
        }

        [HttpGet]
        public IActionResult CadastroRapido(string telefone)
        {
            var model = new ClienteModel { Telefone = telefone };
            return View(model);
        }

        [HttpPost]
        public IActionResult CadastroRapido(ClienteModel model)
        {
            if (ModelState.IsValid)
            {
                // Verifica se já existe
                if (_context.Clientes.Any(c => c.Telefone == model.Telefone))
                {
                    ModelState.AddModelError("Telefone", "Telefone já cadastrado");
                    return View(model);
                }

                // Cadastro mínimo
                model.DataCadastro = DateTime.Now;
                _context.Clientes.Add(model);
                _context.SaveChanges();

                // Login automático
                HttpContext.Session.SetString("ClienteTelefone", model.Telefone);

                return RedirectToAction("Resumo", "Carrinho");
            }

            return View(model);
        }
    }
}
