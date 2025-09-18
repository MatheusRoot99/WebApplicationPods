using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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
        public decimal PrecoUnitario { get; set; }   // preço efetivamente cobrado (promocional se houver)

        /// <summary>Preço cheio (sem promoção) no momento da compra.</summary>
        public decimal? PrecoOriginal { get; set; }

        /// <summary>Observações do item (ex.: sem gelo).</summary>
        [StringLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres")]
        public string? Observacoes { get; set; }

        /// <summary>Sabor selecionado (se aplicável). Usado para dar baixa por sabor.</summary>
        [StringLength(200)]
        public string? Sabor { get; set; }

        /// <summary>Controle para evitar baixa dupla.</summary>
        public bool EstoqueBaixado { get; set; } = false;
        public DateTime? EstoqueBaixadoEm { get; set; }

        // Relacionamentos
        [ForeignKey(nameof(PedidoId))]
        public PedidoModel Pedido { get; set; } = null!;

        [ForeignKey(nameof(ProdutoId))]
        public ProdutoModel Produto { get; set; } = null!;
    }
}
