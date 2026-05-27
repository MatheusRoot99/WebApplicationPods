using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class EnderecoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do cliente é obrigatório")]
        public int ClienteId { get; set; }

        [Required(ErrorMessage = "O logradouro é obrigatório")]
        [StringLength(100, ErrorMessage = "O logradouro deve ter no máximo 100 caracteres")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "O número é obrigatório")]
        [StringLength(20, ErrorMessage = "O número deve ter no máximo 20 caracteres")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "O complemento deve ter no máximo 50 caracteres")]
        public string? Complemento { get; set; }

        [Required(ErrorMessage = "O bairro é obrigatório")]
        [StringLength(50, ErrorMessage = "O bairro deve ter no máximo 50 caracteres")]
        public string Bairro { get; set; } = string.Empty;

        [Required(ErrorMessage = "A cidade é obrigatória")]
        [StringLength(50, ErrorMessage = "A cidade deve ter no máximo 50 caracteres")]
        public string Cidade { get; set; } = string.Empty;

        [Required(ErrorMessage = "O estado é obrigatório")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "UF deve ter 2 caracteres")]
        public string Estado { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CEP é obrigatório")]
        [RegularExpression(@"^\d{5}-\d{3}$", ErrorMessage = "CEP inválido")]
        [Display(Name = "CEP")]
        public string CEP { get; set; } = string.Empty;

        public bool Principal { get; set; } = false;
        public bool Ativo { get; set; } = true;

        [ForeignKey("ClienteId")]
        [ValidateNever]
        public ClienteModel? Cliente { get; set; }
    }
}