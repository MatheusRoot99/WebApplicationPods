using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WebApplicationPods.Constants;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Areas.PainelLojista.Controllers
{
    [Area("PainelLojista")]
    [Authorize(Roles = "Lojista")]
    public class DashboardController : Controller
    {
        private static readonly string[] StatusFinais =
        {
            PedidoStatus.Concluido,
            PedidoStatus.Cancelado,
            PedidoStatus.PagamentoFalhou
        };

        private static readonly string[] StatusEntregaEmAndamento =
        {
            EntregaStatus.Atribuida,
            EntregaStatus.Aceita,
            EntregaStatus.Coletada,
            EntregaStatus.EmRota
        };

        private static readonly Expression<Func<PedidoModel, bool>> PedidoContaComoReceitaExpr =
            x => x.Status != PedidoStatus.Cancelado &&
                 x.Status != PedidoStatus.PagamentoFalhou;

        private readonly BancoContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentLojaService _currentLojaService;

        public DashboardController(
            BancoContext context,
            UserManager<ApplicationUser> userManager,
            ICurrentLojaService currentLojaService)
        {
            _context = context;
            _userManager = userManager;
            _currentLojaService = currentLojaService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var lojaId = ResolverLojaId(user);
            if (!lojaId.HasValue || lojaId.Value <= 0)
            {
                TempData["Erro"] = "Não foi possível identificar a loja do lojista para montar o dashboard.";
                return View(new PainelLojistaDashboardViewModel
                {
                    SaudacaoNome = user.Nome,
                    LojaNome = "Loja não identificada",
                    StatusLojaTexto = "Loja não identificada",
                    StatusLojaMensagem = "Associe o lojista a uma loja válida para visualizar os indicadores."
                });
            }

            var hoje = DateTime.Today;
            var amanha = hoje.AddDays(1);
            var inicio7Dias = hoje.AddDays(-6);
            var fim7Dias = amanha;

            var pedidosBase = _context.Pedidos
                .AsNoTracking()
                .Where(x => x.LojaId == lojaId.Value);

            var produtosBase = _context.Produtos
                .AsNoTracking()
                .Where(x => x.LojaId == lojaId.Value);

            var entregasBase = _context.Entregas
                .AsNoTracking()
                .Where(x => x.Pedido.LojaId == lojaId.Value);

            var loja = await _context.Lojas
                .AsNoTracking()
                .Include(x => x.Config)
                .FirstOrDefaultAsync(x => x.Id == lojaId.Value);

            var pedidosHoje = await pedidosBase
                .CountAsync(x => x.DataPedido >= hoje && x.DataPedido < amanha);

            var faturamentoHoje = await pedidosBase
                .Where(x => x.DataPedido >= hoje && x.DataPedido < amanha)
                .Where(PedidoContaComoReceitaExpr)
                .SumAsync(x => (decimal?)x.ValorTotal) ?? 0m;

            var pedidosPendentes = await pedidosBase
                .CountAsync(x => !StatusFinais.Contains(x.Status));

            var entregasEmAndamento = await entregasBase
                .CountAsync(x => StatusEntregaEmAndamento.Contains(x.Status));

            var produtosAtivos = await produtosBase
                .CountAsync(x => x.Ativo);

            var pedidosPagos7Dias = await pedidosBase
                .Where(x => x.DataPedido >= inicio7Dias && x.DataPedido < fim7Dias)
                .Where(PedidoContaComoReceitaExpr)
                .Select(x => x.ValorTotal)
                .ToListAsync();

            var ticketMedio7Dias = pedidosPagos7Dias.Count > 0
                ? pedidosPagos7Dias.Average()
                : 0m;

            var pedidosUltimos7Dias = await pedidosBase
                .CountAsync(x => x.DataPedido >= inicio7Dias && x.DataPedido < fim7Dias);

            var faturamentoUltimos7Dias = await pedidosBase
                .Where(x => x.DataPedido >= inicio7Dias && x.DataPedido < fim7Dias)
                .Where(PedidoContaComoReceitaExpr)
                .SumAsync(x => (decimal?)x.ValorTotal) ?? 0m;

            var topProduto = await _context.PedidoItens
                .AsNoTracking()
                .Where(x => x.Pedido.LojaId == lojaId.Value)
                .Where(x => x.Pedido.DataPedido >= inicio7Dias && x.Pedido.DataPedido < fim7Dias)
                .Where(x => x.Pedido.Status != PedidoStatus.Cancelado && x.Pedido.Status != PedidoStatus.PagamentoFalhou)
                .GroupBy(x => new { x.ProdutoId, Nome = x.Produto.Nome })
                .Select(g => new
                {
                    g.Key.Nome,
                    Quantidade = g.Sum(x => x.Quantidade)
                })
                .OrderByDescending(x => x.Quantidade)
                .ThenBy(x => x.Nome)
                .FirstOrDefaultAsync();

            var serieBruta = await pedidosBase
                .Where(x => x.DataPedido >= inicio7Dias && x.DataPedido < fim7Dias)
                .GroupBy(x => x.DataPedido.Date)
                .Select(g => new DashboardSerieDiaViewModel
                {
                    Dia = g.Key,
                    Quantidade = g.Count(),
                    Total = g.Where(x => x.Status != PedidoStatus.Cancelado && x.Status != PedidoStatus.PagamentoFalhou)
                        .Sum(x => (decimal?)x.ValorTotal) ?? 0m
                })
                .ToListAsync();

            var serie7Dias = Enumerable.Range(0, 7)
                .Select(offset => inicio7Dias.AddDays(offset))
                .Select(dia =>
                {
                    var ponto = serieBruta.FirstOrDefault(x => x.Dia.Date == dia.Date);
                    return new DashboardSerieDiaViewModel
                    {
                        Dia = dia,
                        Quantidade = ponto?.Quantidade ?? 0,
                        Total = ponto?.Total ?? 0m
                    };
                })
                .ToList();

            var pedidosPorStatus = await pedidosBase
                .GroupBy(x => x.Status == null || x.Status == "" ? "Sem status" : x.Status)
                .Select(g => new DashboardStatusResumoViewModel
                {
                    Status = g.Key,
                    Quantidade = g.Count()
                })
                .OrderByDescending(x => x.Quantidade)
                .ThenBy(x => x.Status)
                .Take(8)
                .ToListAsync();

            var historicoRecente = await _context.PedidoHistoricos
                .AsNoTracking()
                .Where(x => x.Pedido.LojaId == lojaId.Value)
                .OrderByDescending(x => x.DataCadastro)
                .Take(8)
                .ToListAsync();

            var ultimasMovimentacoes = historicoRecente
                .Select(x => new DashboardMovimentacaoViewModel
                {
                    PedidoId = x.PedidoId,
                    Titulo = $"Pedido #{x.PedidoId}",
                    Descricao = MontarDescricaoMovimentacao(x),
                    Data = x.DataCadastro
                })
                .ToList();

            var lojaOnline = loja?.Ativa == true && loja.Config?.Ativo != false && loja.Config?.ForcarFechado != true;
            var statusLojaTexto = lojaOnline ? "Em operação" : "Pausada";
            var statusLojaMensagem = loja?.Config?.MensagemStatus;

            var vm = new PainelLojistaDashboardViewModel
            {
                LojaNome = loja?.Nome ?? user.Loja?.Nome ?? "Minha loja",
                SaudacaoNome = user.Nome,
                LojaOnline = lojaOnline,
                StatusLojaTexto = statusLojaTexto,
                StatusLojaMensagem = string.IsNullOrWhiteSpace(statusLojaMensagem)
                    ? (lojaOnline ? "A loja está pronta para receber pedidos." : "Revise o status da loja nas configurações.")
                    : statusLojaMensagem,
                PedidosHoje = pedidosHoje,
                FaturamentoHoje = faturamentoHoje,
                PedidosPendentes = pedidosPendentes,
                EntregasEmAndamento = entregasEmAndamento,
                TicketMedio7Dias = ticketMedio7Dias,
                ProdutosAtivos = produtosAtivos,
                ProdutoMaisVendidoNome = topProduto?.Nome ?? "Sem vendas ainda",
                ProdutoMaisVendidoQuantidade = topProduto?.Quantidade ?? 0,
                PedidosUltimos7Dias = pedidosUltimos7Dias,
                FaturamentoUltimos7Dias = faturamentoUltimos7Dias,
                Serie7Dias = serie7Dias,
                PedidosPorStatus = pedidosPorStatus,
                UltimasMovimentacoes = ultimasMovimentacoes
            };

            return View(vm);
        }

        private int? ResolverLojaId(ApplicationUser user)
        {
            if (_currentLojaService?.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            if (user.LojaId.HasValue && user.LojaId.Value > 0)
                return user.LojaId.Value;

            var claimLojaId = User.FindFirst("LojaId")?.Value ?? User.FindFirst("lojaId")?.Value;
            return int.TryParse(claimLojaId, out var lojaIdClaim) && lojaIdClaim > 0
                ? lojaIdClaim
                : null;
        }

        private static string MontarDescricaoMovimentacao(PedidoHistoricoModel historico)
        {
            var origem = string.IsNullOrWhiteSpace(historico.Origem)
                ? "sistema"
                : historico.Origem!;

            var responsavel = string.IsNullOrWhiteSpace(historico.NomeResponsavel)
                ? "Sistema"
                : historico.NomeResponsavel!;

            var descricaoBase = string.IsNullOrWhiteSpace(historico.StatusAnterior)
                ? $"{responsavel} definiu o status como {historico.NovoStatus}"
                : $"{responsavel} alterou de {historico.StatusAnterior} para {historico.NovoStatus}";

            if (!string.IsNullOrWhiteSpace(historico.Observacao))
                descricaoBase += $" • {historico.Observacao}";

            return $"{descricaoBase} ({origem})";
        }
    }
}