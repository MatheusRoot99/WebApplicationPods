using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using WebApplicationPods.Enum;

namespace WebApplicationPods.Models
{
    public class ProdutoFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(120)]
        public string Nome { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Descricao { get; set; }

        [StringLength(80)]
        public string? Marca { get; set; }

        [StringLength(80)]
        public string? SKU { get; set; }

        [StringLength(80)]
        public string? CodigoBarras { get; set; }

        [Required(ErrorMessage = "Selecione uma categoria.")]
        public int CategoriaId { get; set; }

        // ✅ Agora é enum (combina com ProdutoModel)
        public ProdutoTipo TipoProduto { get; set; } = ProdutoTipo.Padrao;

        public bool Ativo { get; set; } = true;
        public bool EmPromocao { get; set; } = false;
        public bool MaisVendido { get; set; } = false;
        public bool RequerMaioridade { get; set; } = false;

        public string? ImagemUrl { get; set; }
        public IFormFile? ImagemUpload { get; set; }

        public List<ProdutoVariacaoFormRow> Variacoes { get; set; } = new();

        public class ProdutoVariacaoFormRow
        {
            public int? Id { get; set; }

            [Required(ErrorMessage = "Informe o nome da variação.")]
            [StringLength(80)]
            public string Nome { get; set; } = "Unidade";

            [Range(1, 999, ErrorMessage = "Multiplicador deve ser >= 1.")]
            public int Multiplicador { get; set; } = 1;

            [Required(ErrorMessage = "Informe o preço.")]
            public string PrecoTexto { get; set; } = "";

            public string? PrecoPromocionalTexto { get; set; }

            [Range(0, int.MaxValue, ErrorMessage = "Estoque inválido.")]
            public int Estoque { get; set; } = 0;

            [StringLength(40)]
            public string? SKU { get; set; }

            [StringLength(30)]
            public string? CodigoBarras { get; set; }

            public bool Ativo { get; set; } = true;
        }

        public List<SaborQuantidadeRow> Sabores { get; set; } = new();

        public class SaborQuantidadeRow
        {
            [StringLength(50)]
            public string Sabor { get; set; } = string.Empty;

            [Range(0, int.MaxValue, ErrorMessage = "Quantidade inválida.")]
            public int Quantidade { get; set; } = 0;
        }

    }
}
