using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

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

            _context.Pedidos.Add(pedido);
            _context.SaveChanges();
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
    }
}
