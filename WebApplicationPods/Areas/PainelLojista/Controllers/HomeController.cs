using Microsoft.AspNetCore.Mvc;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("PainelLojista")]
    public class PedidosAdminController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/PedidosAdmin/Index.cshtml");
        }
    }
}