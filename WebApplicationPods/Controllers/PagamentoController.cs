using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Hubs;
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
        private readonly ICarrinhoRepository _carrinho; // <<< limpar quando pago
        private readonly BancoContext _db;
        private readonly IConfiguration _cfg;
        private readonly IPaymentCredentialsResolver _creds;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEstoqueService _estoque;
        private readonly IHubContext<PedidosHub> _hub;

        public PagamentoController(
            IPaymentService payments,
            IPedidoRepository pedidos,
            ICarrinhoRepository carrinho,
            BancoContext db,
            IConfiguration cfg,
            IPaymentCredentialsResolver creds,
            UserManager<ApplicationUser> userManager,
            IEstoqueService estoque,
            IHubContext<PedidosHub> hub) // <= novo
        {
            _payments = payments;
            _pedidos = pedidos;
            _carrinho = carrinho;
            _db = db;
            _cfg = cfg;
            _creds = creds;
            _userManager = userManager;
            _estoque = estoque;
            _hub = hub; // <= novo
        }

        // ========= Helpers internos =========

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

        private void MarcarPedidoComoPago(int pedidoId) =>
            _pedidos.AtualizarStatus(pedidoId, "Pago");

        private async Task EnsureBaixaEstoqueAsync(int pedidoId)
        {
            var existeNaoBaixado = await _db.Set<PedidoItemModel>()
                .AnyAsync(i => i.PedidoId == pedidoId && !i.EstoqueBaixado);
            if (existeNaoBaixado)
                await _estoque.BaixarEstoquePedidoAsync(pedidoId);
        }

        // flag de limpeza por sessão/pedido (evita limpar duas vezes)
        private void MarkCartCleared(int pedidoId) =>
            HttpContext?.Session?.SetString($"CartClearedForOrder_{pedidoId}", "1");

        private bool IsCartCleared(int pedidoId) =>
            string.Equals(HttpContext?.Session?.GetString($"CartClearedForOrder_{pedidoId}"), "1", StringComparison.Ordinal);

        private void ClearCartOnceForThisSession(int pedidoId)
        {
            if (!IsCartCleared(pedidoId))
            {
                _carrinho.LimparCarrinho();
                MarkCartCleared(pedidoId);
            }
        }

        // ========= Ações =========

        /// <summary>Checkout: cria/recicla Payment e injeta PublicKey do MP para o Brick.</summary>
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

            // Itens do resumo (carrega se não veio junto)
            var itens = pedido.PedidoItens?.ToList();
            if (itens == null || itens.Count == 0)
            {
                itens = await _db.Set<PedidoItemModel>()
                    .Where(i => i.PedidoId == pedido.Id)
                    .Include(i => i.Produto)
                    .ToListAsync();
            }

            var itensResumo = itens.Select(i =>
            {
                var precoCheio = i.Produto?.Preco ?? i.PrecoUnitario;
                var precoAplicado =
                    (i.Produto?.EmPromocao == true &&
                     i.Produto?.PrecoPromocional is decimal pp &&
                     pp > 0 && pp < precoCheio)
                    ? pp : i.PrecoUnitario;

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

            var subtotal = itensResumo.Sum(x => (decimal)x.Subtotal);
            var subtotalCheio = itensResumo.Sum(x => (decimal)x.SubtotalCheio);
            var desconto = Math.Max(0m, subtotalCheio - subtotal);
            var frete = pedido.TaxaEntrega;
            var total = pedido.ValorTotal > 0 ? pedido.ValorTotal : (subtotal + frete);

            payment.Pedido = pedido;
            ViewBag.Itens = itensResumo;
            ViewBag.PedidoResumo = new { Numero = pedido.Id, Subtotal = subtotal, Frete = frete, Desconto = desconto, Total = total };

            var mpCreds = await _creds.GetAsync<MercadoPagoOptions>(User, "MercadoPago");
            ViewBag.MP_PublicKey = mpCreds?.PublicKey ?? _cfg["Payments:MercadoPago:PublicKey"];

            return View(payment);
        }

        /// <summary>Status do pagamento (polling). Se <c>Paid</c>, marca, baixa estoque e limpa carrinho da sessão.</summary>
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

            if (isPaid)
            {
                if (!string.Equals(p.Pedido?.Status, "Pago", StringComparison.OrdinalIgnoreCase))
                    MarcarPedidoComoPago(p.PedidoId);

                await EnsureBaixaEstoqueAsync(p.PedidoId);

                // <<< limpa carrinho desta sessão (uma única vez)
                ClearCartOnceForThisSession(p.PedidoId);
            }

            return Json(new
            {
                paid = isPaid,
                status = p.Status.ToString(),
                last4 = p.CardLast4,
                brand = p.CardBrand,
                paidAt = p.PaidAt,
                pedidoId = p.PedidoId,
                redirect = isPaid ? Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId }) : null,
                expiresAt = expiresAtUtc?.ToString("o"),
                remainingSeconds
            });
        }


        private Task NotifyPaidAsync(PedidoModel pedido)
        {
            return _hub.Clients.Group("lojistas").SendAsync("NewOrder", new
            {
                id = pedido.Id,
                cliente = pedido.Cliente?.Nome ?? $"Cliente #{pedido.ClienteId}",
                valor = pedido.ValorTotal,
                quando = pedido.DataPedido.ToString("o"),
                metodo = pedido.MetodoPagamento,
                status = "Pago",
                retirada = pedido.RetiradaNoLocal
            });
        }


        /// <summary>Confirmação do cartão (payload do Brick). Marca pago, baixa estoque e limpa carrinho da sessão.</summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCard(int id, [FromBody] object clientPayload)
        {
            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString());

            // pegar pagamento atualizado + pedido
            var p = await _db.Pagamentos
                .Include(x => x.Pedido).ThenInclude(pd => pd.Cliente)
                .FirstOrDefaultAsync(x => x.Id == id);

            string? redirect = null;

            if (ok && p != null)
            {
                // marca pedido como pago (idempotente)
                MarcarPedidoComoPago(p.PedidoId);
                await EnsureBaixaEstoqueAsync(p.PedidoId);
                ClearCartOnceForThisSession(p.PedidoId);

                // 🔔 notifica lojistas agora
                if (p.Pedido != null) await NotifyPaidAsync(p.Pedido);

                redirect = Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId });
            }

            return Ok(new { success = ok, redirect });
        }


        /// <summary>Webhook do provedor (MP). Atualiza o pagamento no banco. Limpeza do carrinho acontece via Status/ConfirmCard/Confirmacao.</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            await _payments.ApplyWebhookAsync(Request);
            return Ok();
        }

        /// <summary>Cancelar pagamento enquanto não pago.</summary>
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

                if (p.Pedido != null)
                {
                    p.Pedido.Status = "Cancelado";
                    p.Pedido.DataCancelado = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
            }

            return Json(new { ok = true, redirect = Url.Action("Index", "Carrinho") });
        }

        /// <summary>Aprovação manual do lojista (Dinheiro/Débito na entrega).</summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Lojista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprovarPagamentoEntrega(int pedidoId)
        {
            var pedido = await _db.Pedidos
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);
            if (pedido == null) return NotFound();

            // evita online
            if (!string.Equals(pedido.MetodoPagamento, "dinheiro", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.MetodoPagamento, "cartão de débito", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.MetodoPagamento, "cartao de debito", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Este pedido não é de pagamento na entrega.";
                return RedirectToAction("DetalhesPedido", "Admin", new { id = pedidoId });
            }

            _pedidos.AtualizarStatus(pedidoId, "Pago"); // ou "Aprovado pelo Lojista" se preferir
            await EnsureBaixaEstoqueAsync(pedidoId);

            // 🔔 notifica lojistas
            await NotifyPaidAsync(pedido);

            TempData["Sucesso"] = "Pedido aprovado e estoque atualizado.";
            return RedirectToAction("Index", "PedidosAdmin", new { id = pedidoId });
        }


        /// <summary>Confirma PIX manual (backoffice).</summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Lojista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPixManual(int pagamentoId)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido).ThenInclude(pd => pd.Cliente)
                .FirstOrDefaultAsync(x => x.Id == pagamentoId);

            if (p == null)
            {
                TempData["Erro"] = "Pagamento não encontrado.";
                return RedirectToAction("Index", "Admin");
            }

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

            p.Status = PaymentStatus.Paid;
            p.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _pedidos.AtualizarStatus(p.PedidoId, "Pago");
            await EnsureBaixaEstoqueAsync(p.PedidoId);

            // 🔔 notifica
            if (p.Pedido != null) await NotifyPaidAsync(p.Pedido);

            TempData["Sucesso"] = "PIX confirmado e estoque atualizado.";
            return RedirectToAction("DetalhesPedido", "Admin", new { id = p.PedidoId });
        }

    }
}
