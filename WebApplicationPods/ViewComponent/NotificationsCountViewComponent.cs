using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using WebApplicationPods.Repository.Interface;

public class NotificationsCountViewComponent : ViewComponent
{
    private readonly IPedidoRepository _pedidos;
    public NotificationsCountViewComponent(IPedidoRepository pedidos) => _pedidos = pedidos;

    public IViewComponentResult Invoke()
    {
        var user = HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return Content("0");

        if (user.IsInRole("Lojista"))
        {
            // Lojista: por exemplo, pedidos de hoje não cancelados
            var count = _pedidos.ObterDoDia().Count(p => p.Status != "Cancelado");
            return Content(count.ToString());
        }
        else
        {
            // Cliente: 1 se houver último pedido em andamento, senão 0
            var clienteIdStr = user.FindFirstValue("ClienteId");
            int clienteId = 0; int.TryParse(clienteIdStr, out clienteId);

            if (clienteId <= 0) return Content("0");

            var andamento = new[] { "Cancelado", "Pagamento Falhou", "Entregue", "Concluído" };

            var existeEmAndamento = _pedidos.ObterPorCliente(clienteId)
                .Any(p => !string.IsNullOrEmpty(p.Status) &&
                          !andamento.Any(fin => p.Status.Contains(fin, System.StringComparison.OrdinalIgnoreCase)));

            return Content(existeEmAndamento ? "1" : "0");
        }
    }
}
