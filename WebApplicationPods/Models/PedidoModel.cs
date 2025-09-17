using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class PedidoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do cliente é obrigatório")]
        public int ClienteId { get; set; }

        // ⚠️ FK opcional (nula quando "Retirada no local")
        public int? EnderecoId { get; set; }

        [Display(Name = "Data do Pedido")]
        [DataType(DataType.DateTime)]
        public DateTime DataPedido { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "O status do pedido é obrigatório")]
        [StringLength(64, ErrorMessage = "O status deve ter no máximo 64 caracteres")]
        public string Status { get; set; } = "Pendente";

        [Display(Name = "Valor Total")]
        [Range(0, double.MaxValue, ErrorMessage = "O valor total deve ser positivo")]
        public decimal ValorTotal { get; set; }

        [Display(Name = "Taxa de Entrega")]
        [Range(0, double.MaxValue, ErrorMessage = "A taxa de entrega deve ser positiva")]
        public decimal TaxaEntrega { get; set; } = 0;

        [Required(ErrorMessage = "O método de pagamento é obrigatório")]
        [StringLength(32, ErrorMessage = "O método de pagamento deve ter no máximo 32 caracteres")]
        public string MetodoPagamento { get; set; } = string.Empty;

        [Display(Name = "Código da Transação")]
        [StringLength(100, ErrorMessage = "O código da transação deve ter no máximo 100 caracteres")]
        public string CodigoTransacao { get; set; } = Guid.NewGuid().ToString();

        [StringLength(500)]
        public string? Observacoes { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [StringLength(64)]
        public string? RastreioToken { get; set; } = Guid.NewGuid().ToString("N");

        // ====== Retirada no local / dados da loja (sem precisar de EnderecoId) ======
        public bool RetiradaNoLocal { get; set; } = false;

        [StringLength(120)]
        public string? LojaNome { get; set; }

        [StringLength(240)]
        public string? LojaEnderecoTexto { get; set; }

        // Pode armazenar a URL completa do Google Maps (rotas ou place)
        [StringLength(500)]
        public string? LojaMapsUrl { get; set; }

        // ====== Relacionamentos ======
        [ForeignKey(nameof(ClienteId))]
        public ClienteModel Cliente { get; set; }

        // Opcional quando retirada: pode ser null
        [ForeignKey(nameof(EnderecoId))]
        public EnderecoModel? Endereco { get; set; }

        public ICollection<PedidoItemModel> PedidoItens { get; set; } = new List<PedidoItemModel>();

        public ICollection<PaymentModel> Pagamentos { get; set; } = new List<PaymentModel>();
    }
}
