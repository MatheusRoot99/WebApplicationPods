// Models/ViewModels/CarrinhoPageViewModel.cs
using System.Collections.Generic;

namespace WebApplicationPods.Models
{
    public class CarrinhoPageViewModel
    {
        public CarrinhoModel Carrinho { get; set; } = new CarrinhoModel();
        public List<ProdutoResumoVM> Populares { get; set; } = new();
    }

    public class ProdutoResumoVM
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string ImagemUrl { get; set; }
        public decimal Preco { get; set; }
        public decimal? PrecoPromocional { get; set; }

        public bool EmPromocao => PrecoPromocional.HasValue && PrecoPromocional.Value < Preco;
        public decimal PrecoFinal => EmPromocao ? PrecoPromocional!.Value : Preco;
    }
}
