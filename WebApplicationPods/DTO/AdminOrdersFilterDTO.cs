namespace WebApplicationPods.DTO
{
    public class AdminOrdersFilterDTO
    {
        public DateTime? From { get; set; }          // data inicial (inclusive)
        public DateTime? To { get; set; }            // data final (exclusive se vier só a data; ver View)
        public string? Method { get; set; }          // "Dinheiro", "Pix", "Cartão de Crédito", "Cartão de Débito"
        public bool OnlyPaid { get; set; }           // só pedidos considerados “pagos/válidos”
        public string? Q { get; set; }               // busca por nome do cliente
    }
}
