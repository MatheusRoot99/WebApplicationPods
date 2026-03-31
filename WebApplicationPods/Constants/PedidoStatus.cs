namespace WebApplicationPods.Constants
{
    public static class PedidoStatus
    {
        public const string Pendente = "Pendente";
        public const string AguardandoPagamento = "Aguardando Pagamento";
        public const string AguardandoPagamentoEntrega = "Aguardando Pagamento (Entrega)";
        public const string AguardandoConfirmacaoDinheiro = "Aguardando Confirmação (Dinheiro)";
        public const string Pago = "Pago";
        public const string EmPreparacao = "Em Preparação";
        public const string Pronto = "Pronto";
        public const string SaiuParaEntrega = "Saiu p/ Entrega";
        public const string Concluido = "Concluído";
        public const string Cancelado = "Cancelado";
        public const string PagamentoFalhou = "Pagamento Falhou";

        public static readonly string[] Finais =
        {
            Concluido,
            Cancelado,
            PagamentoFalhou
        };
    }
}