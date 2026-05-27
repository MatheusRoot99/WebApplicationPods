using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Models;

[Authorize(Roles = "Lojista")]
public class PagamentosAdminController : Controller
{
    [HttpGet]
    public IActionResult Edit(string provider = "Stripe")
    {
        return RedirectToAction("Edit", "PaymentConfig", new { provider });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(PaymentConfigEditViewModel vm)
    {
        return RedirectToAction("Edit", "PaymentConfig", new { provider = vm.Provider });
    }
}