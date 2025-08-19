using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }
}
