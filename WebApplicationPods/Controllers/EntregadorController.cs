using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Entregador,Lojista")]
    public class EntregadorController : Controller
    {
        private readonly BancoContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEntregaAppService _entregaAppService;

        public EntregadorController(
            BancoContext context,
            UserManager<ApplicationUser> userManager,
            IEntregaAppService entregaAppService)
        {
            _context = context;
            _userManager = userManager;
            _entregaAppService = entregaAppService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var pedidos = await _context.Pedidos
                .Include(x => x.Cliente)
                .Include(x => x.Endereco)
                .Include(x => x.Entregador)
                    .ThenInclude(x => x!.Usuario)
                .Include(x => x.Entrega)
                .Where(x => !x.IsDeleted &&
                            x.Entregador != null &&
                            x.Entregador.Usuario != null &&
                            x.Entregador.Usuario.Id == user.Id)
                .OrderByDescending(x => x.DataPedido)
                .ToListAsync();

            return View(pedidos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aceitar(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var ok = await _entregaAppService.AceitarEntregaAsync(id, user.Id);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Entrega aceita com sucesso."
                : "Não foi possível aceitar a entrega.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Coletada(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var ok = await _entregaAppService.MarcarColetadaAsync(id, user.Id);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Pedido marcado como coletado."
                : "Não foi possível marcar a coleta.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaiuParaEntrega(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var ok = await _entregaAppService.MarcarSaiuParaEntregaAsync(id, user.Id);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Pedido marcado como saiu para entrega."
                : "Não foi possível alterar o status do pedido.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Entregue(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var ok = await _entregaAppService.MarcarEntregueAsync(id, user.Id);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Pedido marcado como entregue."
                : "Não foi possível concluir a entrega.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NaoEntregue(int id, string? motivo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var ok = await _entregaAppService.MarcarNaoEntregueAsync(id, user.Id, motivo);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Entrega marcada como não concluída."
                : "Não foi possível atualizar a entrega.";

            return RedirectToAction(nameof(Index));
        }
    }
}