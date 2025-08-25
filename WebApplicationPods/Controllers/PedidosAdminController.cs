using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Repository.Interface;
using System.Linq;

[Authorize(Roles = "Lojista")]
public class PedidosAdminController : Controller
{
    private readonly IPedidoRepository _pedidos;

    public PedidosAdminController(IPedidoRepository pedidos)
    {
        _pedidos = pedidos;
    }

    // GET /PedidosAdmin
    [HttpGet]
    public IActionResult Index()
    {
        // Liste do mais novo para o mais antigo (pode filtrar por hoje se quiser)
        // Se tiver método específico, use; senão carregue tudo e ordene.
        var lista = _pedidos.ObterPorCliente(0); // placeholder se você não tiver "todos".
        // Se não existir "todos", crie um método para buscar todos os pedidos ou filtre por DataPedido >= hoje.
        // Exemplo rápido (caso você prefira no repositório depois):
        // var lista = _db.Pedidos.Where(p => p.DataPedido.Date == DateTime.Today).OrderByDescending(p => p.Id).ToList();

        return View(lista);
    }

    // POST /PedidosAdmin/AtualizarStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarStatus(int id, string status)
    {
        _pedidos.AtualizarStatus(id, status);
        return RedirectToAction(nameof(Index));
    }
}
