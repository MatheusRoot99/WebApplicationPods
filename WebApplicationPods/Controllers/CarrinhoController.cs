using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SitePodsInicial.Models;
using SitePodsInicial.Repository.Interface;
using WebApplicationPods.Repository.Interface;

namespace SitePodsInicial.Controllers
{
    public class CarrinhoController : Controller
    {
        private readonly ICarrinhoRepository _carrinhoRepository;
        private readonly IProdutoRepository _produtoRepository; // seu repositório de produtos

        public CarrinhoController(ICarrinhoRepository carrinhoRepository, IProdutoRepository produtoRepository)
        {
            _carrinhoRepository = carrinhoRepository;
            _produtoRepository = produtoRepository;
        }

        public IActionResult Index()
        {
            var carrinho = _carrinhoRepository.ObterCarrinho();
            return View(carrinho);
        }

        [HttpPost]
        public IActionResult AdicionarItem(int produtoId, int quantidade, string sabor = null, string observacoes = null, bool buyNow = false)
        {
            try
            {
                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction("Index");
                }

                // Verifica estoque para produtos com sabores
                if (produto.SaboresQuantidadesList?.Any() == true)
                {
                    var saborSelecionado = produto.SaboresQuantidadesList.FirstOrDefault(s => s.Sabor == sabor);
                    if (saborSelecionado == null || saborSelecionado.Quantidade < quantidade)
                    {
                        TempData["Erro"] = $"Quantidade indisponível para o sabor {sabor}";
                        return RedirectToAction("Detalhes", "Produto", new { id = produtoId });
                    }
                }
                else if (produto.Estoque < quantidade) // Verifica estoque para produtos sem sabores
                {
                    TempData["Erro"] = $"Quantidade indisponível em estoque (disponível: {produto.Estoque})";
                    return RedirectToAction("Detalhes", "Produto", new { id = produtoId });
                }

                _carrinhoRepository.AdicionarItem(produto, quantidade, sabor, observacoes);

                if (buyNow)
                    return RedirectToAction("Resumo");

                TempData["Sucesso"] = $"{produto.Nome} adicionado ao carrinho!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                //ILogger.LogError(ex, "Erro ao adicionar item ao carrinho");
                TempData["Erro"] = "Ocorreu um erro ao adicionar o produto ao carrinho.";
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        public IActionResult AtualizarItem(int produtoId, int quantidade, string sabor = null)
        {
            try
            {
                // Verifica se a quantidade é válida
                if (quantidade <= 0)
                {
                    TempData["Erro"] = "Quantidade deve ser maior que zero!";
                    return RedirectToAction("Index");
                }

                // Obtém o produto para verificar estoque
                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction("Index");
                }

                // Verifica estoque para produtos com sabores
                if (produto.SaboresQuantidadesList?.Any() == true && !string.IsNullOrEmpty(sabor))
                {
                    var saborSelecionado = produto.SaboresQuantidadesList.FirstOrDefault(s => s.Sabor == sabor);
                    if (saborSelecionado == null || saborSelecionado.Quantidade < quantidade)
                    {
                        TempData["Erro"] = $"Quantidade indisponível para o sabor {sabor}";
                        return RedirectToAction("Index");
                    }
                }
                else if (produto.Estoque < quantidade) // Verifica estoque para produtos sem sabores
                {
                    TempData["Erro"] = $"Quantidade indisponível em estoque (disponível: {produto.Estoque})";
                    return RedirectToAction("Index");
                }

                // Atualiza o item no carrinho
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var item = carrinho.Itens.FirstOrDefault(i =>
                    i.Produto.Id == produtoId &&
                    i.Sabor == sabor);

                if (item != null)
                {
                    item.Quantidade = quantidade;
                    _carrinhoRepository.SalvarCarrinho(carrinho);
                    TempData["Sucesso"] = "Quantidade atualizada com sucesso!";
                }
                else
                {
                    TempData["Erro"] = "Item não encontrado no carrinho!";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao atualizar item: {ex.Message}";
                return RedirectToAction("Index");
            }
        }




        [HttpPost]
        public IActionResult FinalizarPedido()
        {
            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();

                if (!carrinho.Itens.Any())
                {
                    TempData["Erro"] = "Seu carrinho está vazio!";
                    return RedirectToAction("Index");
                }

                // Atualiza estoque para cada item
                foreach (var item in carrinho.Itens)
                {
                    var produto = _produtoRepository.ObterPorId(item.Produto.Id);

                    if (produto.SaboresQuantidadesList?.Any() == true && !string.IsNullOrEmpty(item.Sabor))
                    {
                        var sabor = produto.SaboresQuantidadesList.FirstOrDefault(s => s.Sabor == item.Sabor);
                        if (sabor != null)
                        {
                            sabor.Quantidade -= item.Quantidade;
                            produto.SaboresQuantidades = JsonConvert.SerializeObject(produto.SaboresQuantidadesList);
                        }
                    }
                    else
                    {
                        produto.Estoque -= item.Quantidade;
                    }

                    _produtoRepository.Atualizar(produto);
                }

                // TODO: Criar registro do pedido no banco de dados

                _carrinhoRepository.LimparCarrinho();

                TempData["MensagemSucesso"] = "Pedido realizado com sucesso!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao finalizar pedido: {ex.Message}";
                return RedirectToAction("Resumo");
            }
        }
    }
}
