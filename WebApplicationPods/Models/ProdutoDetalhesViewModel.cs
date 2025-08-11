using SitePodsInicial.Models;

namespace SitePodsInicial.Models
{
    public class ProdutoDetalhesViewModel
    {
        public ProdutoModel Produto { get; set; }
        public List<string> SaboresDisponiveis { get; set; } = new List<string>();
        public List<ProdutoModel> ProdutosRelacionados { get; set; } = new List<ProdutoModel>();

        
    }
}
