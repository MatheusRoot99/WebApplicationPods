using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SitePodsInicial.Models
{
    public class PedidoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do cliente é obrigatório")]
        public int ClienteId { get; set; }

        [Required(ErrorMessage = "O ID do endereço é obrigatório")]
        public int EnderecoId { get; set; }

        [Display(Name = "Data do Pedido")]
        [DataType(DataType.DateTime)]
        public DateTime DataPedido { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "O status do pedido é obrigatório")]
        [StringLength(20, ErrorMessage = "O status deve ter no máximo 20 caracteres")]
        public string Status { get; set; }

        [Display(Name = "Valor Total")]
        [Range(0, double.MaxValue, ErrorMessage = "O valor total deve ser positivo")]
        public decimal ValorTotal { get; set; }

        [Display(Name = "Taxa de Entrega")]
        [Range(0, double.MaxValue, ErrorMessage = "A taxa de entrega deve ser positiva")]
        public decimal TaxaEntrega { get; set; } = 0;

        [Required(ErrorMessage = "O método de pagamento é obrigatório")]
        [StringLength(20, ErrorMessage = "O método de pagamento deve ter no máximo 20 caracteres")]
        public string MetodoPagamento { get; set; }

        [Display(Name = "Código da Transação")]
        [StringLength(100, ErrorMessage = "O código da transação deve ter no máximo 100 caracteres")]
        public string CodigoTransacao { get; set; }

        // Relacionamentos
        [ForeignKey("ClienteId")]
        public ClienteModel Cliente { get; set; }

        [ForeignKey("EnderecoId")]
        public EnderecoModel Endereco { get; set; }

        public ICollection<PedidoItemModel> PedidoItens { get; set; }
    }
}
