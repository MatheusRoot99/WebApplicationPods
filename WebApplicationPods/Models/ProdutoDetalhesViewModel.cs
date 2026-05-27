namespace WebApplicationPods.Models
{
    public class ProdutoDetalhesViewModel
    {
        public ProdutoModel Produto { get; set; } = new ProdutoModel();
        public List<ProdutoModel.SaborQuantidade> SaboresDisponiveis { get; set; } = new();
        public List<ProdutoModel> ProdutosRelacionados { get; set; } = new();
    }
}