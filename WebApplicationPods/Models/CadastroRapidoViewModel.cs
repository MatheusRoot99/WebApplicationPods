using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class CadastroRapidoViewModel
    {
        // Dados do cliente
        public string Telefone { get; set; }

        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "O email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        public string Email { get; set; }

        // Dados do endereço
        [Required(ErrorMessage = "O CEP é obrigatório")]
        [Display(Name = "CEP")]
        public string CEP { get; set; }

        [Required(ErrorMessage = "O logradouro é obrigatório")]
        public string Logradouro { get; set; }

        [Required(ErrorMessage = "O número é obrigatório")]
        public string Numero { get; set; }

        public string Complemento { get; set; }

        [Required(ErrorMessage = "O bairro é obrigatório")]
        public string Bairro { get; set; }

        [Required(ErrorMessage = "A cidade é obrigatória")]
        public string Cidade { get; set; }

        [Required(ErrorMessage = "O estado é obrigatório")]
        [StringLength(2, MinimumLength = 2)]
        public string Estado { get; set; }
    }
}
