using SitePodsInicial.Models;

namespace SitePodsInicial.Models
{
    public class ProdutoListagemViewModel
    {
        public IEnumerable<ProdutoModel> Produtos { get; set; } // Mude para IEnumerable
        public FiltrosModel Filtros { get; set; }
    }
}
