using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;
using WebApplicationPods.ViewModels;

namespace WebApplicationPods.Components
{
    public class AdminLojaPickerViewComponent : ViewComponent
    {
        private readonly TenantDbContext _tenantDb;
        private readonly ICurrentLojaService _currentLoja;

        public AdminLojaPickerViewComponent(TenantDbContext tenantDb, ICurrentLojaService currentLoja)
        {
            _tenantDb = tenantDb;
            _currentLoja = currentLoja;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var lojas = await _tenantDb.Lojas
                .AsNoTracking()
                .OrderBy(l => l.Nome)
                .Select(l => new { l.Id, l.Nome, l.Ativa })
                .ToListAsync();

            var currentId = _currentLoja.LojaId;
            var currentName = currentId.HasValue
                ? lojas.FirstOrDefault(x => x.Id == currentId.Value)?.Nome
                : null;

            var items = lojas.Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Ativa ? x.Nome : $"{x.Nome} (inativa)",
                Selected = currentId.HasValue && x.Id == currentId.Value
            }).ToList();

            var returnUrl =
                (HttpContext?.Request?.Path.Value ?? "/Admin") +
                (HttpContext?.Request?.QueryString.ToUriComponent() ?? "");

            var vm = new AdminLojaPickerViewModel
            {
                CurrentLojaId = currentId,
                CurrentLojaNome = currentName,
                Lojas = items,
                ReturnUrl = returnUrl
            };

            return View(vm);
        }
    }
}
