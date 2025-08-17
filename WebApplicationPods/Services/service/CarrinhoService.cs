

using WebApplicationPods.Helper;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class CarrinhoService : ICarrinhoService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string _sessionKey = "Carrinho";

        public CarrinhoService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public CarrinhoModel ObterCarrinho()
        {
            var session = _httpContextAccessor.HttpContext.Session;
            var carrinho = session.GetObject<CarrinhoModel>(_sessionKey);

            if (carrinho == null)
            {
                carrinho = new CarrinhoModel();
                session.SetObject(_sessionKey, carrinho);
            }

            return carrinho;
        }

        public void AdicionarItem(ProdutoModel produto, int quantidade, string observacoes)
        {
            var carrinho = ObterCarrinho();
            var itemExistente = carrinho.Itens.FirstOrDefault(i => i.Produto.Id == produto.Id);

            if (itemExistente != null)
            {
                itemExistente.Quantidade += quantidade;
                if (!string.IsNullOrEmpty(observacoes))
                {
                    itemExistente.Observacoes = observacoes;
                }
            }
            else
            {
                carrinho.Itens.Add(new CarrinhoItemViewModel
                {
                    Produto = produto,
                    Quantidade = quantidade,
                    PrecoUnitario = produto.Preco,
                    Observacoes = observacoes
                });
            }

            SalvarCarrinho(carrinho);
        }

        public void RemoverItem(int produtoId)
        {
            var carrinho = ObterCarrinho();
            var itemExistente = carrinho.Itens.FirstOrDefault(i => i.Produto.Id == produtoId);

            if (itemExistente != null)
            {
                carrinho.Itens.Remove(itemExistente);
                SalvarCarrinho(carrinho);
            }
        }

        public void LimparCarrinho()
        {
            var carrinho = ObterCarrinho();
            carrinho.Itens.Clear();
            SalvarCarrinho(carrinho);
        }

        public decimal ObterTotal()
        {
            var carrinho = ObterCarrinho();
            return carrinho.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        }

        public int ObterQuantidadeTotalItens()
        {
            var carrinho = ObterCarrinho();
            return carrinho.Itens.Sum(i => i.Quantidade);
        }

        private void SalvarCarrinho(CarrinhoModel carrinho)
        {
            _httpContextAccessor.HttpContext.Session.SetObject(_sessionKey, carrinho);
        }

        
    }
}
