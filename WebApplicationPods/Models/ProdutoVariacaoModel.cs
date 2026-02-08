using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class ProdutoVariacaoModel
    {
        public int Id { get; set; }

        [Required]
        public int ProdutoId { get; set; }

        [ValidateNever]
        public ProdutoModel Produto { get; set; } = default!;

        [Required, StringLength(80)]
        public string Nome { get; set; } = "Unidade";

        // Ex: 1 (unidade), 6 (fardo), 12 (caixa)
        [Range(1, 999)]
        public int Multiplicador { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Preco { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecoPromocional { get; set; }

        [Range(0, int.MaxValue)]
        public int Estoque { get; set; }

        [StringLength(40)]
        public string? SKU { get; set; }

        [StringLength(30)]
        public string? CodigoBarras { get; set; }

        public bool Ativo { get; set; } = true;

        public bool EstaEmPromocao()
            => PrecoPromocional.HasValue && PrecoPromocional.Value > 0 && PrecoPromocional.Value < Preco;
    }
}
