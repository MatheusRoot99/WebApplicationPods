using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("PainelLojista")]
    public class ProdutoController : Controller
    {
        private readonly BancoContext _context;

        public ProdutoController(BancoContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Produto", new { area = "" });
        }

       
        [HttpGet]
        public IActionResult CriarPadrao()
        {
            return RedirectToAction("CriarPadrao", "Produto", new { area = "" });
        }

        [HttpGet]
        public IActionResult CriarBebida()
        {
            return RedirectToAction("CriarBebida", "Produto", new { area = "" });
        }

        [HttpGet]
        public IActionResult CriarPod()
        {
            return RedirectToAction("CriarPod", "Produto", new { area = "" });
        }

        [HttpGet]
        public IActionResult EditarSimples(int id)
        {
            return RedirectToAction("EditarSimples", "Produto", new { area = "", id });
        }

        [HttpGet]
        public IActionResult Excluir(int id)
        {
            return RedirectToAction("Excluir", "Produto", new { area = "", id });
        }

        [HttpGet]
        public async Task<IActionResult> Visualizar(int id)
        {
            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto == null) return NotFound();

            return View(produto); // Areas/PainelLojista/Views/Produto/Visualizar.cshtml
        }
    }
}