namespace SitePodsInicial.Models
{
    public class CarrinhoModel
    {
        public List<CarrinhoItemViewModel> Itens { get; set; } = new List<CarrinhoItemViewModel>();
        public decimal Total => Itens.Sum(i => i.Quantidade * i.PrecoUnitario);
    }

    public class CarrinhoItemViewModel
    {
        public ProdutoModel Produto { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public string Observacoes { get; set; }
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }
}
