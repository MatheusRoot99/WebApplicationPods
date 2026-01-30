using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApplicationPods.ViewModels
{
    public class LojaFormViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(120)]
        [Display(Name = "Nome da loja")]
        public string Nome { get; set; }

        [Required, StringLength(60)]
        [Display(Name = "Subdomínio")]
        public string Subdominio { get; set; }

        [StringLength(30)]
        [Display(Name = "Plano")]
        public string? Plano { get; set; } = "Basic";

        [Display(Name = "Ativa")]
        public bool Ativa { get; set; } = true;

        [Display(Name = "Dono / Lojista")]
        public int? DonoUserId { get; set; }

        public IEnumerable<SelectListItem> Lojistas { get; set; } = Enumerable.Empty<SelectListItem>();
    }
}
