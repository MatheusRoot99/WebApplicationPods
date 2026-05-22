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
using System.Security.Cryptography;
using System.Text;

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
        private readonly IPedidoAppService _pedidoAppService;

        public PagamentoController(
            IPaymentService payments,
            IPedidoRepository pedidos,
            ICarrinhoRepository carrinho,
            BancoContext db,
            IConfiguration cfg,
            IPaymentCredentialsResolver creds,
            UserManager<ApplicationUser> userManager,
            IEstoqueService estoque,
            IHubContext<PedidosHub> hub,
            IPedidoAppService pedidoAppService) // <= novo
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
            _pedidoAppService = pedidoAppService;
        }

        // ========= Helpers internos =========

        private bool ValidarAssinaturaMercadoPago(HttpRequest request)
        {
            var secret = _cfg["Payments:MercadoPago:WebhookSecret"];

            if (string.IsNullOrWhiteSpace(secret))
                return false;

            var xSignature = request.Headers["x-signature"].FirstOrDefault();
            var xRequestId = request.Headers["x-request-id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(xSignature) || string.IsNullOrWhiteSpace(xRequestId))
                return false;

            var partes = xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries);

            string? ts = null;
            string? v1 = null;

            foreach (var parte in partes)
            {
                var keyValue = parte.Split('=', 2, StringSplitOptions.TrimEntries);

                if (keyValue.Length != 2)
                    continue;

                if (string.Equals(keyValue[0], "ts", StringComparison.OrdinalIgnoreCase))
                    ts = keyValue[1];

                if (string.Equals(keyValue[0], "v1", StringComparison.OrdinalIgnoreCase))
                    v1 = keyValue[1];
            }

            if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(v1))
                return false;

            if (!TimestampMercadoPagoEstaDentroDaTolerancia(ts))
                return false;

            var dataId = request.Query["data.id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(dataId))
                dataId = request.Query["id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(dataId))
                return false;

            var manifest = $"id:{dataId};request-id:{xRequestId};ts:{ts};";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            return ComparacaoSegura(hash, v1);
        }

        private static bool TimestampMercadoPagoEstaDentroDaTolerancia(string ts)
        {
            if (!long.TryParse(ts, out var timestamp))
                return false;

            DateTimeOffset enviadoEm;

            try
            {
                enviadoEm = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            }
            catch
            {
                return false;
            }

            var agora = DateTimeOffset.UtcNow;
            var diferenca = agora - enviadoEm;

            return diferenca.Duration() <= TimeSpan.FromMinutes(10);
        }

        private static bool ComparacaoSegura(string valorCalculado, string valorRecebido)
        {
            var a = Encoding.UTF8.GetBytes(valorCalculado);
            var b = Encoding.UTF8.GetBytes(valorRecebido);

            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
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

        private Task<bool> MarcarPedidoComoPago(int pedidoId) => _pedidoAppService.MarcarComoPagoAsync(
        pedidoId,
        origem: "PagamentoController");

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
        public async Task<IActionResult> Checkout(int pedidoId, string? t = null)
        {
            var pedido = _pedidos.ObterPorId(pedidoId);
            if (pedido == null) return NotFound();

            if (!PodeAcessarPedido(pedido, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction("Buscar", "Pedido");
            }

            ViewBag.OrderToken = pedido.RastreioToken;

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
        public async Task<IActionResult> Status(int id, string? t = null)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null)
                return NotFound();

            if (!PodeAcessarPedido(p.Pedido, t))
                return Unauthorized(new { paid = false, status = "Unauthorized" });

            bool IsTerminal(PaymentStatus s) =>
                s == PaymentStatus.Paid ||
                s == PaymentStatus.Failed ||
                s == PaymentStatus.Canceled;

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

                    await _pedidoAppService.AtualizarStatusAsync(
                        p.PedidoId,
                        "Cancelado",
                        observacao: "Pagamento PIX expirado automaticamente.",
                        origem: "PagamentoController.Status");
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
                    await MarcarPedidoComoPago(p.PedidoId);

                await EnsureBaixaEstoqueAsync(p.PedidoId);

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
                redirect = isPaid
                    ? Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId, t = p.Pedido?.RastreioToken })
                    : null,
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
        public async Task<IActionResult> ConfirmCard(int id, string? t, [FromBody] object clientPayload)
        {
            var pagamentoAntes = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (pagamentoAntes == null)
                return NotFound(new { success = false, message = "Pagamento não encontrado." });

            if (!PodeAcessarPedido(pagamentoAntes.Pedido, t))
                return Unauthorized(new { success = false, message = "Pedido não autorizado." });

            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString() ?? string.Empty);

            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .ThenInclude(pd => pd.Cliente)
                .FirstOrDefaultAsync(x => x.Id == id);

            string? redirect = null;

            if (ok && p != null)
            {
                await MarcarPedidoComoPago(p.PedidoId);
                await EnsureBaixaEstoqueAsync(p.PedidoId);
                ClearCartOnceForThisSession(p.PedidoId);

                if (p.Pedido != null)
                    await NotifyPaidAsync(p.Pedido);

                redirect = Url.Action("Confirmacao", "Carrinho", new
                {
                    id = p.PedidoId,
                    t = p.Pedido?.RastreioToken
                });
            }

            return Ok(new { success = ok, redirect });
        }


        /// <summary>Webhook do provedor (MP). Atualiza o pagamento no banco. Limpeza do carrinho acontece via Status/ConfirmCard/Confirmacao.</summary>
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            if (!ValidarAssinaturaMercadoPago(Request))
            {
                return Unauthorized(new
                {
                    ok = false,
                    message = "Assinatura do Mercado Pago inválida."
                });
            }

            await _payments.ApplyWebhookAsync(Request);

            return Ok(new
            {
                ok = true
            });
        }

        /// <summary>Cancelar pagamento enquanto não pago.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id, string? t = null)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null)
                return Json(new { ok = false, message = "Pagamento não encontrado." });

            if (!PodeAcessarPedido(p.Pedido, t))
                return Unauthorized(new { ok = false, message = "Pedido não autorizado." });

            if (p.Status == PaymentStatus.Paid)
                return Json(new { ok = false, message = "Pagamento já aprovado; não é possível cancelar." });

            if (p.Status != PaymentStatus.Canceled)
            {
                p.Status = PaymentStatus.Canceled;
                p.CanceledAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                await _pedidoAppService.AtualizarStatusAsync(
                    p.PedidoId,
                    "Cancelado",
                    observacao: "Pagamento cancelado pelo usuário.",
                    origem: "PagamentoController.Cancelar");
            }

            return Json(new
            {
                ok = true,
                redirect = Url.Action("Index", "Carrinho")
            });
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

            await _pedidoAppService.MarcarComoPagoAsync(pedidoId,origem: "PagamentoController"); // ou "Aprovado pelo Lojista" se preferir
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

            await _pedidoAppService.MarcarComoPagoAsync(
                p.PedidoId,
                origem: "PagamentoController.ConfirmarPixManual");

            await EnsureBaixaEstoqueAsync(p.PedidoId);

            if (p.Pedido != null)
                await NotifyPaidAsync(p.Pedido);

            TempData["Sucesso"] = "PIX confirmado e estoque atualizado.";
            return RedirectToAction("DetalhesPedido", "Admin", new { id = p.PedidoId });
        }

        private bool PodeAcessarPedido(PedidoModel? pedido, string? token)
        {
            if (pedido == null)
                return false;

            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin") || User.IsInRole("Lojista"))
                    return true;
            }

            return !string.IsNullOrWhiteSpace(token) &&
                   !string.IsNullOrWhiteSpace(pedido.RastreioToken) &&
                   string.Equals(pedido.RastreioToken, token, StringComparison.OrdinalIgnoreCase);
        }

    }
}
