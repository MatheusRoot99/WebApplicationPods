using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class PedidoAtribuirEntregadorViewModel
    {
        public int PedidoId { get; set; }

        public string ClienteNome { get; set; } = string.Empty;
        public string StatusAtual { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }

        [Display(Name = "Entregador")]
        [Required(ErrorMessage = "Selecione um entregador.")]
        public int? EntregadorId { get; set; }

        public List<SelectListItem> Entregadores { get; set; } = new();
    }
}