using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Services;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers.Admin
{
    [Authorize(Policy = "Admin")]
    public class LojaSwitchController : Controller
    {
        private readonly ICurrentLojaService _currentLoja;

        public LojaSwitchController(ICurrentLojaService currentLoja)
        {
            _currentLoja = currentLoja;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(int lojaId, string? returnUrl = null)
        {
            _currentLoja.SetLojaId(lojaId);
            if (!string.IsNullOrWhiteSpace(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear(string? returnUrl = null)
        {
            _currentLoja.ClearLoja();
            if (!string.IsNullOrWhiteSpace(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}
