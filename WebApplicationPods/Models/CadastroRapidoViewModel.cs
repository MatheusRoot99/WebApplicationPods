using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class CadastroRapidoViewModel
    {
        // Retorno do fluxo após salvar
        public string? ReturnUrl { get; set; }

        // Dados do cliente
        [Required(ErrorMessage = "O telefone é obrigatório")]
        [Display(Name = "Telefone (WhatsApp)")]
        // Aceita (43) 98846-2752, 43988462752, 43 98846-2752…
        [RegularExpression(@"^\(?\d{2}\)?\s?\d{4,5}-?\d{4}$", ErrorMessage = "Telefone inválido")]
        public string Telefone { get; set; } = string.Empty;

        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        [Display(Name = "Nome completo")]
        [StringLength(120)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        public string Email { get; set; } = string.Empty;

        // Endereço principal
        [Required(ErrorMessage = "O CEP é obrigatório")]
        [Display(Name = "CEP")]
        [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP inválido")]
        public string CEP { get; set; } = string.Empty;

        [Required(ErrorMessage = "O logradouro é obrigatório")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "O número é obrigatório")]
        public string Numero { get; set; } = string.Empty;

        public string? Complemento { get; set; }

        [Required(ErrorMessage = "O bairro é obrigatório")]
        public string Bairro { get; set; } = string.Empty;

        [Required(ErrorMessage = "A cidade é obrigatória")]
        public string Cidade { get; set; } = string.Empty;

        [Required(ErrorMessage = "O estado é obrigatório")]
        [StringLength(2, MinimumLength = 2)]
        [RegularExpression(@"^[A-Za-z]{2}$", ErrorMessage = "UF inválida")]
        public string Estado { get; set; } = string.Empty;


        // ...campos existentes...

        [Required(ErrorMessage = "O CPF é obrigatório")]
        [Display(Name = "CPF")]
        [RegularExpression(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$", ErrorMessage = "CPF inválido")]
        public string CPF { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de nascimento é obrigatória")]
        [DataType(DataType.Date)]
        [Display(Name = "Data de nascimento")]
        public DateTime? DataNascimento { get; set; }
    }
}
