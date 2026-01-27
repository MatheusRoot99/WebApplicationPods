using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class ProdutoAtributoModel
    {
        public int Id { get; set; }

        public int ProdutoId { get; set; }

        [ForeignKey(nameof(ProdutoId))]
        public ProdutoModel Produto { get; set; } = default!;

        [Required, StringLength(50)]
        public string Chave { get; set; } = string.Empty;

        [Required, StringLength(120)]
        public string Valor { get; set; } = string.Empty;
    }
}
