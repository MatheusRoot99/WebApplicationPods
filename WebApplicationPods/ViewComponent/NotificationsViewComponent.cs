using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using WebApplicationPods.Repository.Interface;

public class NotificationsViewComponent : ViewComponent
{
    private readonly IPedidoRepository _pedidos;

    public NotificationsViewComponent(IPedidoRepository pedidos) => _pedidos = pedidos;

    public IViewComponentResult Invoke()
    {
        var user = HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return View("Default", Enumerable.Empty<WebApplicationPods.Models.PedidoModel>());

        if (user.IsInRole("Lojista"))
        {
            // Lojista: mantém sua lógica (ex.: pedidos de hoje não cancelados)
            var lista = _pedidos.ObterDoDia()
                                .Where(p => p.Status != "Cancelado")
                                .OrderByDescending(p => p.DataPedido)
                                .Take(10)
                                .ToList();

            ViewBag.IsLojista = true;
            return View("Default", lista);
        }
        else
        {
            // Cliente: APENAS o último pedido EM ANDAMENTO desse cliente
            var clienteIdStr = user.FindFirstValue("ClienteId");
            int clienteId = 0; int.TryParse(clienteIdStr, out clienteId);

            var andamento = new[] { "Cancelado", "Pagamento Falhou", "Entregue", "Concluído" };

            var ultimoEmAndamento = (clienteId > 0)
                ? _pedidos.ObterPorCliente(clienteId)
                          .Where(p => !string.IsNullOrEmpty(p.Status) &&
                                      !andamento.Any(fin => p.Status.Contains(fin, System.StringComparison.OrdinalIgnoreCase)))
                          .OrderByDescending(p => p.DataPedido)
                          .Take(1)
                          .ToList()
                : new List<WebApplicationPods.Models.PedidoModel>();

            ViewBag.IsLojista = false;
            return View("Default", ultimoEmAndamento);
        }
    }
}
