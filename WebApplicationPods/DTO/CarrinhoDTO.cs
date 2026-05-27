namespace WebApplicationPods.DTO
{
    public class CarrinhoDTO
    {
        public List<CarrinhoItemDTO> Itens { get; set; } = new List<CarrinhoItemDTO>();
        public decimal Total { get; set; }
    }

    public class CarrinhoItemDTO
    {
        public int ProdutoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal PrecoUnitario { get; set; }
        public string ImagemUrl { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public string Sabor { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
    }
}