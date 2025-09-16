using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class EditarInfoViewModel
    {
        [Required(ErrorMessage = "Informe seu nome")]
        [Display(Name = "Nome completo")]
        public string Nome { get; set; } = "";

        [Required(ErrorMessage = "Informe seu telefone")]
        [Display(Name = "Telefone (WhatsApp)")]
        public string Telefone { get; set; } = "";

        [EmailAddress(ErrorMessage = "E-mail inválido")]
        [Display(Name = "E-mail (opcional)")]
        public string? Email { get; set; }

        // para voltar ao fluxo (ex.: Carrinho/Resumo)
        public string? ReturnUrl { get; set; }
    }
}
