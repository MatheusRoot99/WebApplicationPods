using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class PedidoHistoricoModel
    {
        public int Id { get; set; }

        [Required]
        public int PedidoId { get; set; }

        [StringLength(64)]
        public string? StatusAnterior { get; set; }

        [Required]
        [StringLength(64)]
        public string NovoStatus { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Observacao { get; set; }

        [StringLength(100)]
        public string? UsuarioResponsavelId { get; set; }

        [StringLength(120)]
        public string? NomeResponsavel { get; set; }

        [StringLength(60)]
        public string? Origem { get; set; }

        public DateTime DataCadastro { get; set; } = DateTime.Now;

        [ForeignKey(nameof(PedidoId))]
        public PedidoModel Pedido { get; set; } = null!;
    }
}