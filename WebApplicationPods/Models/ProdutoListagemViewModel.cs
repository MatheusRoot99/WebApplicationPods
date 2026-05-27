namespace WebApplicationPods.Models
{
    public class ProdutoListagemViewModel
    {
        public IEnumerable<ProdutoModel> Produtos { get; set; } = Enumerable.Empty<ProdutoModel>();
        public FiltrosModel Filtros { get; set; } = new FiltrosModel();

        public LojaConfig? Loja { get; set; }
        public List<int> ProdutosNoCarrinho { get; set; } = new();
    }
}