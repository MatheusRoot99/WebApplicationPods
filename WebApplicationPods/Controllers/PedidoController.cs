using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Data;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Controllers
{
    public class PedidoController : Controller
    {
        private readonly BancoContext _context;
        private readonly IPedidoRepository _pedidoRepository;


        public PedidoController(BancoContext context, IPedidoRepository pedidoRepository)
        {
            _pedidoRepository = pedidoRepository;
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        // GET /Pedido/Acompanhar/123
        [HttpGet]
        public IActionResult Acompanhar(int id)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            return View(pedido);
        }

        // GET /Pedido/Status?id=123  -> usado pelo polling da view
        [HttpGet]
        public IActionResult Status(int id)
        {
            var pedido = _pedidoRepository.ObterPorId(id);
            if (pedido == null) return NotFound();

            return Json(new
            {
                status = pedido.Status,
                total = pedido.ValorTotal,
                atualizado = pedido.DataPedido
            });
        }
    }
}
