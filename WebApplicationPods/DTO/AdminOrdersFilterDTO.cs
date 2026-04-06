namespace WebApplicationPods.DTO
{
    public class AdminOrdersFilterDTO
    {
        public string? Filtro { get; set; } = "abertos";

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Method { get; set; }
        public bool OnlyPaid { get; set; }
        public string? Q { get; set; }

        public int? PedidoId { get; set; }
        public string? Telefone { get; set; }
        public string? Status { get; set; }
        public int? EntregadorId { get; set; }
        public DateTime? DataInicial { get; set; }
        public DateTime? DataFinal { get; set; }
    }
}
