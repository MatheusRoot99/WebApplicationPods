using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class CategoriaModel
    {
        public int Id { get; set; }

        // MULTI-LOJA
        public int LojaId { get; set; }

        [Required(ErrorMessage = "O nome da categoria é obrigatório")]
        [StringLength(50, ErrorMessage = "O nome deve ter no máximo 50 caracteres")]
        [Display(Name = "Nome da Categoria")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres")]
        [Display(Name = "Descrição")]
        public string? Descricao { get; set; }

        [ValidateNever]
        public ICollection<ProdutoModel> Produtos { get; set; } = new List<ProdutoModel>();
    }
}
