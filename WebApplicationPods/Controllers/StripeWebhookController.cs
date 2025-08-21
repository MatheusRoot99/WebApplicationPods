using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Payments;

namespace WebApplicationPods.Controllers
{
    public class StripeWebhookController : Controller
    {
        private readonly IPaymentService _payments;

        public StripeWebhookController(IPaymentService payments) => _payments = payments;

        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            try
            {
                await _payments.ApplyWebhookAsync(Request);
                return Ok(); // 200 => Stripe considera entregue
            }
            catch (Exception)
            {
                // Deixe 400/500 para Stripe re-tentar (idempotência é importante)
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
