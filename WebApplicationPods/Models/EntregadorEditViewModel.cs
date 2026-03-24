using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class EntregadorEditViewModel
    {
        public int Id { get; set; }
        public int? UserId { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [StringLength(11, MinimumLength = 11)]
        [Display(Name = "CPF (apenas dígitos)")]
        public string CPF { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Telefone (apenas dígitos)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "E-mail")]
        public string? Email { get; set; }

        [Display(Name = "Veículo")]
        [StringLength(80)]
        public string? Veiculo { get; set; }

        [Display(Name = "Placa")]
        [StringLength(20)]
        public string? PlacaVeiculo { get; set; }

        [Display(Name = "Observações")]
        [StringLength(500)]
        public string? Observacoes { get; set; }

        [Display(Name = "Ativo")]
        public bool Ativo { get; set; }
    }
}