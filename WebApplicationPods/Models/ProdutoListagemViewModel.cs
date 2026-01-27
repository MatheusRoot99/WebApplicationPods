using WebApplicationPods.Models;

namespace WebApplicationPods.Models
{
    public class ProdutoListagemViewModel
    {
        public IEnumerable<ProdutoModel> Produtos { get; set; } // Mude para IEnumerable
        public FiltrosModel Filtros { get; set; }

        public LojaConfig Loja { get; set; }
    }
}
