using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class EntregadorModel
    {
        public int Id { get; set; }

        [Required]
        public int LojaId { get; set; }

        [ForeignKey(nameof(LojaId))]
        public LojaModel? Loja { get; set; }

        public int? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? Usuario { get; set; }

        [Required(ErrorMessage = "Informe o nome do entregador.")]
        [StringLength(150)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o telefone.")]
        [StringLength(20)]
        public string Telefone { get; set; } = string.Empty;

        [StringLength(80)]
        public string? Veiculo { get; set; }

        [StringLength(20)]
        public string? PlacaVeiculo { get; set; }

        [StringLength(500)]
        public string? Observacoes { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime DataCadastro { get; set; } = DateTime.Now;
    }
}