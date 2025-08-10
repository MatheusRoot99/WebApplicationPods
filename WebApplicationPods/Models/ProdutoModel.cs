using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SitePodsInicial.Models
{
    public class ProdutoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome do produto é obrigatório")]
        [StringLength(100, ErrorMessage = "O nome do produto deve ter no máximo 100 caracteres")]
        public string Nome { get; set; }

        [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres")]
        public string Descricao { get; set; }

        [Required(ErrorMessage = "O preço do produto é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "O preço deve ser maior que zero")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Preco { get; set; }

        [Display(Name = "URL da Imagem")]
        [ValidateNever]
        public string ImagemUrl { get; set; }  // Esta propriedade será mapeada para o banco

        [NotMapped]
        [Display(Name = "Imagem do Produto")]
        public IFormFile ImagemUpload { get; set; }

        [Required(ErrorMessage = "A categoria é obrigatória")]
        [Display(Name = "Categoria")]
        public int CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        [ValidateNever]
        public virtual CategoriaModel Categoria { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "O estoque não pode ser negativo")]
        public int Estoque { get; set; } = 0;

        [Display(Name = "Data de Cadastro")]
        [DataType(DataType.DateTime)]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public bool Ativo { get; set; } = true;

        [ValidateNever]
        public virtual ICollection<PedidoItemModel> PedidoItens { get; set; }
    }
}