using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class PedidoItemModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do pedido é obrigatório")]
        public int PedidoId { get; set; }

        [Required(ErrorMessage = "O ID do produto é obrigatório")]
        public int ProdutoId { get; set; }

        [Required(ErrorMessage = "A quantidade é obrigatória")]
        [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser pelo menos 1")]
        public int Quantidade { get; set; }

        [Display(Name = "Preço Unitário")]
        [Range(0.01, double.MaxValue, ErrorMessage = "O preço unitário deve ser maior que zero")]
        public decimal PrecoUnitario { get; set; }

        public decimal? PrecoOriginal { get; set; }

        [StringLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres")]
        public string? Observacoes { get; set; }

        [StringLength(200)]
        public string? Sabor { get; set; }

        // ============================
        // SNAPSHOT DO PRODUTO NO PEDIDO
        // ============================
        // Esses campos guardam como o produto era no momento da compra.
        // Assim, se o lojista alterar o produto depois, o pedido antigo continua correto.

        [StringLength(120)]
        public string? ProdutoNomeSnapshot { get; set; }

        [StringLength(50)]
        public string? TipoProdutoSnapshot { get; set; }

        [StringLength(50)]
        public string? EmbalagemNome { get; set; }

        public int? UnidadesPorEmbalagem { get; set; }

        [StringLength(120)]
        public string? UnidadeVendaDescricao { get; set; }

        public bool EstoqueBaixado { get; set; } = false;

        public DateTime? EstoqueBaixadoEm { get; set; }

        [ForeignKey(nameof(PedidoId))]
        public PedidoModel Pedido { get; set; } = null!;

        [ForeignKey(nameof(ProdutoId))]
        public ProdutoModel Produto { get; set; } = null!;

        [NotMapped]
        public string NomeExibicao =>
            !string.IsNullOrWhiteSpace(ProdutoNomeSnapshot)
                ? ProdutoNomeSnapshot
                : Produto?.Nome ?? $"Produto #{ProdutoId}";

        [NotMapped]
        public string UnidadeVendaExibicao =>
            !string.IsNullOrWhiteSpace(UnidadeVendaDescricao)
                ? UnidadeVendaDescricao
                : "unidade";

        [NotMapped]
        public int TotalUnidadesFisicas =>
            Quantidade * Math.Max(UnidadesPorEmbalagem ?? 1, 1);

        [NotMapped]
        public string ResumoQuantidadeExibicao
        {
            get
            {
                var unidades = Math.Max(UnidadesPorEmbalagem ?? 1, 1);

                if (unidades <= 1)
                    return Quantidade == 1
                        ? "1 unidade"
                        : $"{Quantidade} unidades";

                return $"{Quantidade} × {UnidadeVendaExibicao} ({TotalUnidadesFisicas} unidades)";
            }
        }
    }
}