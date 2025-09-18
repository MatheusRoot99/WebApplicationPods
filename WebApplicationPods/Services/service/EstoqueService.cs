using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class EstoqueService : IEstoqueService
    {
        private readonly BancoContext _context;
        public EstoqueService(BancoContext context) => _context = context;

        public async Task BaixarEstoquePedidoAsync(int pedidoId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var pedido = await _context.Pedidos
                .Include(p => p.PedidoItens)
                .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null) throw new InvalidOperationException("Pedido não encontrado.");

            foreach (var item in pedido.PedidoItens.Where(i => !i.EstoqueBaixado))
            {
                var prod = await _context.Produtos.FirstAsync(p => p.Id == item.ProdutoId);

                // Se usa sabores com estoque por sabor:
                prod.DeserializarSaboresQuantidades();

                if (!string.IsNullOrWhiteSpace(item.Sabor) && prod.SaboresQuantidadesList?.Any() == true)
                {
                    var sq = prod.SaboresQuantidadesList
                        .FirstOrDefault(s => s.Sabor.Equals(item.Sabor, StringComparison.OrdinalIgnoreCase));

                    if (sq == null)
                        throw new InvalidOperationException($"Sabor '{item.Sabor}' não encontrado em {prod.Nome}.");

                    if (sq.Quantidade < item.Quantidade)
                        throw new InvalidOperationException($"Estoque insuficiente do sabor '{item.Sabor}'.");

                    sq.Quantidade -= item.Quantidade;
                }
                else
                {
                    // Estoque total (sem sabor)
                    if (prod.Estoque < item.Quantidade)
                        throw new InvalidOperationException($"Estoque insuficiente do produto '{prod.Nome}'.");
                    prod.Estoque -= item.Quantidade;
                }

                // Recalcula estoque total a partir dos sabores, se for o seu caso:
                if (prod.SaboresQuantidadesList?.Any() == true)
                {
                    prod.Estoque = prod.SaboresQuantidadesList.Sum(s => s.Quantidade);
                    prod.SerializarSaboresQuantidades();
                }

                _context.Produtos.Update(prod);

                item.EstoqueBaixado = true;
                item.EstoqueBaixadoEm = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }
}
