using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using WebApplicationPods.Enum;

namespace WebApplicationPods.Models
{
    public class ProdutoFormSimplesViewModel
    {
        public int? Id { get; set; }

        // ============================
        // CAMPOS PRINCIPAIS
        // ============================

        [Required(ErrorMessage = "Informe o nome do produto.")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "A descrição deve ter no máximo 1000 caracteres.")]
        public string? Descricao { get; set; }

        [StringLength(80, ErrorMessage = "A marca deve ter no máximo 80 caracteres.")]
        public string? Marca { get; set; }

        [Required(ErrorMessage = "Selecione uma categoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria.")]
        public int CategoriaId { get; set; }

        public ProdutoTipo TipoProduto { get; set; } = ProdutoTipo.Padrao;

        [Range(0.01, double.MaxValue, ErrorMessage = "Informe um preço maior que zero.")]
        public decimal Preco { get; set; }

        public decimal? PrecoPromocional { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "O estoque não pode ser negativo.")]
        public int Estoque { get; set; }

        public bool Ativo { get; set; } = true;

        public string? ImagemUrl { get; set; }

        public IFormFile? ImagemUpload { get; set; }

        // ============================
        // CAMPOS DE POD
        // ============================

        [StringLength(50, ErrorMessage = "O sabor deve ter no máximo 50 caracteres.")]
        public string? Sabor { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "O número de puffs não pode ser negativo.")]
        public int? PodPuffs { get; set; }

        [StringLength(40, ErrorMessage = "A bateria deve ter no máximo 40 caracteres.")]
        public string? PodCapacidadeBateria { get; set; }

        [StringLength(40, ErrorMessage = "O tipo do pod deve ter no máximo 40 caracteres.")]
        public string? PodTipo { get; set; }

        // ============================
        // CAMPOS DE BEBIDA
        // ============================

        [Range(1, 100000, ErrorMessage = "O volume em ml deve ser maior que zero.")]
        public int? BebidaVolumeMl { get; set; }

        [StringLength(40, ErrorMessage = "O tipo da bebida deve ter no máximo 40 caracteres.")]
        public string? BebidaTipo { get; set; }

        public BebidaEmbalagemTipo BebidaEmbalagem { get; set; } = BebidaEmbalagemTipo.NaoInformado;

        [Range(1, 1000, ErrorMessage = "A quantidade por embalagem deve ser maior que zero.")]
        public int? BebidaQtdPorEmbalagem { get; set; }

        // ============================
        // CAMPOS ANTIGOS / COMPATIBILIDADE
        // Não aparecem mais no formulário simples.
        // O ProdutoController limpa esses campos ao salvar.
        // ============================

        public string? SKU { get; set; }

        public string? CodigoBarras { get; set; }

        public bool EmPromocao { get; set; } = false;

        public bool MaisVendido { get; set; } = false;

        public bool RequerMaioridade { get; set; } = false;

        public decimal? BebidaTeorAlcoolico { get; set; }

        public string? Cor { get; set; }
    }
}