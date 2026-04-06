using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplicationPods.Constants;
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
        private readonly ICurrentLojaService _currentLoja;

        public PedidosAdminController(
            IPedidoRepository pedidos,
            IHubContext<PedidosHub> hub,
            IPedidoAppService pedidoAppService,
            IEntregaAppService entregaAppService,
            BancoContext context,
            ICurrentLojaService currentLoja)
        {
            _pedidos = pedidos;
            _hub = hub;
            _pedidoAppService = pedidoAppService;
            _entregaAppService = entregaAppService;
            _context = context;
            _currentLoja = currentLoja;
        }

        private int? ObterLojaAtual()
        {
            if (_currentLoja?.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            var claimLojaId = User.FindFirst("LojaId")?.Value
                           ?? User.FindFirst("lojaId")?.Value;

            if (int.TryParse(claimLojaId, out var lojaIdClaim) && lojaIdClaim > 0)
                return lojaIdClaim;

            return null;
        }

        private async Task<List<SelectListItem>> CarregarEntregadoresAsync(int? pedidoLojaId = null)
        {
            var lojaAtual = ObterLojaAtual();
            var lojaBase = pedidoLojaId.GetValueOrDefault() > 0
                ? pedidoLojaId
                : lojaAtual;

            var entregadores = new List<EntregadorModel>();

            if (lojaBase.HasValue && lojaBase.Value > 0)
            {
                entregadores = await _context.Entregadores
                    .Include(x => x.Usuario)
                    .Where(x =>
                        x.Ativo &&
                        (
                            x.LojaId == lojaBase.Value ||
                            (x.Usuario != null && x.Usuario.LojaId == lojaBase.Value)
                        ))
                    .OrderBy(x => x.Nome)
                    .ToListAsync();
            }

            if (!entregadores.Any() && lojaAtual.HasValue && lojaAtual.Value > 0)
            {
                entregadores = await _context.Entregadores
                    .Include(x => x.Usuario)
                    .Where(x =>
                        x.Ativo &&
                        (
                            x.LojaId == lojaAtual.Value ||
                            (x.Usuario != null && x.Usuario.LojaId == lojaAtual.Value)
                        ))
                    .OrderBy(x => x.Nome)
                    .ToListAsync();
            }

            if (!entregadores.Any())
            {
                entregadores = await _context.Entregadores
                    .Include(x => x.Usuario)
                    .Where(x => x.Ativo)
                    .OrderBy(x => x.Nome)
                    .ToListAsync();
            }

            return entregadores
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(x.Telefone)
                        ? x.Nome
                        : $"{x.Nome} - {x.Telefone}"
                })
                .ToList();
        }

        private static string NormalizarFiltro(string? filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro))
                return "abertos";

            var normalizado = filtro.Trim().ToLowerInvariant();
            return normalizado switch
            {
                "dia" => "dia",
                "todos" => "todos",
                _ => "abertos"
            };
        }

        private static List<SelectListItem> ObterStatusOptions()
        {
            var statuses = new[]
            {
                PedidoStatus.Pendente,
                PedidoStatus.AguardandoPagamento,
                PedidoStatus.AguardandoPagamentoEntrega,
                PedidoStatus.AguardandoConfirmacaoDinheiro,
                PedidoStatus.Pago,
                PedidoStatus.EmPreparacao,
                PedidoStatus.Pronto,
                PedidoEntregaStatus.AguardandoAtribuicao,
                PedidoEntregaStatus.Atribuido,
                PedidoStatus.SaiuParaEntrega,
                PedidoEntregaStatus.SaiuParaEntrega,
                PedidoEntregaStatus.Entregue,
                PedidoStatus.Concluido,
                PedidoStatus.Cancelado,
                PedidoStatus.PagamentoFalhou
            };

            return statuses
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => new SelectListItem { Value = x, Text = x })
                .ToList();
        }

        private async Task<PedidosAdminIndexViewModel> MontarIndexVmAsync(AdminOrdersFilterDTO filtros)
        {
            filtros ??= new AdminOrdersFilterDTO();
            filtros.Filtro = NormalizarFiltro(filtros.Filtro);

            var pedidos = _pedidos.Buscar(filtros).ToList();

            var vm = new PedidosAdminIndexViewModel
            {
                Pedidos = pedidos,
                Filtros = filtros,
                StatusOptions = ObterStatusOptions(),
                Entregadores = await CarregarEntregadoresAsync(),
                TotalEncontrado = pedidos.Count,
                ModoTitulo = filtros.Filtro switch
                {
                    "dia" => "de hoje",
                    "todos" => "gerais",
                    _ => "abertos"
                }
            };

            return vm;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] AdminOrdersFilterDTO filtros)
        {
            var vm = await MontarIndexVmAsync(filtros);
            ViewBag.Allowed = PedidoStatusRules.AllowedTransitions;
            return View("~/Views/PedidosAdmin/Index.cshtml", vm);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Table([FromQuery] AdminOrdersFilterDTO filtros)
        {
            filtros ??= new AdminOrdersFilterDTO();
            filtros.Filtro = NormalizarFiltro(filtros.Filtro);

            var lista = _pedidos.Buscar(filtros);
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

            var vm = new PedidoAtribuirEntregadorViewModel
            {
                PedidoId = pedido.Id,
                ClienteNome = pedido.Cliente?.Nome ?? "-",
                StatusAtual = pedido.Status ?? "-",
                ValorTotal = pedido.ValorTotal,
                EntregadorId = pedido.EntregadorId,
                Entregadores = await CarregarEntregadoresAsync(pedido.LojaId)
            };

            if (!vm.Entregadores.Any())
            {
                ModelState.AddModelError(string.Empty, "Nenhum entregador ativo foi encontrado.");
            }

            return View("~/Views/PedidosAdmin/AtribuirEntregador.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtribuirEntregador(PedidoAtribuirEntregadorViewModel vm)
        {
            var pedidoReload = await _context.Pedidos
                .Include(x => x.Cliente)
                .FirstOrDefaultAsync(x => x.Id == vm.PedidoId);

            if (pedidoReload == null)
            {
                TempData["Erro"] = "Pedido não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            vm.ClienteNome = pedidoReload.Cliente?.Nome ?? "-";
            vm.StatusAtual = pedidoReload.Status ?? "-";
            vm.ValorTotal = pedidoReload.ValorTotal;
            vm.Entregadores = await CarregarEntregadoresAsync(pedidoReload.LojaId);

            if (!vm.Entregadores.Any())
            {
                ModelState.AddModelError(string.Empty, "Nenhum entregador ativo foi encontrado.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/PedidosAdmin/AtribuirEntregador.cshtml", vm);
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
            var group = pedido.LojaId > 0
                ? WebApplicationPods.Hubs.PedidosHub.LojaGroup(pedido.LojaId)
                : WebApplicationPods.Hubs.PedidosHub.GlobalLojistasGroup;

            await _hub.Clients.Group(group).SendAsync("PedidosChanged", new { id, deleted = true });

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true });

            TempData["Sucesso"] = "Pedido excluído.";
            return RedirectToAction(nameof(Index));
        }
    }
}