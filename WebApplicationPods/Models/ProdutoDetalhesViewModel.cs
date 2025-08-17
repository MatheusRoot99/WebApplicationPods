using WebApplicationPods.Models;

namespace WebApplicationPods.Models
{
    public class ProdutoDetalhesViewModel
    {
        public ProdutoModel Produto { get; set; }
        public List<ProdutoModel.SaborQuantidade> SaboresDisponiveis { get; set; } 
        public List<ProdutoModel> ProdutosRelacionados { get; set; } = new List<ProdutoModel>();

        
    }
}
