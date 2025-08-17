using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class UsuarioModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O telefone é obrigatório")]
        [RegularExpression(@"^\(?\d{2}\)?[\s-]?\d{4,5}[\s-]?\d{4}$",
                          ErrorMessage = "Telefone inválido")]
        public string Telefone { get; set; }

        public string Nome { get; set; }

        [EmailAddress(ErrorMessage = "Email inválido")]
        public string Email { get; set; } // Opcional

        public string Endereco { get; set; }
        public string Complemento { get; set; }
        public string Cidade { get; set; }
        public string Estado { get; set; }
        public string CEP { get; set; }
    }
}
