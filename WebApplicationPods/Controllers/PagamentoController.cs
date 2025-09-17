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

        public PagamentoController(
            IPaymentService payments,
            IPedidoRepository pedidos,
            BancoContext db,
            IConfiguration cfg,
            IPaymentCredentialsResolver creds,
            UserManager<ApplicationUser> userManager)
        {
            _payments = payments;
            _pedidos = pedidos;
            _db = db;
            _cfg = cfg;
            _creds = creds;
            _userManager = userManager;
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

            // ====== RESUMO PARA A VIEW ======
            // garanta que temos os itens carregados (usa o repositório se ele já popula; senão faz o fallback via DB)
            // ====== RESUMO PARA A VIEW (com preço promocional do Produto) ======
            var itensPedido = pedido.PedidoItens?.ToList();
            if (itensPedido == null || itensPedido.Count == 0)
            {
                itensPedido = await _db.Set<PedidoItemModel>()
                    .Where(i => i.PedidoId == pedido.Id)
                    .Include(i => i.Produto) // precisamos do Preco/PrecoPromocional
                    .ToListAsync();
            }

            // Monta view model leve por item
            var itensResumo = itensPedido.Select(i =>
            {
                // preço cheio vem do produto
                var precoCheio = i.Produto?.Preco ?? i.PrecoUnitario;

                // preço aplicado: se produto está em promoção e PrecoPromocional válido (< cheio), usa-o;
                // senão, usa o PrecoUnitario do item (que já deve refletir o que será cobrado).
                decimal precoAplicado;
                if (i.Produto?.EmPromocao == true && i.Produto?.PrecoPromocional is decimal pProm && pProm > 0 && pProm < precoCheio)
                    precoAplicado = pProm;
                else
                    precoAplicado = i.PrecoUnitario;

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

            // Subtotais e desconto (diferença entre cheio e aplicado)
            decimal subtotal = itensResumo.Sum(x => (decimal)x.Subtotal);
            decimal subtotalCheio = itensResumo.Sum(x => (decimal)x.SubtotalCheio);
            decimal desconto = Math.Max(0m, subtotalCheio - subtotal);

            // frete e total
            decimal frete = pedido.TaxaEntrega;
            decimal total = pedido.ValorTotal > 0 ? pedido.ValorTotal : (subtotal + frete);

            // anexa para a view
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


            // pega credenciais (usuário logado OU default da loja OU appsettings) – como já existia
            var mpCreds = await _creds.GetAsync<MercadoPagoOptions>(User, "MercadoPago");
            ViewBag.MP_PublicKey = mpCreds?.PublicKey ?? _cfg["Payments:MercadoPago:PublicKey"];

            return View(payment);
        }

        private void MarcarPedidoComoPago(int pedidoId) =>
            _pedidos.AtualizarStatus(pedidoId, "Pago");

        /// <summary>Polling de status.</summary>
        // GET /Pagamento/Status?id=123
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Status(int id)
        {
            var p = await _db.Pagamentos
                .Include(x => x.Pedido)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            // timeout (minutos) via appsettings: "Payments:Pix:TimeoutMinutes": 15
            var timeoutMinutes = 15;
            if (int.TryParse(_cfg["Payments:Pix:TimeoutMinutes"], out var cfgMin) && cfgMin > 0)
                timeoutMinutes = cfgMin;

            // calcula expiração apenas para PIX em estados não-terminais
            DateTime? expiresAtUtc = null;
            int remainingSeconds = 0;

            bool IsTerminal(PaymentStatus s) =>
                s == PaymentStatus.Paid || s == PaymentStatus.Failed || s == PaymentStatus.Canceled;

            if (p.Metodo == PaymentMethod.Pix && !IsTerminal(p.Status))
            {
                // CreatedAt já está em UTC no modelo
                expiresAtUtc = p.CreatedAt.AddMinutes(timeoutMinutes);
                var now = DateTime.UtcNow;

                if (now >= expiresAtUtc.Value)
                {
                    // expirou: cancela pagamento e pedido
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

            return Json(new
            {
                paid = isPaid,
                status = p.Status.ToString(),
                last4 = p.CardLast4,
                brand = p.CardBrand,
                paidAt = p.PaidAt,
                pedidoId = p.PedidoId,

                // redireciona somente quando pago
                redirect = isPaid
                    ? Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId })
                    : null,

                // dados de expiração (para frontend mostrar contagem regressiva)
                expiresAt = expiresAtUtc?.ToString("o"), // ISO 8601 UTC
                remainingSeconds
            });
        }


        /// <summary>Confirmação do cartão (payload do Brick).</summary>
        [HttpPost]
        [AllowAnonymous] // cliente é anônimo
        [ValidateAntiForgeryToken] // mantém proteção (enviar token no fetch)
        public async Task<IActionResult> ConfirmCard(int id, [FromBody] object clientPayload)
        {
            var ok = await _payments.ConfirmCardAsync(id, clientPayload?.ToString());

            var p = await _db.Pagamentos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            string? redirect = null;

            if (ok && p != null)
            {
                MarcarPedidoComoPago(p.PedidoId);
                redirect = Url.Action("Confirmacao", "Carrinho", new { id = p.PedidoId });
            }

            return Ok(new { success = ok, redirect });
        }

        /// <summary>Webhook do provedor (MP).</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            await _payments.ApplyWebhookAsync(Request);
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

            // Já pago: não permite cancelar
            if (p.Status == PaymentStatus.Paid)
                return Json(new { ok = false, message = "Pagamento já aprovado; não é possível cancelar." });

            // Se já estiver cancelado, apenas responde OK (idempotente)
            if (p.Status != PaymentStatus.Canceled)
            {
                p.Status = PaymentStatus.Canceled;
                p.CanceledAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _pedidos.AtualizarStatus(p.PedidoId, "Cancelado");
            }

            return Json(new
            {
                ok = true,
                redirect = Url.Action("Index", "Carrinho")
            });
        }


    }
}
