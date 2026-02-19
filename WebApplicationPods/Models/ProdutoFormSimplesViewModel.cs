using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using WebApplicationPods.Enum;

namespace WebApplicationPods.Models
{
    public class ProdutoFormSimplesViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(120)]
        public string Nome { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Descricao { get; set; }

        [StringLength(80)]
        public string? Marca { get; set; }

        [StringLength(40)]
        public string? SKU { get; set; }

        [StringLength(30)]
        public string? CodigoBarras { get; set; }

        [Required(ErrorMessage = "Selecione uma categoria.")]
        public int CategoriaId { get; set; }

        public ProdutoTipo TipoProduto { get; set; } = ProdutoTipo.Padrao;

        [Range(0.01, double.MaxValue, ErrorMessage = "Preço inválido.")]
        public decimal Preco { get; set; }

        public decimal? PrecoPromocional { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Estoque inválido.")]
        public int Estoque { get; set; }

        public bool Ativo { get; set; } = true;
        public bool EmPromocao { get; set; } = false;
        public bool MaisVendido { get; set; } = false;
        public bool RequerMaioridade { get; set; } = false;

        public string? ImagemUrl { get; set; }
        public IFormFile? ImagemUpload { get; set; }

        // POD extras (opcional)
        public int? PodPuffs { get; set; }
        [StringLength(40)]
        public string? PodCapacidadeBateria { get; set; }
        [StringLength(40)]
        public string? PodTipo { get; set; }

        // Se você quiser continuar usando sabor/cor como “atributo simples”
        [StringLength(50)]
        public string? Sabor { get; set; }
        [StringLength(30)]
        public string? Cor { get; set; }
    }
}
