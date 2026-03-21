using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Security.Cryptography;
using WebApplicationPods.Data;
using WebApplicationPods.DTO;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;
using static WebApplicationPods.DTO.ReportsDTO;

namespace WebApplicationPods.Repository.Repository
{
    public class PedidoRepository : IPedidoRepository
    {
        private readonly BancoContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly ICurrentLojaService _currentLoja;

        private static readonly string[] StatusVisiveisAbertos = new[]
        {
            "Aguardando Confirmação (Dinheiro)",
            "Pago",
            "Em Preparação",
            "Pronto",
            "Saiu p/ Entrega",
            "Aguardando Pagamento (Entrega)",
            "Aguardando Pagamento"
        };

        public PedidoRepository(BancoContext context, IHttpContextAccessor http, ICurrentLojaService currentLoja)
        {
            _context = context;
            _http = http;
            _currentLoja = currentLoja;
        }

        // ===================== Helpers =====================

        private bool IsAdmin()
        {
            var user = _http.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true && user.IsInRole("Admin");
        }

        private int? LojaIdContext()
        {
            // Multi-loja desativado por enquanto.
            // Futuramente, pode voltar com:
            // if (IsAdmin()) return null;
            // if (_currentLoja?.LojaId is int lojaId && lojaId > 0) return lojaId;

            return null;
        }

        private IQueryable<PedidoModel> BaseQuery()
        {
            var q = _context.Pedidos.AsQueryable();

            var lojaId = LojaIdContext();
            if (lojaId.HasValue)
                q = q.Where(p => p.LojaId == lojaId.Value);

            return q;
        }

        private static readonly Expression<Func<PedidoModel, bool>> PagoExpr =
            p => p.Status != null
                 && p.Status != "Cancelado"
                 && p.Status != "Pagamento Falhou";

        // ===================== CRUD / Consultas =====================

        public PedidoModel ObterPorId(int id)
        {
            var q = BaseQuery();

            return q
                .Include(p => p.Cliente)
                .Include(p => p.Endereco)
                .Include(p => p.PedidoItens).ThenInclude(pi => pi.Produto)
                .Include(p => p.Pagamentos)
                .FirstOrDefault(p => p.Id == id)!;
        }

        public IEnumerable<PedidoModel> ObterPorCliente(int clienteId)
        {
            return BaseQuery()
                .Where(p => p.ClienteId == clienteId && !p.IsDeleted)
                .Include(p => p.PedidoItens).ThenInclude(pi => pi.Produto)
                .OrderByDescending(p => p.DataPedido)
                .AsNoTracking()
                .ToList();
        }

        public PedidoModel? ObterPorToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            return BaseQuery()
                .Include(p => p.Cliente)
                .Include(p => p.PedidoItens).ThenInclude(i => i.Produto)
                .Include(p => p.Pagamentos)
                .FirstOrDefault(p => p.RastreioToken == token && !p.IsDeleted);
        }

        public void Adicionar(PedidoModel pedido)
        {
            if (pedido == null)
                throw new ArgumentNullException(nameof(pedido));

            if (pedido.PedidoItens == null || !pedido.PedidoItens.Any())
                throw new ArgumentException("Pedido deve conter itens");

            pedido.DataPedido = DateTime.Now;

            if (string.IsNullOrWhiteSpace(pedido.RastreioToken))
            {
                pedido.RastreioToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            }

            _context.Pedidos.Add(pedido);
            _context.SaveChanges();
        }

        public void AtualizarStatus(int pedidoId, string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return;

            var pedido = BaseQuery().FirstOrDefault(p => p.Id == pedidoId);
            if (pedido == null) return;

            if (!string.Equals(pedido.Status, status, StringComparison.OrdinalIgnoreCase))
            {
                pedido.Status = status;
                _context.SaveChanges();
            }
        }

        public decimal ObterTotalVendasHoje()
        {
            var hoje = DateTime.Today;
            var amanha = hoje.AddDays(1);

            return BaseQuery()
                .Where(p => !p.IsDeleted
                            && p.DataPedido >= hoje && p.DataPedido < amanha
                            && p.Status != "Cancelado"
                            && p.Status != "Pagamento Falhou")
                .Sum(p => (decimal?)p.ValorTotal) ?? 0m;
        }

        public IEnumerable<PedidoModel> ObterAbertos()
        {
            return BaseQuery()
                .Where(p => !p.IsDeleted && StatusVisiveisAbertos.Contains(p.Status))
                .Include(p => p.Cliente)
                .Include(p => p.PedidoItens)
                .Include(p => p.Pagamentos)
                .OrderByDescending(p => p.DataPedido)
                .AsNoTracking()
                .ToList();
        }

        public IEnumerable<PedidoModel> ObterDoDia()
        {
            var hoje = DateTime.Today;
            var amanha = hoje.AddDays(1);

            return BaseQuery()
                .Where(p => !p.IsDeleted && p.DataPedido >= hoje && p.DataPedido < amanha)
                .Include(p => p.Cliente)
                .Include(p => p.PedidoItens)
                .Include(p => p.Pagamentos)
                .OrderByDescending(p => p.DataPedido)
                .AsNoTracking()
                .ToList();
        }

        // ===================== Relatórios =====================

        public ResumoVendas ObterResumo(DateTime inicio, DateTime fim)
        {
            var q = BaseQuery()
                .Where(p => !p.IsDeleted && p.DataPedido >= inicio && p.DataPedido < fim);

            var recebidos = q.Count();
            var pagos = q.Where(PagoExpr).Count();
            var totalVendido = q.Where(PagoExpr).Sum(p => (decimal?)p.ValorTotal) ?? 0m;

            return new ResumoVendas
            {
                Recebidos = recebidos,
                Pagos = pagos,
                TotalVendido = totalVendido
            };
        }

        public IEnumerable<SerieDia> ObterSeriePorDia(DateTime inicio, DateTime fim)
        {
            return BaseQuery()
                .Where(p => !p.IsDeleted && p.DataPedido >= inicio && p.DataPedido < fim)
                .GroupBy(p => p.DataPedido.Date)
                .Select(g => new SerieDia
                {
                    Dia = g.Key,
                    Quantidade = g.Count(),
                    Total = g.Sum(p =>
                        (p.Status != null && p.Status != "Cancelado" && p.Status != "Pagamento Falhou")
                            ? (decimal?)p.ValorTotal
                            : 0m
                    ) ?? 0m
                })
                .OrderBy(x => x.Dia)
                .AsNoTracking()
                .ToList();
        }

        public IEnumerable<MetodoPagamentoResumo> ObterMetodosPagamentoResumo(DateTime inicio, DateTime fim)
        {
            return BaseQuery()
                .Where(p => !p.IsDeleted && p.DataPedido >= inicio && p.DataPedido < fim)
                .GroupBy(p => p.MetodoPagamento ?? "Indefinido")
                .Select(g => new MetodoPagamentoResumo
                {
                    Metodo = g.Key,
                    Quantidade = g.Count(),
                    Total = g.Sum(p =>
                        (p.Status != null && p.Status != "Cancelado" && p.Status != "Pagamento Falhou")
                            ? (decimal?)p.ValorTotal
                            : 0m
                    ) ?? 0m
                })
                .OrderByDescending(x => x.Quantidade)
                .AsNoTracking()
                .ToList();
        }

        public IEnumerable<TopClienteResumo> ObterTopClientes(DateTime inicio, DateTime fim, int take = 5)
        {
            return BaseQuery()
                .Where(p => !p.IsDeleted && p.DataPedido >= inicio && p.DataPedido < fim)
                .Include(p => p.Cliente)
                .GroupBy(p => new { p.ClienteId, Nome = p.Cliente!.Nome })
                .Select(g => new TopClienteResumo
                {
                    ClienteId = g.Key.ClienteId,
                    Nome = g.Key.Nome,
                    Quantidade = g.Count(),
                    Total = g.Sum(p =>
                        (p.Status != null && p.Status != "Cancelado" && p.Status != "Pagamento Falhou")
                            ? (decimal?)p.ValorTotal
                            : 0m
                    ) ?? 0m
                })
                .OrderByDescending(x => x.Total)
                .ThenByDescending(x => x.Quantidade)
                .Take(take)
                .AsNoTracking()
                .ToList();
        }

        // ===================== Busca avançada =====================

        public IEnumerable<PedidoModel> Buscar(AdminOrdersFilterDTO f)
        {
            var q = BaseQuery()
                .Where(p => !p.IsDeleted)
                .Include(p => p.Cliente)
                .Include(p => p.PedidoItens)
                .Include(p => p.Pagamentos)
                .AsQueryable();

            if (f.From.HasValue) q = q.Where(p => p.DataPedido >= f.From.Value);
            if (f.To.HasValue) q = q.Where(p => p.DataPedido < f.To.Value);

            if (!string.IsNullOrWhiteSpace(f.Method))
            {
                var m = f.Method.Trim();
                q = q.Where(p => (p.MetodoPagamento ?? "") == m);
            }

            if (f.OnlyPaid) q = q.Where(PagoExpr);

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var qLower = f.Q.ToLower();
                q = q.Where(p =>
                    (p.Cliente != null && p.Cliente.Nome != null && p.Cliente.Nome.ToLower().Contains(qLower))
                    || (p.MetodoPagamento != null && p.MetodoPagamento.ToLower().Contains(qLower))
                    || (p.Status != null && p.Status.ToLower().Contains(qLower))
                );
            }

            return q.OrderByDescending(p => p.DataPedido)
                    .AsNoTracking()
                    .ToList();
        }

        // ===================== Soft delete & manutenção =====================

        public void ExcluirLogico(int id, string? usuario = null)
        {
            var p = BaseQuery().FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            p.IsDeleted = true;
            p.DeletedAt = DateTime.UtcNow;
            p.DeletedBy = usuario;
            _context.SaveChanges();
        }

        public int PurgaCanceladosAntigos(int dias = 30)
        {
            var limite = DateTime.UtcNow.AddDays(-dias);

            var q = _context.Pedidos.IgnoreQueryFilters().AsQueryable();

            var lojaId = LojaIdContext();
            if (lojaId.HasValue)
                q = q.Where(p => p.LojaId == lojaId.Value);

            var antigos = q
                .Include(p => p.PedidoItens)
                .Where(p => p.IsDeleted && (p.DeletedAt == null || p.DeletedAt < limite))
                .ToList();

            if (antigos.Count == 0) return 0;

            if (antigos.SelectMany(p => p.PedidoItens ?? Enumerable.Empty<PedidoItemModel>()).Any())
                _context.PedidoItens.RemoveRange(antigos.SelectMany(p => p.PedidoItens));

            _context.Pedidos.RemoveRange(antigos);
            return _context.SaveChanges();
        }

        public void Restaurar(int id)
        {
            var q = _context.Pedidos.IgnoreQueryFilters().AsQueryable();

            var lojaId = LojaIdContext();
            if (lojaId.HasValue)
                q = q.Where(p => p.LojaId == lojaId.Value);

            var p = q.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            p.IsDeleted = false;
            p.DeletedAt = null;
            p.DeletedBy = null;
            _context.SaveChanges();
        }
    }
}