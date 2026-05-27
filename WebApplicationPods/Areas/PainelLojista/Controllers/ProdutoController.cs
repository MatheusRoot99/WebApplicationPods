using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("PainelLojista")]
    [Authorize(Roles = "Lojista,Admin")]
    public class ProdutoController : Controller
    {
        private readonly BancoContext _context;
        private readonly ICurrentLojaService _currentLoja;

        public ProdutoController(BancoContext context, ICurrentLojaService currentLoja)
        {
            _context = context;
            _currentLoja = currentLoja;
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
            if (_currentLoja.LojaId is not int lojaId || lojaId <= 0)
                return Forbid();

            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.LojaId == lojaId);

            if (produto == null)
                return NotFound();

            return View(produto);
        }
    }
}