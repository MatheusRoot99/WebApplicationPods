using Microsoft.AspNetCore.Mvc;
using SitePodsInicial.Services.Interface;
using SitePodsInicial.Services.service;

namespace SitePodsInicial.Components
{
    public class CarrinhoResumoViewComponent : ViewComponent
    {
        private readonly ICarrinhoService _carrinhoService;

        public CarrinhoResumoViewComponent(ICarrinhoService carrinhoService)
        {
            _carrinhoService = carrinhoService;
        }

        public IViewComponentResult Invoke()
        {
            // Versão simplificada que retorna 0 - você deve substituir pela lógica real
            return View(0);
        }
    }
}
