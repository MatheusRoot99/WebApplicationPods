using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApplicationPods.Hubs;
using WebApplicationPods.Repository.Interface;
using static WebApplicationPods.DTO.ReportsDTO;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista")]
    public class PedidosAdminController : Controller
    {
        private readonly IPedidoRepository _pedidos;
        private readonly IHubContext<PedidosHub> _hub;

        public PedidosAdminController(IPedidoRepository pedidos, IHubContext<PedidosHub> hub)
        {
            _pedidos = pedidos;
            _hub = hub;
        }

        // Fluxo permitido (status atual -> próximos possíveis)
        private static readonly Dictionary<string, string[]> AllowedTransitions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Pendente"] = new[] { "Cancelado" }, // se quiser permitir "Pago" aqui, adicione
                ["Aguardando Pagamento"] = new[] { "Pago", "Cancelado" },
                ["Aguardando Pagamento (Entrega)"] = new[] { "Pago", "Cancelado" },

                ["Aguardando Confirmação (Dinheiro)"] = new[] { "Pago", "Cancelado" },
                ["Pago"] = new[] { "Em Preparação", "Cancelado" },
                ["Em Preparação"] = new[] { "Pronto", "Cancelado" },
                ["Pronto"] = new[] { "Saiu p/ Entrega", "Cancelado" },
                ["Saiu p/ Entrega"] = new[] { "Entregue", "Cancelado" },
                ["Pagamento Falhou"] = new[] { "Cancelado" }
                // "Entregue" e "Cancelado" -> finais
            };

        // GET /PedidosAdmin?filtro=abertos|dia
        [HttpGet]
        public IActionResult Index(string? filtro = "abertos")
        {
            var lista = string.Equals(filtro, "dia", StringComparison.OrdinalIgnoreCase)
                ? _pedidos.ObterDoDia()
                : _pedidos.ObterAbertos();

            ViewBag.Filtro = filtro;
            ViewBag.Allowed = AllowedTransitions; // <-- expõe para a view/partial
            return View(lista);
        }

        // GET /PedidosAdmin/Table?filtro=abertos|dia   (usado no live refresh)
        [HttpGet]
        public IActionResult Table(string? filtro = "abertos")
        {
            var lista = string.Equals(filtro, "dia", StringComparison.OrdinalIgnoreCase)
                ? _pedidos.ObterDoDia()
                : _pedidos.ObterAbertos();

            ViewBag.Allowed = AllowedTransitions; // <-- idem
            return PartialView("_PedidosTableBody", lista);
        }

        // POST /PedidosAdmin/AtualizarStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarStatus(int id, string status)
        {
            var pedido = _pedidos.ObterPorId(id);
            if (pedido is null)
            {
                TempData["Erro"] = "Pedido não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            var atual = pedido.Status ?? string.Empty;
            if (!AllowedTransitions.TryGetValue(atual, out var nexts) ||
                !nexts.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                TempData["Erro"] = $"Transição inválida de '{atual}' para '{status}'.";
                return RedirectToAction(nameof(Index));
            }

            _pedidos.AtualizarStatus(id, status);

            // avisa todos os lojistas para atualizar a grade
            await _hub.Clients.Group("lojistas").SendAsync("PedidosChanged", new { id, status });

            return RedirectToAction(nameof(Index));
        }

        // ============ RELATÓRIO ============

        // GET /PedidosAdmin/Relatorio?modo=dia&data=2025-08-26
        // GET /PedidosAdmin/Relatorio?modo=mes&ano=2025&mes=8
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

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var pedido = _pedidos.ObterPorId(id);
            if (pedido is null)
            {
                TempData["Erro"] = "Pedido não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(pedido.Status, "Cancelado", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Só é permitido excluir pedidos cancelados.";
                return RedirectToAction(nameof(Index));
            }

            _pedidos.ExcluirLogico(id, User.Identity?.Name);

            await _hub.Clients.Group("lojistas").SendAsync("PedidosChanged", new { id, deleted = true });
            TempData["Sucesso"] = "Pedido excluído.";
            return RedirectToAction(nameof(Index));
        }

    }


}
