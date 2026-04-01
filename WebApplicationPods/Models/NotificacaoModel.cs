using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class NotificacaoModel
    {
        public int Id { get; set; }

        [Required]
        public int LojaId { get; set; }

        public int? PedidoId { get; set; }

        [Required]
        [StringLength(40)]
        public string Tipo { get; set; } = "info";

        [Required]
        [StringLength(120)]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Mensagem { get; set; } = string.Empty;

        public bool Lida { get; set; } = false;
        public DateTime? DataLeitura { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        [ForeignKey(nameof(PedidoId))]
        public PedidoModel? Pedido { get; set; }

        [ForeignKey(nameof(LojaId))]
        public LojaModel? Loja { get; set; }
    }
}