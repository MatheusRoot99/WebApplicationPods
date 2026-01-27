namespace WebApplicationPods.Models
{
    public class CarrinhoModel
    {
        public int Id { get; set; }
        public int LojaId { get; set; }

        public string ClienteTelefone { get; set; }
        public string SessionId { get; set; }
        public List<CarrinhoItemViewModel> Itens { get; set; } = new List<CarrinhoItemViewModel>();

        public decimal Total => Itens?.Sum(i => i.Subtotal) ?? 0;
    }

    public class CarrinhoItemViewModel
    {
        public int Id { get; set; }               // ID do item no carrinho (opcional)
        public ProdutoModel Produto { get; set; } // Objeto completo do produto
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public string Observacoes { get; set; }
        public string Sabor { get; set; }
        public string ImagemUrl { get; set; }     // Pode ser derivado do Produto
        public decimal Subtotal => Quantidade * PrecoUnitario;

        // Propriedade conveniente para acessar o ID do produto
        public int ProdutoId => Produto?.Id ?? 0;
    }
}
