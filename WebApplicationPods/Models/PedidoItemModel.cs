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
        public decimal PrecoUnitario { get; set; }

        [StringLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres")]
        public string? Observacoes { get; set; }

        /// <summary>Preço cheio (sem promoção) no momento da compra.</summary>
        public decimal? PrecoOriginal { get; set; }   // <-- NOVO

        // Relacionamentos
        [ForeignKey("PedidoId")]
        public PedidoModel Pedido { get; set; }

        [ForeignKey("ProdutoId")]
        public ProdutoModel Produto { get; set; }
    }
}
