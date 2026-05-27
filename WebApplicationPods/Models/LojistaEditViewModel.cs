using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.ViewModels
{
    public class LojistaEditViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Nome { get; set; } = string.Empty;

        [Required, StringLength(11, MinimumLength = 11)]
        [Display(Name = "CPF (apenas dígitos)")]
        public string CPF { get; set; } = string.Empty;

        [Required, Display(Name = "Telefone (apenas dígitos)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
