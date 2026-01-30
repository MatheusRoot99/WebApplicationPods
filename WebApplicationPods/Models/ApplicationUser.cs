using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        [Required, StringLength(150)]
        public string Nome { get; set; }

        [Required, StringLength(11, MinimumLength = 11)]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter 11 dígitos.")]
        public string CPF { get; set; }

        // opcionais
        public string? Endereco { get; set; }
        public string? Complemento { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? CEP { get; set; }

        // ===== MULTI-LOJA (SaaS) =====
        // Admin Master: LojaId = null
        // Lojista: LojaId = (Id da loja)
        public int? LojaId { get; set; }

        [ForeignKey(nameof(LojaId))]
        public LojaModel? Loja { get; set; }
    }
}
