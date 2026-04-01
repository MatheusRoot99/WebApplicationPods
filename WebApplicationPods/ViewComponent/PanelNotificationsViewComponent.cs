using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

public class PanelNotificationsViewComponent : ViewComponent
{
    private readonly INotificationAppService _notificationAppService;
    private readonly ICurrentLojaService _currentLoja;

    public PanelNotificationsViewComponent(
        INotificationAppService notificationAppService,
        ICurrentLojaService currentLoja)
    {
        _notificationAppService = notificationAppService;
        _currentLoja = currentLoja;
    }

    public async Task<IViewComponentResult> InvokeAsync(int take = 8)
    {
        var user = HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || !user.IsInRole("Lojista"))
            return View("Default", new List<NotificacaoModel>());

        var lojaId = ObterLojaAtual();
        if (!lojaId.HasValue)
            return View("Default", new List<NotificacaoModel>());

        var lista = await _notificationAppService.ObterRecentesAsync(lojaId.Value, take, incluirLidas: true);
        return View("Default", lista);
    }

    private int? ObterLojaAtual()
    {
        if (_currentLoja?.LojaId is int lojaAtual && lojaAtual > 0)
            return lojaAtual;

        var claimLojaId = HttpContext?.User?.FindFirst("LojaId")?.Value
                       ?? HttpContext?.User?.FindFirst("lojaId")?.Value;

        if (int.TryParse(claimLojaId, out var lojaIdClaim) && lojaIdClaim > 0)
            return lojaIdClaim;

        return null;
    }
}