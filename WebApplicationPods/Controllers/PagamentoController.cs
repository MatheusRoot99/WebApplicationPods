using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
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
        private readonly ICarrinhoRepository _carrinho;
        private readonly BancoContext _db;
        private readonly IConfiguration _cfg;
        private readonly IPaymentCredentialsResolver _creds;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEstoqueService _estoque;
        private readonly IHubContext<PedidosHub> _hub;
        private readonly IPedidoAppService _pedidoAppService;
        private readonly ICurrentLojaService _currentLoja;

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
            IPedidoAppService pedidoAppService,
            ICurrentLojaService currentLoja)
        {
            _payments = payments;
            _pedidos = pedidos;
            _carrinho = carrinho;
            _db = db;
            _cfg = cfg;
            _creds = creds;
            _userManager = userManager;
            _estoque = estoque;
            _hub = hub;
            _pedidoAppService = pedidoAppService;
            _currentLoja = currentLoja;
        }

        private async Task<string?> ExtrairMercadoPagoPaymentIdAsync(HttpRequest request)
        {
            var dataId = request.Query["data.id"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(dataId))
                dataId = request.Query["id"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(dataId))
                return dataId;

            request.EnableBuffering();

            using var sr = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await sr.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var dataEl) &&
                    dataEl.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    dataEl.TryGetProperty("id", out var idEl))
                {
                    return idEl.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? idEl.GetInt64().ToString()
                        : idEl.GetString();
                }

                if (root.TryGetProperty("resource", out var resourceEl) &&
                    resourceEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var resource = resourceEl.GetString() ?? "";
                    var lastSlash = resource.LastIndexOf('/');
                    if (lastSlash >= 0 && lastSlash + 1 < resource.Length)
                        return resource[(lastSlash + 1)..];
                }
            }
            catch
            {
            }

            return null;
        }

        private bool ValidarAssinaturaMercadoPago(HttpRequest request, string? secret, string? dataId)
        {
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
            if (string.IsNullOrWhiteSpace(metodo))
                return PaymentMethod.Cash;

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

        private Task<bool> MarcarPedidoComoPago(int pedidoId)
        {
            return _pedidoAppService.MarcarComoPagoAsync(
                pedidoId,
                origem: "PagamentoController");
        }

        private async Task EnsureBaixaEstoqueAsync(int pedidoId)
        {
            var existeNaoBaixado = await _db.Set<PedidoItemModel>()
                .AnyAsync(i => i.PedidoId == pedidoId && !i.EstoqueBaixado);

            if (existeNaoBaixado)
                await _estoque.BaixarEstoquePedidoAsync(pedidoId);
        }

        private void MarkCartCleared(int pedidoId)
        {
            HttpContext?.Session?.SetString($"CartClearedForOrder_{pedidoId}", "1");
        }

        private bool IsCartCleared(int pedidoId)
        {
            return string.Equals(
                HttpContext?.Session?.GetString($"CartClearedForOrder_{pedidoId}"),
                "1",
                StringComparison.Ordinal);
        }

        private void ClearCartOnceForThisSession(int pedidoId)
        {
            if (!IsCartCleared(pedidoId))
            {
                _carrinho.LimparCarrinho();
                MarkCartCleared(pedidoId);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Checkout(int pedidoId, string? t = null)
        {
            var pedido = _pedidos.ObterPorId(pedidoId);

            if (pedido == null)
                return NotFound();

            if (!await PodeAcessarPedidoAsync(pedido, t))
            {
                TempData["Erro"] = "Não foi possível identificar seu pedido.";
                return RedirectToAction("Buscar", "Pedido");
            }

            ViewBag.OrderToken = pedido.RastreioToken;

            var metodo = MapMetodo(pedido.MetodoPagamento);

            var existing = await _db.Pagamentos
                .IgnoreQueryFilters()
                .Where(p => p.PedidoId == pedido.Id && p.Metodo == metodo)
                .Where(p => p.Status != PaymentStatus.Paid &&
                            p.Status != PaymentStatus.Canceled &&
                            p.Status != PaymentStatus.Failed)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            var payment = existing ?? await _payments.StartPaymentAsync(pedido, metodo);

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
                    i.Produto?.EmPromocao == true &&
                    i.Produto?.PrecoPromocional is decimal pp &&
                    pp > 0 &&
                    pp < precoCheio
                        ? pp
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

            var subtotal = itensResumo.Sum(x => (decimal)x.Subtotal);
            var subtotalCheio = itensResumo.Sum(x => (decimal)x.SubtotalCheio);
            var desconto = Math.Max(0m, subtotalCheio - subtotal);
            var frete = pedido.TaxaEntrega;
            var total = pedido.ValorTotal > 0 ? pedido.ValorTotal : subtotal + frete;

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

            var mpCreds = pedido.LojaId > 0
                ? await _creds.GetForLojaAsync<MercadoPagoOptions>(pedido.LojaId, "MercadoPago")
                : await _creds.GetAsync<MercadoPagoOptions>(User, "MercadoPago");
            ViewBag.MP_PublicKey = mpCreds?.PublicKey ?? _cfg["Payments:MercadoPago:PublicKey"];

            return View(payment);
        }

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Status(int id, string? t = null)
        {
            var p = await _db.Pagamentos
                .IgnoreQueryFilters()
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null)
                return NotFound();

            if (!await PodeAcessarPedidoAsync(p.Pedido, t))
                return Unauthorized(new { paid = false, status = "Unauthorized" });

            bool IsTerminal(PaymentStatus s)
            {
                return s == PaymentStatus.Paid ||
                       s == PaymentStatus.Failed ||
                       s == PaymentStatus.Canceled;
            }

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
            return _hub.Clients.Groups(PedidosHub.DestinosPedido(pedido.LojaId)).SendAsync("NewOrder", new
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

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCard(int id, string? t, [FromBody] object clientPayload)
        {
            var pagamentoAntes = await _db.Pagamentos
                .IgnoreQueryFilters()
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (pagamentoAntes == null)
                return NotFound(new { success = false, message = "Pagamento não encontrado." });

            if (!await PodeAcessarPedidoAsync(pagamentoAntes.Pedido, t))
                return Unauthorized(new { success = false, message = "Pedido não autorizado." });

            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString() ?? string.Empty);

            var p = await _db.Pagamentos
                .IgnoreQueryFilters()
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

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            var providerPaymentId = await ExtrairMercadoPagoPaymentIdAsync(Request);

            var payment = string.IsNullOrWhiteSpace(providerPaymentId)
                ? null
                : await _db.Pagamentos
                    .IgnoreQueryFilters()
                    .Include(p => p.Pedido)
                    .FirstOrDefaultAsync(p =>
                        p.Provider == "MercadoPago" &&
                        p.ProviderPaymentId == providerPaymentId);

            string? webhookSecret = null;

            if (payment?.Pedido?.LojaId is int lojaId && lojaId > 0)
            {
                var lojaCreds = await _creds.GetForLojaAsync<MercadoPagoOptions>(lojaId, "MercadoPago");
                webhookSecret = lojaCreds.WebhookSecret;
            }

            webhookSecret ??= _cfg["Payments:MercadoPago:WebhookSecret"];

            if (!ValidarAssinaturaMercadoPago(Request, webhookSecret, providerPaymentId))
            {
                return Unauthorized(new
                {
                    ok = false,
                    message = "Assinatura do Mercado Pago inválida."
                });
            }

            await _payments.ApplyWebhookAsync(Request);

            return Ok(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id, string? t = null)
        {
            var p = await _db.Pagamentos
                .IgnoreQueryFilters()
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null)
                return Json(new { ok = false, message = "Pagamento não encontrado." });

            if (!await PodeAcessarPedidoAsync(p.Pedido, t))
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

        [HttpPost]
        [Authorize(Roles = "Admin,Lojista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprovarPagamentoEntrega(int pedidoId)
        {
            var pedido = await _db.Pedidos
                .IgnoreQueryFilters()
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null)
                return NotFound();

            if (!await PodeAcessarPedidoAsync(pedido, null))
                return Forbid();

            if (!string.Equals(pedido.MetodoPagamento, "dinheiro", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.MetodoPagamento, "cartão de débito", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pedido.MetodoPagamento, "cartao de debito", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Este pedido não é de pagamento na entrega.";
                return RedirectToAction("Index", "PedidosAdmin");
            }

            await _pedidoAppService.MarcarComoPagoAsync(
                pedidoId,
                origem: "PagamentoController");

            await EnsureBaixaEstoqueAsync(pedidoId);
            await NotifyPaidAsync(pedido);

            TempData["Sucesso"] = "Pedido aprovado e estoque atualizado.";
            return RedirectToAction("Index", "PedidosAdmin");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lojista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPixManual(int pagamentoId)
        {
            var p = await _db.Pagamentos
                .IgnoreQueryFilters()
                .Include(x => x.Pedido)
                .ThenInclude(pd => pd.Cliente)
                .FirstOrDefaultAsync(x => x.Id == pagamentoId);

            if (p == null)
            {
                TempData["Erro"] = "Pagamento não encontrado.";
                return RedirectToAction("Index", "PedidosAdmin");
            }

            if (!await PodeAcessarPedidoAsync(p.Pedido, null))
                return Forbid();

            if (p.Metodo != PaymentMethod.Pix ||
                !string.Equals(p.Provider, "PixManual", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Este pagamento não é PIX manual.";
                return RedirectToAction("Index", "PedidosAdmin");
            }

            if (p.Status == PaymentStatus.Paid)
            {
                TempData["Sucesso"] = "Pagamento já está aprovado.";
                return RedirectToAction("Index", "PedidosAdmin");
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
            return RedirectToAction("Index", "PedidosAdmin");
        }

        private int? ObterLojaIdDoContexto()
        {
            if (_currentLoja?.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            var lojaIdClaim = User.FindFirst("LojaId")?.Value
                           ?? User.FindFirst("lojaId")?.Value;

            if (int.TryParse(lojaIdClaim, out var lojaId) && lojaId > 0)
                return lojaId;

            return null;
        }

        private async Task<bool> PodeAcessarPedidoAsync(PedidoModel? pedido, string? token)
        {
            if (pedido == null)
                return false;

            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                    return true;

                if (User.IsInRole("Lojista"))
                {
                    var lojaId = ObterLojaIdDoContexto();

                    if (!lojaId.HasValue)
                    {
                        var user = await _userManager.GetUserAsync(User);
                        lojaId = user?.LojaId;
                    }

                    return lojaId.HasValue &&
                           lojaId.Value > 0 &&
                           pedido.LojaId == lojaId.Value;
                }
            }

            return !string.IsNullOrWhiteSpace(token) &&
                   !string.IsNullOrWhiteSpace(pedido.RastreioToken) &&
                   string.Equals(pedido.RastreioToken, token, StringComparison.OrdinalIgnoreCase);
        }
    }
}
