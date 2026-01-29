using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProdutoRepository _produtoRepository;
        private readonly BancoContext _context;

        public HomeController(IProdutoRepository produtoRepository, BancoContext context)
        {
            _produtoRepository = produtoRepository;
            _context = context;
        }

        public IActionResult Landing()
        {
            return View();
        }

        public IActionResult Index(FiltrosModel filtros)
        {
            // Se estiver sem loja (sem subdomínio), o filtro global faz isso retornar null
            var loja = _context.LojaConfigs
                .AsNoTracking()
                .FirstOrDefault();

            if (loja == null)
                return RedirectToAction(nameof(Landing));

            var viewModel = new ProdutoListagemViewModel
            {
                Produtos = _produtoRepository.FiltrarProdutos(filtros),

                Filtros = new FiltrosModel
                {
                    CategoriasDisponiveis = _produtoRepository.ObterCategoriasDistintas(),
                    SaboresDisponiveis = _produtoRepository.ObterSaboresDistintos(),
                    CoresDisponiveis = _produtoRepository.ObterCoresDistintas(),

                    Categoria = filtros.Categoria,
                    Sabor = filtros.Sabor,
                    Cor = filtros.Cor,
                    PrecoMin = filtros.PrecoMin,
                    PrecoMax = filtros.PrecoMax,
                    AvaliacaoMin = filtros.AvaliacaoMin,
                    ApenasPromocoes = filtros.ApenasPromocoes,
                    ApenasEstoque = filtros.ApenasEstoque,
                    OrdenarPor = filtros.OrdenarPor
                },

                Loja = loja
            };

            return View(viewModel);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
