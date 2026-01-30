using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("PainelLojista")]
    [Authorize(Roles = "Lojista")]
    public class DashboardController : Controller
    {
        public IActionResult Index() => View();
    }
}
