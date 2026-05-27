using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        [DataType(DataType.Password)]
        [Display(Name = "Nova Senha")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;
    }
}
