using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Informe Telefone ou CPF")]
        [Display(Name = "Telefone ou CPF")]
        public string TelefoneOuCpf { get; set; }

        [Required(ErrorMessage = "Informe a senha")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; }

        [Display(Name = "Lembrar-me")]
        public bool LembrarMe { get; set; }


        [ValidateNever] // <- não validar esse campo
        public string ReturnUrl { get; set; }
    }
}
