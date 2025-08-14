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
        public string Nome { get; set; }
        public decimal PrecoUnitario { get; set; }
        public string ImagemUrl { get; set; }
        public int Quantidade { get; set; }
        public string Sabor { get; set; }
        public string Observacoes { get; set; }
    }
}
