namespace WebApplicationPods.Payments.Options
{
    public class PixManualOptions
    {
        // chave pix do lojista: cpf/cnpj/email/celular/chave-aleatoria
        public string PixKey { get; set; } = "";

        // nome do recebedor (máx 25 chars segundo manual)
        public string BeneficiaryName { get; set; } = "";

        // cidade do recebedor (máx 15 chars)
        public string BeneficiaryCity { get; set; } = "BRASILIA";

        // identificador (txid) prefixo opcional, app completa com PedidoId
        public string? TxIdPrefix { get; set; }

        // opcional: descrição do recebedor (merchant name curto)
        public string? MerchantName { get; set; }
    }
}
