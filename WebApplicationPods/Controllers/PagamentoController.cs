

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Enum;
using WebApplicationPods.Payments;
using WebApplicationPods.Repository.Interface;

public class PagamentoController : Controller
{
    private readonly IPaymentService _payments;
    private readonly IPedidoRepository _pedidos;

    public PagamentoController(IPaymentService payments, IPedidoRepository pedidos)
    {
        _payments = payments; _pedidos = pedidos;
    }

    // Após Finalizar pedido, redirecione aqui
    public async Task<IActionResult> Checkout(int pedidoId)
    {
        var pedido = _pedidos.ObterPorId(pedidoId);
        if (pedido == null) return NotFound();

        // Se já existir Payment associado e pendente, reusa (opcional)
        // Caso contrário, cria de acordo com pedido.MetodoPagamento
        PaymentMethod metodo = pedido.MetodoPagamento switch
        {
            "Dinheiro" => PaymentMethod.Cash,
            "Pix" => PaymentMethod.Pix,
            "Cartão de Crédito" => PaymentMethod.CardCredit,
            "Cartão de Débito" => PaymentMethod.CardDebit,
            _ => PaymentMethod.Cash
        };

        var existing = /* busque Payment do pedido se quiser reutilizar */;
        var payment = existing ?? await _payments.StartPaymentAsync(pedido, metodo);

        // View escolhe layout por método/status
        return View(payment);
    }

    // Confirmação do cartão (recebe token/nonce do front)
    [HttpPost]
    public async Task<IActionResult> ConfirmCard(int id /*paymentId*/, [FromBody] object clientPayload)
    {
        var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString());
        return Ok(new { success = ok });
    }

    // Webhook do provedor (URL pública)
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        await _payments.ApplyWebhookAsync(Request);
        return Ok(); // responda 200 para o provedor
    }
}
