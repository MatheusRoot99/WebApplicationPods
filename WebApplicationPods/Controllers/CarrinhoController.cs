using Microsoft.AspNetCore.Mvc;


namespace SitePodsInicial.Controllers
{
    public class CarrinhoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
