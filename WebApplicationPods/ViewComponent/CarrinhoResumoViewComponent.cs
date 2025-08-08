using Microsoft.AspNetCore.Mvc;
using SitePodsInicial.Services.Interface;

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
            var quantidadeItens = _carrinhoService.ObterQuantidadeTotalItens();
            return View(quantidadeItens);
        }
    }
}
