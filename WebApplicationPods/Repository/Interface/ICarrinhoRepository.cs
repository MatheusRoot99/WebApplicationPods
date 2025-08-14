using SitePodsInicial.Models;

namespace WebApplicationPods.Repository.Interface
{
    public interface ICarrinhoRepository
    {
        CarrinhoModel ObterCarrinho();
        void SalvarCarrinho(CarrinhoModel carrinho);
        void AdicionarItem(ProdutoModel produto, int quantidade, string sabor = null, string observacoes = null);
        void AtualizarItem(ProdutoModel produto, int quantidade, string sabor = null);
        void RemoverItem(int produtoId, string sabor = null);
        void LimparCarrinho();
    }
}
