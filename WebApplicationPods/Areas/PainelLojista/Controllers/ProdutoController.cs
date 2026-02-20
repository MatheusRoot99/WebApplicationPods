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
        public IActionResult Detalhes(int id)
        {
            var produto = _context.Produtos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id);

            if (produto == null) return NotFound();

            return View(produto);
        }
    }
}