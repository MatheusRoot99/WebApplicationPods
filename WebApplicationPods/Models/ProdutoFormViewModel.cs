using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class ProdutoFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "O nome do produto é obrigatório")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        public string? Descricao { get; set; }
        public string? Marca { get; set; }
        public string? SKU { get; set; }
        public string? CodigoBarras { get; set; }

        [Required]
        public int CategoriaId { get; set; }

        public bool RequerMaioridade { get; set; }
        public bool Ativo { get; set; } = true;
        public bool MaisVendido { get; set; }
        public bool EmPromocao { get; set; } // (opcional manter no produto; promo real fica na variação)

        [ValidateNever]
        public string? ImagemUrl { get; set; }

        [ValidateNever]
        public IFormFile? ImagemUpload { get; set; }

        // ✅ Variações
        public List<ProdutoVariacaoFormRow> Variacoes { get; set; } = new();

        public class ProdutoVariacaoFormRow
        {
            public int? Id { get; set; } // quando editar

            [Required, StringLength(80)]
            public string Nome { get; set; } = "Unidade";

            [Range(1, 999)]
            public int Multiplicador { get; set; } = 1;

            [Range(0.01, double.MaxValue)]
            public decimal Preco { get; set; }

            public decimal? PrecoPromocional { get; set; }

            [Range(0, int.MaxValue)]
            public int Estoque { get; set; }

            public string? SKU { get; set; }
            public string? CodigoBarras { get; set; }

            public bool Ativo { get; set; } = true;
        }
    }
}
