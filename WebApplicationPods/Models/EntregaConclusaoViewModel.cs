using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebApplicationPods.Models
{
    public class EntregaConclusaoViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome de quem recebeu.")]
        [StringLength(120, ErrorMessage = "O nome de quem recebeu deve ter no máximo 120 caracteres.")]
        [Display(Name = "Nome de quem recebeu")]
        public string NomeRecebedor { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "A observação deve ter no máximo 500 caracteres.")]
        [Display(Name = "Observação da entrega")]
        public string? ObservacaoEntrega { get; set; }

        [Display(Name = "Foto do comprovante")]
        public IFormFile? FotoComprovante { get; set; }
    }
}