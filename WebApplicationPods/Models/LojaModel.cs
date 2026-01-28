using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class LojaModel
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Nome { get; set; } = string.Empty;

        // ✅ obrigatório e único
        [Required, StringLength(60)]
        public string Subdominio { get; set; } = string.Empty;

        public bool Ativa { get; set; } = true;

        public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

        [StringLength(30)]
        public string? Plano { get; set; } = "Basic";

        public int? DonoUserId { get; set; }

        [ForeignKey(nameof(DonoUserId))]
        public ApplicationUser? Dono { get; set; }

        public LojaConfig? Config { get; set; }
    }
}
