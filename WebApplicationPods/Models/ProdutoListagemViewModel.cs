using WebApplicationPods.Models;

namespace WebApplicationPods.Models
{
    public class ProdutoListagemViewModel
    {
        public IEnumerable<ProdutoModel> Produtos { get; set; }
        public FiltrosModel Filtros { get; set; }

        public LojaConfig Loja { get; set; }
        public List<int> ProdutosNoCarrinho { get; set; } = new();
    }
}
