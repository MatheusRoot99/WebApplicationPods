// ViewModels/ClienteViewModel.cs
using System.ComponentModel.DataAnnotations;
using WebApplicationPods.Validation;

namespace WebApplicationPods.ViewModels
{
    public class ClienteViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        [StringLength(100)]
        [Display(Name = "Nome completo")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu telefone com DDD")]
        [Display(Name = "Telefone (WhatsApp)")]
        // Aceita (43) 98846-2752, 43988462752, 43 98846-2752…
        [RegularExpression(@"^\(?\d{2}\)?\s?\d{4,5}-?\d{4}$", ErrorMessage = "Telefone inválido")]
        public string Telefone { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório")]
        [Display(Name = "CPF")]
        // Aceita com ou sem máscara; normalize no controller
        [RegularExpression(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$", ErrorMessage = "CPF inválido")]
        [Cpf] // <- usa o atributo acima
        public string CPF { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de nascimento é obrigatória")]
        [DataType(DataType.Date)]
        [Display(Name = "Data de nascimento")]
        public DateTime? DataNascimento { get; set; }

        [Display(Name = "Cadastrado em")]
        public DateTime? DataCadastro { get; set; }
    }
}
