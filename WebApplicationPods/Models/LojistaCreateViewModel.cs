using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.ViewModels
{
    public class LojistaCreateViewModel
    {
        [Required, StringLength(150)]
        public string Nome { get; set; }

        [Required, StringLength(11, MinimumLength = 11)]
        [Display(Name = "CPF (apenas dígitos)")]
        public string CPF { get; set; }

        [Required, Display(Name = "Telefone (apenas dígitos)")]
        public string PhoneNumber { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MinLength(6)]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; }
    }
}