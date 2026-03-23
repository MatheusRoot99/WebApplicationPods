using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebApplicationPods.DTO;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using static WebApplicationPods.DTO.ReportsDTO;
using System.Security.Claims;
using WebApplicationPods.Helper;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista,Admin")]
    public class PedidosAdminController : Controller
    {
        private readonly IPedidoRepository _pedidos;
        private readonly IHubContext<PedidosHub> _hub;

        public PedidosAdminController(IPedidoRepository pedidos, IHubContext<PedidosHub> hub)
        {
            _pedidos = pedidos;
            _hub = hub;
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

            _pedidos.AtualizarStatus(
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