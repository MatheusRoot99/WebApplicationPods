using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApplicationPods.Data;             // <= adicione
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProdutoRepository _produtoRepository;
        private readonly BancoContext _context;   // <= injeta o contexto

        public HomeController(IProdutoRepository produtoRepository, BancoContext context)
        {
            _produtoRepository = produtoRepository;
            _context = context;
        }

        public IActionResult Index(FiltrosModel filtros)
        {
            // Carrega dados da loja (pega a primeira/única config)
            var loja = _context.LojaConfigs
                .AsNoTracking()
                .FirstOrDefault();

            var viewModel = new ProdutoListagemViewModel
            {
                Produtos = _produtoRepository.FiltrarProdutos(filtros),

                Filtros = new FiltrosModel
                {
                    // Opções dos dropdowns
                    CategoriasDisponiveis = _produtoRepository.ObterCategoriasDistintas(),
                    SaboresDisponiveis = _produtoRepository.ObterSaboresDistintos(),
                    CoresDisponiveis = _produtoRepository.ObterCoresDistintas(),

                    // Seleções atuais
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

                // <<< A loja vai no VM principal (não dentro de Filtros) >>>
                Loja = loja   // ou NomeLoja = loja, se seu VM usar esse nome
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
