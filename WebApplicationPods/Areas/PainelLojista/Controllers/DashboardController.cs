using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("Painel")]
    [Authorize(Roles = "Lojista")]
    public class DashboardController : Controller
    {
        public IActionResult Index() => View();
    }
}
