using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.ViewModels
{
    public class LojistaEditViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Nome { get; set; }

        [Required, StringLength(11, MinimumLength = 11)]
        [Display(Name = "CPF (apenas dígitos)")]
        public string CPF { get; set; }

        [Required, Display(Name = "Telefone (apenas dígitos)")]
        public string PhoneNumber { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }
    }
}
