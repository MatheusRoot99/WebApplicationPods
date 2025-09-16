using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class AuthLoginViewModel
    {
        [Display(Name = "Telefone")]
        [Required(ErrorMessage = "Informe seu telefone com DDD")]
        public string Telefone { get; set; } = string.Empty;

        // Preenchido quando achamos o cliente
        public string? ClienteNome { get; set; }

        // Se true, o POST confirma o login direto
        public bool Confirmar { get; set; } = false;

        // Para retornar ao lugar certo
        public string? ReturnUrl { get; set; }
    }
}
