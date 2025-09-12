using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class BuscarPedidoVM
    {
        [Display(Name = "Número do pedido")]
        [Required(ErrorMessage = "Informe o número do pedido")]
        public int? Id { get; set; }

        [Display(Name = "Token de rastreio")]
        [Required(ErrorMessage = "Informe o token de rastreio")]
        public string? Token { get; set; }
    }
}
