using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApplicationPods.ViewModels
{
    public class AdminLojaPickerViewModel
    {
        public int? CurrentLojaId { get; set; }
        public string? CurrentLojaNome { get; set; }
        public List<SelectListItem> Lojas { get; set; } = new();
        public string ReturnUrl { get; set; } = "/Admin";
    }
}
