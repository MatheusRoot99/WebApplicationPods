using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista")]
    public class NotificacoesController : Controller
    {
        private readonly INotificationAppService _notificationAppService;
        private readonly ICurrentLojaService _currentLoja;

        public NotificacoesController(
            INotificationAppService notificationAppService,
            ICurrentLojaService currentLoja)
        {
            _notificationAppService = notificationAppService;
            _currentLoja = currentLoja;
        }

        [HttpGet]
        public async Task<IActionResult> Index(bool somenteNaoLidas = false)
        {
            var lojaId = ObterLojaAtual();
            if (!lojaId.HasValue)
            {
                TempData["Erro"] = "Loja atual não encontrada.";
                return RedirectToAction("Index", "PedidosAdmin");
            }

            var lista = await _notificationAppService.ObterCentralAsync(
                lojaId.Value,
                somenteNaoLidas,
                take: 100);

            ViewBag.SomenteNaoLidas = somenteNaoLidas;
            ViewBag.TotalNaoLidas = await _notificationAppService.ContarNaoLidasAsync(lojaId.Value);

            return View("~/Views/Notificacoes/Index.cshtml", lista);
        }

        [HttpGet]
        public async Task<IActionResult> IrPara(int id)
        {
            var lojaId = ObterLojaAtual();
            if (!lojaId.HasValue)
                return RedirectToAction(nameof(Index));

            var notificacao = await _notificationAppService.ObterPorIdAsync(id, lojaId.Value);
            if (notificacao == null)
                return RedirectToAction(nameof(Index));

            await _notificationAppService.MarcarComoLidaAsync(id, lojaId.Value);

            if (notificacao.PedidoId.HasValue)
            {
                return RedirectToAction("Index", "PedidosAdmin", new { filtro = "dia" });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarComoLida(int id, string? returnUrl = null)
        {
            var lojaId = ObterLojaAtual();
            if (lojaId.HasValue)
            {
                await _notificationAppService.MarcarComoLidaAsync(id, lojaId.Value);
            }

            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarTodasComoLidas(string? returnUrl = null)
        {
            var lojaId = ObterLojaAtual();
            if (lojaId.HasValue)
            {
                await _notificationAppService.MarcarTodasComoLidasAsync(lojaId.Value);
                TempData["Sucesso"] = "Notificações marcadas como lidas.";
            }

            return RedirectToLocalOrIndex(returnUrl);
        }

        private int? ObterLojaAtual()
        {
            if (_currentLoja?.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            var claimLojaId = User.FindFirst("LojaId")?.Value
                           ?? User.FindFirst("lojaId")?.Value;

            if (int.TryParse(claimLojaId, out var lojaIdClaim) && lojaIdClaim > 0)
                return lojaIdClaim;

            return null;
        }

        private IActionResult RedirectToLocalOrIndex(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}