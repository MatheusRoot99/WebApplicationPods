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
        private readonly IWebHostEnvironment _hostEnvironment;

        public EntregadorController(
            BancoContext context,
            UserManager<ApplicationUser> userManager,
            IEntregaAppService entregaAppService,
            IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _entregaAppService = entregaAppService;
            _hostEnvironment = hostEnvironment;
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
        public async Task<IActionResult> Entregue(EntregaConclusaoViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (model == null || model.Id <= 0)
            {
                TempData["Erro"] = "Entrega inválida.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.NomeRecebedor))
            {
                TempData["Erro"] = "Informe o nome de quem recebeu.";
                return RedirectToAction(nameof(Index));
            }

            string? comprovanteUrl = null;

            if (model.FotoComprovante != null && model.FotoComprovante.Length > 0)
            {
                var error = ValidateDeliveryImage(model.FotoComprovante, out var extLower);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    TempData["Erro"] = error;
                    return RedirectToAction(nameof(Index));
                }

                comprovanteUrl = await SaveDeliveryImageAndReturnUrl(model.FotoComprovante, model.Id, extLower);
            }

            var ok = await _entregaAppService.MarcarEntregueAsync(
                model.Id,
                user.Id,
                model.NomeRecebedor,
                model.ObservacaoEntrega,
                comprovanteUrl);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Pedido marcado como entregue com comprovante."
                : "Não foi possível concluir a entrega.";

            return RedirectToAction(nameof(Index));
        }

        private static string? ValidateDeliveryImage(IFormFile file, out string extLower)
        {
            extLower = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowed.Contains(extLower))
                return "A foto do comprovante deve estar em JPG, JPEG, PNG ou WEBP.";

            if (file.Length > 5 * 1024 * 1024)
                return "A foto do comprovante não pode exceder 5MB.";

            return null;
        }

        private async Task<string> SaveDeliveryImageAndReturnUrl(IFormFile file, int pedidoId, string extLower)
        {
            var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "comprovantes-entrega");
            Directory.CreateDirectory(pastaUploads);

            var fileName = MakeDeliveryFileName(pedidoId, extLower);
            var caminho = Path.Combine(pastaUploads, fileName);

            using var fs = new FileStream(caminho, FileMode.Create);
            await file.CopyToAsync(fs);

            return $"/uploads/comprovantes-entrega/{fileName}";
        }

        private static string MakeDeliveryFileName(int pedidoId, string extLower)
        {
            var guid8 = Guid.NewGuid().ToString("N")[..8];
            return $"pedido-{pedidoId}-comprovante-{guid8}{extLower}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NaoEntregue(int id, string? motivo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Erro"] = "Informe o motivo da tentativa sem sucesso.";
                return RedirectToAction(nameof(Index));
            }

            var ok = await _entregaAppService.MarcarNaoEntregueAsync(id, user.Id, motivo);

            TempData[ok ? "Sucesso" : "Erro"] = ok
                ? "Entrega marcada como não concluída e devolvida para nova atribuição."
                : "Não foi possível atualizar a entrega.";

            return RedirectToAction(nameof(Index));
        }
    }
}