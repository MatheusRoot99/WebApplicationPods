using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Security.Cryptography;
using WebApplicationPods.Data;
using WebApplicationPods.DTO;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using static WebApplicationPods.DTO.ReportsDTO;

namespace WebApplicationPods.Repository.Repository
{
    public class PedidoRepository : IPedidoRepository
    {
        private readonly BancoContext _context;

        public PedidoRepository(BancoContext context)
        {
            _context = context;
        }

        public PedidoModel ObterPorId(int id)
        {
            return _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Endereco)
                .Include(p => p.PedidoItens)
                    .ThenInclude(pi => pi.Produto)
                .FirstOrDefault(p => p.Id == id);
        }

        public IEnumerable<PedidoModel> ObterPorCliente(int clienteId)
        {
            return _context.Pedidos
                .Where(p => p.ClienteId == clienteId)
                .Include(p => p.PedidoItens)
                    .ThenInclude(pi => pi.Produto)
                .OrderByDescending(p => p.DataPedido)
                .ToList();
        }

        public void Adicionar(PedidoModel pedido)
        {
            if (pedido == null)
                throw new ArgumentNullException(nameof(pedido));

            if (pedido.PedidoItens == null || !pedido.PedidoItens.Any())
                throw new ArgumentException("Pedido deve conter itens");

            pedido.DataPedido = DateTime.Now;

            // Gera um token aleatório caso não exista
            if (string.IsNullOrEmpty(pedido.RastreioToken))
            {
                // 16 bytes aleatórios => 32 chars hex (suficiente e legível)
                pedido.RastreioToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            }

            _context.Pedidos.Add(pedido);
            _context.SaveChanges(); // após isso, pedido.Id e RastreioToken estão persistidos
        }

        public void AtualizarStatus(int pedidoId, string status)
        {
            var pedido = _context.Pedidos.Find(pedidoId);
            if (pedido != null)
            {
                pedido.Status = status;
                _context.SaveChanges();
            }
        }

        public decimal ObterTotalVendasHoje()
        {
            var hoje = DateTime.Today;
            return _context.Pedidos
                .Where(p => p.DataPedido.Date == hoje && p.Status != "Cancelado")
                .Sum(p => p.ValorTotal);
        }
        public IEnumerable<PedidoModel> ObterAbertos()
        {
            var visiveis = new[] {
        "Aguardando Confirmação (Dinheiro)",
        "Pago", "Em Preparação", "Pronto", "Saiu p/ Entrega"
    };

            return _context.Pedidos
                .Include(p => p.Cliente)
                .Where(p => visiveis.Contains(p.Status))
                .OrderByDescending(p => p.Id)
                .AsNoTracking()
                .ToList();
        }

        public IEnumerable<PedidoModel> ObterDoDia()
        {
            var hoje = DateTime.Today;
            return _context.Pedidos
                .Include(p => p.Cliente)
                .Where(p => p.DataPedido >= hoje && p.DataPedido < hoje.AddDays(1))
                .OrderByDescending(p => p.Id)
                .AsNoTracking()
                .ToList();
        }

        // ===================== Relatórios =====================
        // Expressão OK para consultas fora de GroupBy
        private static readonly Expression<Func<PedidoModel, bool>> PagoExpr =
            p => p.Status != null &&
                 p.Status != "Cancelado" &&
                 p.Status != "Pagamento Falhou";

        public ResumoVendas ObterResumo(DateTime inicio, DateTime fim)
        {
            var q = _context.Pedidos.Where(p => p.DataPedido >= inicio && p.DataPedido < fim);

            var recebidos = q.Count();
            var pagos = q.Where(PagoExpr).Count();                                // OK (IQueryable)
            var totalVendido = q.Where(PagoExpr).Sum(p => (decimal?)p.ValorTotal) ?? 0;  // OK

            return new ResumoVendas { Recebidos = recebidos, Pagos = pagos, TotalVendido = totalVendido };
        }

        public IEnumerable<SerieDia> ObterSeriePorDia(DateTime inicio, DateTime fim)
        {
            // ❗️Dentro do GroupBy use condição inline
            return _context.Pedidos
                .Where(p => p.DataPedido >= inicio && p.DataPedido < fim)
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
            return _context.Pedidos
                .Where(p => p.DataPedido >= inicio && p.DataPedido < fim)
                .GroupBy(p => p.MetodoPagamento ?? "Indefinido")
                .Select(g => new MetodoPagamentoResumo
                {
                    Metodo = g.Key,
                    Quantidade = g.Count(), // ou: g.Count(p => p.Status != null && …) se quiser só pagos
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
            return _context.Pedidos
                .Include(p => p.Cliente)
                .Where(p => p.DataPedido >= inicio && p.DataPedido < fim)
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

        // NOVO: busca com filtros
        public IEnumerable<PedidoModel> Buscar(AdminOrdersFilterDTO f)
        {
            var q = _context.Pedidos
                .Include(p => p.Cliente)
                .AsQueryable();

            if (f.From.HasValue)
                q = q.Where(p => p.DataPedido >= f.From.Value);

            if (f.To.HasValue)
                q = q.Where(p => p.DataPedido < f.To.Value);

            if (!string.IsNullOrWhiteSpace(f.Method))
                q = q.Where(p => p.MetodoPagamento == f.Method);

            if (f.OnlyPaid)
                q = q.Where(PagoExpr);

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var qLower = f.Q.ToLower();
                q = q.Where(p =>
                    (p.Cliente != null && p.Cliente.Nome != null && p.Cliente.Nome.ToLower().Contains(qLower))
                );
            }

            return q.OrderByDescending(p => p.Id)
                    .AsNoTracking()
                    .ToList();
        }

        public void ExcluirLogico(int id, string? usuario = null)
        {
            // O filtro global NÃO atrapalha aqui porque o registro ainda não está deletado
            var p = _context.Pedidos.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            p.IsDeleted = true;
            p.DeletedAt = DateTime.UtcNow;
            p.DeletedBy = usuario;

            _context.SaveChanges();
        }

        // Opcional: apagar de vez pedidos cancelados (e já escondidos) após X dias
        public int PurgaCanceladosAntigos(int dias = 30)
        {
            var limite = DateTime.UtcNow.AddDays(-dias);

            // Precisamos ignorar o filtro global para enxergar soft-deletados
            var antigos = _context.Pedidos
                .IgnoreQueryFilters()
                .Include(p => p.PedidoItens)
                .Where(p => p.IsDeleted &&
                            (p.DeletedAt == null || p.DeletedAt < limite))
                .ToList();

            if (antigos.Count == 0) return 0;

            if (antigos.SelectMany(p => p.PedidoItens ?? Enumerable.Empty<PedidoItemModel>()).Any())
                _context.PedidoItens.RemoveRange(antigos.SelectMany(p => p.PedidoItens));

            _context.Pedidos.RemoveRange(antigos);
            return _context.SaveChanges();
        }

        public void Restaurar(int id)
        {
            var p = _context.Pedidos.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
            if (p == null) return;
            p.IsDeleted = false;
            p.DeletedAt = null;
            p.DeletedBy = null;
            _context.SaveChanges();
        }

        public PedidoModel? ObterPorToken(string token)
        {
            return _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.PedidoItens).ThenInclude(i => i.Produto)
                .FirstOrDefault(p => p.RastreioToken == token);
        }
    }
}
