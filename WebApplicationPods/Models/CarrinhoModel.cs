namespace WebApplicationPods.Models
{
    public class CarrinhoModel
    {
        public int Id { get; set; }
        public int LojaId { get; set; }

        public string ClienteTelefone { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public List<CarrinhoItemViewModel> Itens { get; set; } = new List<CarrinhoItemViewModel>();

        public decimal Total => Itens?.Sum(i => i.Subtotal) ?? 0;
    }

    public class CarrinhoItemViewModel
    {
        public int Id { get; set; }
        public ProdutoModel Produto { get; set; } = new ProdutoModel();
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public string Observacoes { get; set; } = string.Empty;
        public string Sabor { get; set; } = string.Empty;
        public string ImagemUrl { get; set; } = string.Empty;
        public decimal Subtotal => Quantidade * PrecoUnitario;

        public int ProdutoId => Produto?.Id ?? 0;
    }
}