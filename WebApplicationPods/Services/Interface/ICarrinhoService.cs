using SitePodsInicial.Models;

namespace SitePodsInicial.Services.Interface
{
    public interface ICarrinhoService
    {
        CarrinhoModel ObterCarrinho();
        void AdicionarItem(ProdutoModel produto, int quantidade, string observacoes);
        void RemoverItem(int produtoId);
        void LimparCarrinho();
        decimal ObterTotal();
        int ObterQuantidadeTotalItens();
        //Task<CarrinhoModel> ObterCarrinhoPorUsuarioAsync(string usuarioId);
    }
}
