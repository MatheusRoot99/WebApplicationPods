using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class AuthController : Controller
    {
        private readonly BancoContext _context;
        private readonly ICarrinhoRepository _carrinhoRepository;
       

        public AuthController(BancoContext context, ICarrinhoRepository carrinhoRepository)
        {
            _context = context;
            _carrinhoRepository = carrinhoRepository;
           
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
        public IActionResult CadastroRapido(string telefone, string returnUrl = null)
        {
            // Validação do telefone
            telefone = new string(telefone?.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(telefone) || telefone.Length < 11)
            {
                TempData["Erro"] = "Telefone inválido";
                return RedirectToAction("Login");
            }

            // Crie o ViewModel correto
            var viewModel = new CadastroRapidoViewModel
            {
                Telefone = telefone
            };

            ViewBag.ReturnUrl = returnUrl;
            return View(viewModel); // Certifique-se de passar o ViewModel correto
        }

        [HttpPost]
        public IActionResult CadastroRapido(CadastroRapidoViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Verifica se o telefone já existe
                    if (_context.Clientes.Any(c => c.Telefone == model.Telefone))
                    {
                        ModelState.AddModelError("Telefone", "Telefone já cadastrado");
                        return View(model);
                    }

                    // 1. Criar o cliente
                    var cliente = new ClienteModel
                    {
                        Telefone = model.Telefone,
                        Nome = model.Nome,
                        Email = model.Email,
                        DataCadastro = DateTime.Now,
                        Enderecos = new List<EnderecoModel>() // Inicializa a coleção
                    };

                    // 2. Criar o endereço principal
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
                        Cliente = cliente // Estabelece o relacionamento
                    };

                    // 3. Adicionar o endereço ao cliente
                    cliente.Enderecos.Add(endereco);

                    // 4. Salvar no banco de dados
                    _context.Clientes.Add(cliente);
                    _context.SaveChanges();

                    // 5. Login automático
                    HttpContext.Session.SetString("ClienteTelefone", cliente.Telefone);

                    // 6. Redirecionar
                    return RedirectToAction("Resumo", "Carrinho");
                }
                catch (Exception ex)
                {
                    // Logar o erro em produção
                    ModelState.AddModelError("", "Ocorreu um erro ao salvar seus dados. Por favor, tente novamente.");
                    return View(model);
                }
            }

            return View(model);
        }
    }
}
