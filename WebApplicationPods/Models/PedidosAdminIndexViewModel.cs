using Microsoft.AspNetCore.Mvc.Rendering;
using WebApplicationPods.DTO;

namespace WebApplicationPods.Models
{
    public class PedidosAdminIndexViewModel
    {
        public List<PedidoModel> Pedidos { get; set; } = new();
        public AdminOrdersFilterDTO Filtros { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> Entregadores { get; set; } = new();
        public int TotalEncontrado { get; set; }
        public string ModoTitulo { get; set; } = "abertos";
    }
}