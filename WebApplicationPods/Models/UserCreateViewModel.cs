using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class UserCreateViewModel
    {
        [Required, StringLength(150)]
        public string Nome { get; set; } = string.Empty;

        [Required, StringLength(11, MinimumLength = 11)]
        [Display(Name = "CPF (apenas dígitos)")]
        public string CPF { get; set; } = string.Empty;

        [Required, Display(Name = "Telefone (apenas dígitos)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required, MinLength(6)]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        // Opcional: escolha de role no momento da criação
        [Display(Name = "Perfil (opcional)")]
        public string? Role { get; set; } // Admin, Lojista ou Cliente
    }
}
