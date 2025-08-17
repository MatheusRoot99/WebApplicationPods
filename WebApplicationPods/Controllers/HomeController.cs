using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Models;

namespace WebApplicationPods.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProdutoRepository _produtoRepository;


        public HomeController(
           IProdutoRepository produtoRepository) 
        
        
        {
            
            _produtoRepository = produtoRepository;
        }
        public IActionResult Index(FiltrosModel filtros)
        {
            var viewModel = new ProdutoListagemViewModel
            {
                Produtos = _produtoRepository.FiltrarProdutos(filtros), // Agora compatível
                Filtros = new FiltrosModel
                {
                    // Preenche as listas para os dropdowns
                    CategoriasDisponiveis = _produtoRepository.ObterCategoriasDistintas(),
                    SaboresDisponiveis = _produtoRepository.ObterSaboresDistintos(),
                    CoresDisponiveis = _produtoRepository.ObterCoresDistintas(),
                    // Mantém os filtros selecionados
                    Categoria = filtros.Categoria,
                    Sabor = filtros.Sabor,
                    Cor = filtros.Cor,
                    PrecoMin = filtros.PrecoMin,
                    PrecoMax = filtros.PrecoMax,
                    AvaliacaoMin = filtros.AvaliacaoMin,
                    ApenasPromocoes = filtros.ApenasPromocoes,
                    ApenasEstoque = filtros.ApenasEstoque,
                    OrdenarPor = filtros.OrdenarPor
                }
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
