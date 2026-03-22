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
        private readonly ICarrinhoRepository _carrinhoRepository;

        public HomeController(
            IProdutoRepository produtoRepository,
            BancoContext context,
            ICarrinhoRepository carrinhoRepository)
        {
            _produtoRepository = produtoRepository;
            _context = context;
            _carrinhoRepository = carrinhoRepository;
        }

        public IActionResult Landing()
        {
            return View();
        }

        public IActionResult Index(FiltrosModel filtros)
        {
            filtros ??= new FiltrosModel();

            filtros.Categoria = string.IsNullOrWhiteSpace(filtros.Categoria) ? null : filtros.Categoria;
            filtros.Sabor = string.IsNullOrWhiteSpace(filtros.Sabor) ? null : filtros.Sabor;
            filtros.Cor = string.IsNullOrWhiteSpace(filtros.Cor) ? null : filtros.Cor;
            filtros.Termo = string.IsNullOrWhiteSpace(filtros.Termo) ? null : filtros.Termo;
            filtros.OrdenarPor = string.IsNullOrWhiteSpace(filtros.OrdenarPor) ? "Populares" : filtros.OrdenarPor;

            var loja = _context.LojaConfigs
                .AsNoTracking()
                .FirstOrDefault();

            var produtos = _produtoRepository.FiltrarProdutos(filtros);

            var carrinho = _carrinhoRepository.ObterCarrinho();
            var produtosNoCarrinho = carrinho?.Itens?
                .Where(i => i.ProdutoId > 0)
                .Select(i => i.ProdutoId)
                .Distinct()
                .ToList() ?? new List<int>();

            var viewModel = new ProdutoListagemViewModel
            {
                Produtos = produtos,

                Filtros = new FiltrosModel
                {
                    CategoriasDisponiveis = _produtoRepository.ObterCategoriasDistintas(),
                    SaboresDisponiveis = _produtoRepository.ObterSaboresDistintos(),
                    CoresDisponiveis = _produtoRepository.ObterCoresDistintas(),

                    Categoria = filtros.Categoria,
                    Sabor = filtros.Sabor,
                    Cor = filtros.Cor,
                    Termo = filtros.Termo,
                    PrecoMin = filtros.PrecoMin,
                    PrecoMax = filtros.PrecoMax,
                    AvaliacaoMin = filtros.AvaliacaoMin,
                    ApenasPromocoes = filtros.ApenasPromocoes,
                    ApenasEstoque = filtros.ApenasEstoque,
                    OrdenarPor = filtros.OrdenarPor
                },

                Loja = loja,
                ProdutosNoCarrinho = produtosNoCarrinho
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