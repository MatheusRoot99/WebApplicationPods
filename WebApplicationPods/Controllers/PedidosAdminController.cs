using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplicationPods.Data;
using WebApplicationPods.DTO;
using WebApplicationPods.Helper;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;
using static WebApplicationPods.DTO.ReportsDTO;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista,Admin")]
    public class PedidosAdminController : Controller
    {
        private readonly IPedidoRepository _pedidos;
        private readonly IHubContext<PedidosHub> _hub;
        private readonly IPedidoAppService _pedidoAppService;
        private readonly IEntregaAppService _entregaAppService;
        private readonly BancoContext _context;

        public PedidosAdminController(IPedidoRepository pedidos, IHubContext<PedidosHub> hub, IPedidoAppService pedidoAppService, IEntregaAppService entregaAppService,
        BancoContext context)
        {
            _pedidos = pedidos;
            _hub = hub;
            _pedidoAppService = pedidoAppService;
            _entregaAppService = entregaAppService;
            _context = context;
        }

        //private static readonly Dictionary<string, string[]> AllowedTransitions =
        //    new(StringComparer.OrdinalIgnoreCase)
        //    {
        //        ["Aguardando Confirmação (Dinheiro)"] = new[] { "Em Preparação", "Cancelado" },
        //        ["Pago"] = new[] { "Em Preparação", "Cancelado" },
        //        ["Em Preparação"] = new[] { "Pronto", "Cancelado" },
        //        ["Pronto"] = new[] { "Saiu p/ Entrega", "Concluído", "Cancelado" },
        //        ["Saiu p/ Entrega"] = new[] { "Concluído", "Cancelado" },
        //        ["Aguardando Pagamento (Entrega)"] = new[] { "Pago", "Cancelado" },
        //        ["Aguardando Pagamento"] = new[] { "Pago", "Cancelado" },
        //        ["Concluído"] = Array.Empty<string>(),
        //        ["Cancelado"] = Array.Empty<string>()
        //    };

        [HttpGet]
        public IActionResult Index(string? filtro = "abertos")
        {
            var lista = string.Equals(filtro, "dia", StringComparison.OrdinalIgnoreCase)
                ? _pedidos.ObterDoDia()
                : _pedidos.ObterAbertos();

            ViewBag.Filtro = filtro;
            ViewBag.Allowed = PedidoStatusRules.AllowedTransitions;

            return View("~/Views/PedidosAdmin/Index.cshtml", lista);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Table(string? filtro = "abertos")
        {
            var lista = string.Equals(filtro, "dia", StringComparison.OrdinalIgnoreCase)
                ? _pedidos.ObterDoDia()
                : _pedidos.ObterAbertos();

            ViewBag.Filtro = filtro;
            ViewBag.Allowed = PedidoStatusRules.AllowedTransitions;

            return PartialView("~/Views/PedidosAdmin/_PedidosTableBody.cshtml", lista);
        }

        [HttpGet]
        public IActionResult VoltarPedidos()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> AtribuirEntregador(int id)
        {
            var pedido = await _context.Pedidos
                .Include(x => x.Cliente)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (pedido == null)
                return NotFound();

            var entregadores = await _context.Entregadores
                .Where(x => x.Ativo && x.LojaId == pedido.LojaId)
                .OrderBy(x => x.Nome)
                .ToListAsync();

            var vm = new PedidoAtribuirEntregadorViewModel
            {
                PedidoId = pedido.Id,
                ClienteNome = pedido.Cliente?.Nome ?? "-",
                StatusAtual = pedido.Status ?? "-",
                ValorTotal = pedido.ValorTotal,
                EntregadorId = pedido.EntregadorId,
                Entregadores = entregadores.Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = $"{x.Nome} - {x.Telefone}"
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtribuirEntregador(PedidoAtribuirEntregadorViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var pedidoReload = await _context.Pedidos.FirstOrDefaultAsync(x => x.Id == vm.PedidoId);
                if (pedidoReload != null)
                {
                    vm.Entregadores = await _context.Entregadores
                        .Where(x => x.Ativo && x.LojaId == pedidoReload.LojaId)
                        .OrderBy(x => x.Nome)
                        .Select(x => new SelectListItem
                        {
                            Value = x.Id.ToString(),
                            Text = x.Nome + " - " + x.Telefone
                        })
                        .ToListAsync();
                }

                return View(vm);
            }

            var ok = await _entregaAppService.AtribuirEntregadorAsync(
                vm.PedidoId,
                vm.EntregadorId!.Value,
                User.Identity?.Name);

            if (!ok)
            {
                TempData["Erro"] = "Não foi possível atribuir o entregador.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Sucesso"] = "Entregador atribuído com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarStatus(int id, string status)
        {
            var pedido = _pedidos.ObterPorId(id);
            if (pedido is null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { ok = false, error = "Pedido não encontrado." });

                TempData["Erro"] = "Pedido não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            var atual = pedido.Status ?? string.Empty;

            if (!PedidoStatusRules.PodeTransicionar(atual, status))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { ok = false, error = $"Transição inválida de '{atual}' para '{status}'." });

                TempData["Erro"] = $"Transição inválida de '{atual}' para '{status}'.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _pedidoAppService.AtualizarStatusAsync(
                    id,
                    status,
                    nomeResponsavel: User.Identity?.Name,
                    usuarioResponsavelId: usuarioId,
                    observacao: null,
                    origem: "PainelLojista");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true });

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Relatorio(string? modo = "dia", DateTime? data = null, int? ano = null, int? mes = null)
        {
            DateTime inicio, fim;
            string periodoDesc;

            if (string.Equals(modo, "mes", StringComparison.OrdinalIgnoreCase))
            {
                var y = ano ?? DateTime.Today.Year;
                var m = mes ?? DateTime.Today.Month;

                inicio = new DateTime(y, m, 1);
                fim = inicio.AddMonths(1);
                periodoDesc = $"Mês {m:00}/{y}";
            }
            else
            {
                var dia = data?.Date ?? DateTime.Today;
                inicio = dia;
                fim = dia.AddDays(1);
                periodoDesc = $"Dia {dia:dd/MM/yyyy}";
            }

            var vm = new AdminReportViewModel
            {
                PeriodoDescricao = periodoDesc,
                Inicio = inicio,
                Fim = fim,
                Resumo = _pedidos.ObterResumo(inicio, fim),
                SeriePorDia = _pedidos.ObterSeriePorDia(inicio, fim).ToList(),
                Metodos = _pedidos.ObterMetodosPagamentoResumo(inicio, fim).ToList(),
                TopClientes = _pedidos.ObterTopClientes(inicio, fim, 5).ToList()
            };

            return View("~/Views/PedidosAdmin/Relatorio.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var pedido = _pedidos.ObterPorId(id);
            if (pedido is null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { ok = false, error = "Pedido não encontrado." });

                TempData["Erro"] = "Pedido não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(pedido.Status, "Cancelado", StringComparison.OrdinalIgnoreCase))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return BadRequest(new { ok = false, error = "Só é permitido excluir pedidos cancelados." });

                TempData["Erro"] = "Só é permitido excluir pedidos cancelados.";
                return RedirectToAction(nameof(Index));
            }

            _pedidos.ExcluirLogico(id, User.Identity?.Name);
            await _hub.Clients.Group("lojistas").SendAsync("PedidosChanged", new { id, deleted = true });

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true });

            TempData["Sucesso"] = "Pedido excluído.";
            return RedirectToAction(nameof(Index));
        }
    }
}