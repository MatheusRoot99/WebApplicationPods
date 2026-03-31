using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class EntregaModel
    {
        public int Id { get; set; }

        [Required]
        public int PedidoId { get; set; }

        [ForeignKey(nameof(PedidoId))]
        public PedidoModel Pedido { get; set; } = null!;

        public int? EntregadorId { get; set; }

        [ForeignKey(nameof(EntregadorId))]
        public EntregadorModel? Entregador { get; set; }

        [Required]
        [StringLength(40)]
        public string Status { get; set; } = "Pendente";

        public DateTime? DataAtribuicao { get; set; }
        public DateTime? DataAceite { get; set; }
        public DateTime? DataColeta { get; set; }
        public DateTime? DataSaidaParaEntrega { get; set; }
        public DateTime? DataConclusao { get; set; }

        [StringLength(500)]
        public string? Observacao { get; set; }

        [StringLength(120)]
        public string? NomeRecebedor { get; set; }

        [StringLength(500)]
        public string? ObservacaoEntrega { get; set; }

        [StringLength(500)]
        public string? ComprovanteEntregaUrl { get; set; }

        public DateTime DataCadastro { get; set; } = DateTime.Now;
        public DateTime? DataAtualizacao { get; set; }
    }
}