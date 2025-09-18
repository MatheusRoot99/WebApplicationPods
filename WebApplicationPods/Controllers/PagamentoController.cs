using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers
{
    public class PagamentoController : Controller
    {
        private readonly IPaymentService _payments;
        private readonly IPedidoRepository _pedidos;
        private readonly BancoContext _db;
        private readonly IConfiguration _cfg;
        private readonly IPaymentCredentialsResolver _creds;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEstoqueService _estoque;  // <<< baixa de estoque

        public PagamentoController(
            IPaymentService payments,
            IPedidoRepository pedidos,
            BancoContext db,
            IConfiguration cfg,
            IPaymentCredentialsResolver creds,
            UserManager<ApplicationUser> userManager,
            IEstoqueService estoque)
        {
            _payments = payments;
            _pedidos = pedidos;
            _db = db;
            _cfg = cfg;
            _creds = creds;
            _userManager = userManager;
            _estoque = estoque;
        }

        /// <summary>Checkout: cria/usa Payment e manda a PublicKey do MP para o Brick.</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Checkout(int pedidoId)
        {
            var pedido = _pedidos.ObterPorId(pedidoId);
            if (pedido == null) return NotFound();

            var metodo = MapMetodo(pedido.MetodoPagamento);

            var existing = await _db.Pagamentos
                .Where(p => p.PedidoId == pedido.Id && p.Metodo == metodo)
                .Where(p => p.Status != PaymentStatus.Paid &&
                            p.Status != PaymentStatus.Canceled &&
                            p.Status != PaymentStatus.Failed)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            var payment = existing ?? await _payments.StartPaymentAsync(pedido, metodo);

            // ====== Itens para o resumo (considera promo do Produto) ======
            var itensPedido = pedido.PedidoItens?.ToList();
            if (itensPedido == null || itensPedido.Count == 0)
            {
                itensPedido = await _db.Set<PedidoItemModel>()
                    .Where(i => i.PedidoId == pedido.Id)
                    .Include(i => i.Produto)
                    .ToListAsync();
            }

            var itensResumo = itensPedido.Select(i =>
            {
                var precoCheio = i.Produto?.Preco ?? i.PrecoUnitario;
                decimal precoAplicado =
                    (i.Produto?.EmPromocao == true &&
                     i.Produto?.PrecoPromocional is decimal pProm &&
                     pProm > 0 && pProm < precoCheio)
                    ? pProm
                    : i.PrecoUnitario;

                return new
                {
                    Nome = i.Produto?.Nome ?? $"Item #{i.ProdutoId}",
                    Qtd = i.Quantidade,
                    Preco = precoAplicado,
                    PrecoCheio = precoCheio,
                    Subtotal = precoAplicado * i.Quantidade,
                    SubtotalCheio = precoCheio * i.Quantidade,
                    Img = i.Produto?.ImagemUrl
                };
            }).ToList();

            decimal subtotal = itensResumo.Sum(x => (decimal)x.Subtotal);
            decimal subtotalCheio = itensResumo.Sum(x => (decimal)x.SubtotalCheio);
            decimal desconto = Math.Max(0m, subtotalCheio - subtotal);
            decimal frete = pedido.TaxaEntrega;
            decimal total = pedido.ValorTotal > 0 ? pedido.ValorTotal : (subtotal + frete);

            payment.Pedido = pedido;
            ViewBag.Itens = itensResumo;
            ViewBag.PedidoResumo = new
            {
                Numero = pedido.Id,
                Subtotal = subtotal,
                Frete = frete,
                Desconto = desconto,
                Total = total
            };

            var mpCreds = await _creds.GetAsync<MercadoPagoOptions>(User, "MercadoPago");
            ViewBag.MP_PublicKey = mpCreds?.PublicKey ?? _cfg["Payments:MercadoPago:PublicKey"];

            return View(payment);
        }

        private void MarcarPedidoComoPago(int pedidoId) =>
            _pedidos.AtualizarStatus(pedidoId, "Pago");

        /// <summary>Garante a baixa de estoque se ainda não foi baixado.</summary>
        private async Task EnsureBaixaEstoqueAsync(int pedidoId)
        {
            var existeNaoBaixado = await _db.Set<PedidoItemModel>()
                .AnyAsync(i => i.PedidoId == pedidoId && !i.EstoqueBaixado);
            if (existeNaoBaixado)
                await _estoque.BaixarEstoquePedidoAsync(pedidoId);
        }

        /// <summary>Polling de status (usado pelo front). Se pago, marca pedido e baixa estoque.</summary>
        // GET /Pagamento/Status?id=123
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Status(int id)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            bool IsTerminal(PaymentStatus s) =>
                s == PaymentStatus.Paid || s == PaymentStatus.Failed || s == PaymentStatus.Canceled;

            // Timeout de PIX
            var timeoutMinutes = 15;
            if (int.TryParse(_cfg["Payments:Pix:TimeoutMinutes"], out var cfgMin) && cfgMin > 0)
                timeoutMinutes = cfgMin;

            DateTime? expiresAtUtc = null;
            int remainingSeconds = 0;

            if (p.Metodo == PaymentMethod.Pix && !IsTerminal(p.Status))
            {
                expiresAtUtc = p.CreatedAt.AddMinutes(timeoutMinutes);
                var now = DateTime.UtcNow;

                if (now >= expiresAtUtc.Value)
                {
                    p.Status = PaymentStatus.Canceled;
                    p.CanceledAt = now;
                    await _db.SaveChangesAsync();

                    _pedidos.AtualizarStatus(p.PedidoId, "Cancelado por expiração (PIX)");
                }
                else
                {
                    remainingSeconds = (int)Math.Max(0, (expiresAtUtc.Value - now).TotalSeconds);
                }
            }

            var isPaid = p.Status == PaymentStatus.Paid;

            // ✅ Se pago, marca e baixa estoque (idempotente)
            if (isPaid)
            {
                if (!string.Equals(p.Pedido?.Status, "Pago", StringComparison.OrdinalIgnoreCase))
                    MarcarPedidoComoPago(p.PedidoId);

                await EnsureBaixaEstoqueAsync(p.PedidoId);
            }

            return Json(new
            {
                paid = isPaid,
                status = p.Status.ToString(),
                last4 = p.CardLast4,
                brand = p.CardBrand,
                paidAt = p.PaidAt,
                pedidoId = p.PedidoId,
                redirect = isPaid
                    ? Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId })
                    : null,
                expiresAt = expiresAtUtc?.ToString("o"),
                remainingSeconds
            });
        }

        /// <summary>Confirmação do cartão (payload do Brick). Marca pago e baixa estoque.</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCard(int id, [FromBody] object clientPayload)
        {
            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString());

            var p = await _db.Pagamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            string? redirect = null;

            if (ok && p != null)
            {
                MarcarPedidoComoPago(p.PedidoId);
                await EnsureBaixaEstoqueAsync(p.PedidoId);        // ✅ baixa aqui
                redirect = Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId });
            }

            return Ok(new { success = ok, redirect });
        }

        /// <summary>Webhook do provedor (MP). O serviço aplica o update de status.</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            // Só processa o payload e atualiza o pagamento no banco
            await _payments.ApplyWebhookAsync(Request);

            // NÃO tenta ler paymentId aqui. A baixa acontecerá via:
            // - /Pagamento/Status (polling do front) quando ficar Paid, e/ou
            // - ConfirmCard (cartão), que já chama EnsureBaixaEstoqueAsync

            return Ok();
        }



        private static PaymentMethod MapMetodo(string metodo)
        {
            if (string.IsNullOrWhiteSpace(metodo)) return PaymentMethod.Cash;
            var s = metodo.Trim().ToLowerInvariant();
            return s switch
            {
                "dinheiro" => PaymentMethod.Cash,
                "pix" => PaymentMethod.Pix,
                "cartão de crédito" or "cartao de credito" => PaymentMethod.CardCredit,
                "cartão de débito" or "cartao de debito" => PaymentMethod.CardDebit,
                _ => PaymentMethod.Cash
            };
        }

        // POST /Pagamento/Cancelar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null)
                return Json(new { ok = false, message = "Pagamento não encontrado." });

            if (p.Status == PaymentStatus.Paid)
                return Json(new { ok = false, message = "Pagamento já aprovado; não é possível cancelar." });

            if (p.Status != PaymentStatus.Canceled)
            {
                p.Status = PaymentStatus.Canceled;
                p.CanceledAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _pedidos.AtualizarStatus(p.PedidoId, "Cancelado");
            }

            return Json(new { ok = true, redirect = Url.Action("Index", "Carrinho") });
        }

        /// <summary>
        /// ✅ Aprovação manual do lojista (Dinheiro / Débito na entrega).
        /// Marca pedido como "Aprovado pelo Lojista" e dá baixa de estoque.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Lojista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprovarPagamentoEntrega(int pedidoId)
        {
            var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId);
            if (pedido == null) return NotFound();

            // Evita aprovar pagamentos online por aqui
            if (!string.Equals(pedido.MetodoPagamento, "dinheiro", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.MetodoPagamento, "cartão de débito", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.MetodoPagamento, "cartao de debito", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Este pedido não é de pagamento na entrega.";
                return RedirectToAction("DetalhesPedido", "Admin", new { id = pedidoId });
            }

            _pedidos.AtualizarStatus(pedidoId, "Aprovado pelo Lojista");
            await EnsureBaixaEstoqueAsync(pedidoId);  // ✅ baixa aqui

            TempData["Sucesso"] = "Pedido aprovado e estoque atualizado.";
            return RedirectToAction("Index", "PedidosAdmin", new { id = pedidoId });
        }

        // PagamentoController
        [HttpPost]
        [Authorize(Roles = "Admin,Lojista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPixManual(int pagamentoId)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == pagamentoId);

            if (p == null)
            {
                TempData["Erro"] = "Pagamento não encontrado.";
                return RedirectToAction("Index", "Admin"); // ajuste sua rota de retorno
            }

            // Valida que é PIX manual e ainda pendente
            if (p.Metodo != PaymentMethod.Pix || !string.Equals(p.Provider, "PixManual", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Este pagamento não é PIX manual.";
                return RedirectToAction("DetalhesPedido", "Admin", new { id = p.PedidoId });
            }
            if (p.Status == PaymentStatus.Paid)
            {
                TempData["Sucesso"] = "Pagamento já está aprovado.";
                return RedirectToAction("DetalhesPedido", "Admin", new { id = p.PedidoId });
            }

            // Marca como pago e baixa estoque
            p.Status = PaymentStatus.Paid;
            p.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _pedidos.AtualizarStatus(p.PedidoId, "Pago");
            await EnsureBaixaEstoqueAsync(p.PedidoId); // usa seu método idempotente

            TempData["Sucesso"] = "PIX confirmado e estoque atualizado.";
            return RedirectToAction("DetalhesPedido", "Admin", new { id = p.PedidoId });
        }
    }
}
