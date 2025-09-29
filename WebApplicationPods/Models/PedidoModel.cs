using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class PedidoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do cliente é obrigatório")]
        public int ClienteId { get; set; }

        // FK opcional (nula quando "Retirada no local")
        public int? EnderecoId { get; set; }

        [Display(Name = "Data do Pedido")]
        [DataType(DataType.DateTime)]
        public DateTime DataPedido { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "O status do pedido é obrigatório")]
        [StringLength(64)]
        public string Status { get; set; } = "Pendente";

        [Display(Name = "Valor Total")]
        [Range(0, double.MaxValue)]
        public decimal ValorTotal { get; set; }

        [Display(Name = "Taxa de Entrega")]
        [Range(0, double.MaxValue)]
        public decimal TaxaEntrega { get; set; } = 0;

        [Required]
        [StringLength(32)]
        public string MetodoPagamento { get; set; } = string.Empty;

        [Display(Name = "Código da Transação")]
        [StringLength(100)]
        public string CodigoTransacao { get; set; } = Guid.NewGuid().ToString();

        [StringLength(500)]
        public string? Observacoes { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        [StringLength(64)]
        public string? RastreioToken { get; set; } = Guid.NewGuid().ToString("N");

        // ===== Retirada no local / dados da loja =====
        public bool RetiradaNoLocal { get; set; } = false;

        [StringLength(120)]
        public string? LojaNome { get; set; }

        [StringLength(240)]
        public string? LojaEnderecoTexto { get; set; }

        [StringLength(500)]
        public string? LojaMapsUrl { get; set; }

        // ===== Timestamps por etapa =====
        // 0 - Pendente
        public DateTime? DataAguardandoPagamento { get; set; }      // 1
        public DateTime? DataPagamentoAprovado { get; set; }        // 2
        public DateTime? DataInicioPreparo { get; set; }            // 3
        public DateTime? DataSaiuParaEntregaOuRetirada { get; set; }// 4
        public DateTime? DataConcluido { get; set; }                // 5
        public DateTime? DataCancelado { get; set; }                // cancelado

        // ===== Relacionamentos =====
        [ForeignKey(nameof(ClienteId))]
        public ClienteModel Cliente { get; set; }

        [ForeignKey(nameof(EnderecoId))]
        public EnderecoModel? Endereco { get; set; }

        public ICollection<PedidoItemModel> PedidoItens { get; set; } = new List<PedidoItemModel>();
        public ICollection<PaymentModel> Pagamentos { get; set; } = new List<PaymentModel>();
    }
}
