using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SitePodsInicial.Models;
using SitePodsInicial.Repository.Interface;
using WebApplicationPods.Repository.Interface;

namespace SitePodsInicial.Controllers
{
    public class CarrinhoController : Controller
    {
        private readonly ICarrinhoRepository _carrinhoRepository;
        private readonly IProdutoRepository _produtoRepository;

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

                // Verifica estoque disponível
                if (!ValidarEstoque(produto, quantidade, sabor, out string mensagemErro))
                {
                    TempData["Erro"] = mensagemErro;
                    return RedirectToAction("Detalhes", "Produto", new { id = produtoId });
                }

                _carrinhoRepository.AdicionarItem(produto, quantidade, sabor, observacoes);

                TempData["Sucesso"] = $"{produto.Nome} adicionado ao carrinho!";
                return buyNow ? RedirectToAction("Resumo") : RedirectToAction("Index");
            }
            catch
            {
                TempData["Erro"] = "Ocorreu um erro ao adicionar o produto ao carrinho.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult AtualizarItem(int produtoId, int quantidade, string sabor = null)
        {
            try
            {
                if (quantidade <= 0)
                {
                    TempData["Erro"] = "Quantidade deve ser maior que zero!";
                    return RedirectToAction("Index");
                }

                var produto = _produtoRepository.ObterPorId(produtoId);
                if (produto == null)
                {
                    TempData["Erro"] = "Produto não encontrado!";
                    return RedirectToAction("Index");
                }

                if (!ValidarEstoque(produto, quantidade, sabor, out string mensagemErro))
                {
                    TempData["Erro"] = mensagemErro;
                    return RedirectToAction("Index");
                }

                var carrinho = _carrinhoRepository.ObterCarrinho();
                var item = carrinho.Itens.FirstOrDefault(i => i.Produto.Id == produtoId && i.Sabor == sabor);

                if (item != null)
                {
                    item.Quantidade = quantidade;
                    _carrinhoRepository.SalvarCarrinho(carrinho);

                    TempData["Sucesso"] = $"Quantidade de {produto.Nome} atualizada para {quantidade}.";
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
        public IActionResult RemoverItem(int produtoId, string sabor = null)
        {
            try
            {
                var carrinho = _carrinhoRepository.ObterCarrinho();
                var item = carrinho.Itens.FirstOrDefault(i => i.Produto.Id == produtoId && i.Sabor == sabor);

                if (item != null)
                {
                    carrinho.Itens.Remove(item);
                    _carrinhoRepository.SalvarCarrinho(carrinho);

                    TempData["Sucesso"] = $"{item.Produto.Nome} removido do carrinho.";
                }
                else
                {
                    TempData["Erro"] = "Item não encontrado no carrinho!";
                }
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao remover item: {ex.Message}";
            }

            return RedirectToAction("Index");
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

        // 🔹 Método centralizado para validação de estoque
        private bool ValidarEstoque(ProdutoModel produto, int quantidade, string sabor, out string mensagemErro)
        {
            mensagemErro = string.Empty;

            if (produto.SaboresQuantidadesList?.Any() == true && !string.IsNullOrEmpty(sabor))
            {
                var saborSelecionado = produto.SaboresQuantidadesList.FirstOrDefault(s => s.Sabor == sabor);
                if (saborSelecionado == null)
                {
                    mensagemErro = "Sabor selecionado inválido.";
                    return false;
                }

                if (saborSelecionado.Quantidade < quantidade)
                {
                    mensagemErro = $"Estoque insuficiente para o sabor {sabor}. Disponível: {saborSelecionado.Quantidade}";
                    return false;
                }
            }
            else if (produto.Estoque < quantidade)
            {
                mensagemErro = $"Estoque insuficiente. Disponível: {produto.Estoque}";
                return false;
            }

            return true;
        }
    }
}
