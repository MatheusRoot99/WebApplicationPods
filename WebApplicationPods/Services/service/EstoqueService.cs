using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class EstoqueService : IEstoqueService
    {
        private readonly BancoContext _context;

        public EstoqueService(BancoContext context)
        {
            _context = context;
        }

        public async Task BaixarEstoquePedidoAsync(int pedidoId)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var pedido = await _context.Pedidos
                .Include(p => p.PedidoItens)
                .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null)
                throw new InvalidOperationException("Pedido não encontrado.");

            if (pedido.PedidoItens == null || !pedido.PedidoItens.Any())
                throw new InvalidOperationException("Pedido não possui itens para baixa de estoque.");

            foreach (var item in pedido.PedidoItens.Where(i => !i.EstoqueBaixado))
            {
                if (item.Quantidade <= 0)
                    throw new InvalidOperationException($"Quantidade inválida no item #{item.Id}.");

                var produto = await _context.Produtos
                    .FirstOrDefaultAsync(p => p.Id == item.ProdutoId && p.LojaId == pedido.LojaId);

                if (produto == null)
                    throw new InvalidOperationException($"Produto #{item.ProdutoId} não encontrado na loja do pedido.");

                produto.DeserializarSaboresQuantidades();

                if (!string.IsNullOrWhiteSpace(item.Sabor) &&
                    produto.SaboresQuantidadesList != null &&
                    produto.SaboresQuantidadesList.Any())
                {
                    BaixarEstoquePorSabor(produto, item.Sabor, item.Quantidade);
                }
                else
                {
                    BaixarEstoqueProdutoPrincipal(produto, item.Quantidade);
                }

                if (produto.SaboresQuantidadesList != null &&
                    produto.SaboresQuantidadesList.Any())
                {
                    produto.Estoque = produto.SaboresQuantidadesList.Sum(s => Math.Max(0, s.Quantidade));
                    produto.SerializarSaboresQuantidades();
                }

                item.EstoqueBaixado = true;
                item.EstoqueBaixadoEm = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        private static void BaixarEstoqueProdutoPrincipal(Models.ProdutoModel produto, int quantidadeVendida)
        {
            if (produto.Estoque < quantidadeVendida)
            {
                throw new InvalidOperationException(
                    $"Estoque insuficiente do produto '{produto.Nome}'. " +
                    $"Disponível: {produto.Estoque}. Solicitado: {quantidadeVendida}."
                );
            }

            produto.Estoque -= quantidadeVendida;
        }

        private static void BaixarEstoquePorSabor(Models.ProdutoModel produto, string sabor, int quantidadeVendida)
        {
            var saborEstoque = produto.SaboresQuantidadesList
                .FirstOrDefault(s => string.Equals(s.Sabor, sabor, StringComparison.OrdinalIgnoreCase));

            if (saborEstoque == null)
            {
                throw new InvalidOperationException(
                    $"Sabor '{sabor}' não encontrado no produto '{produto.Nome}'."
                );
            }

            if (saborEstoque.Quantidade < quantidadeVendida)
            {
                throw new InvalidOperationException(
                    $"Estoque insuficiente do sabor '{sabor}' do produto '{produto.Nome}'. " +
                    $"Disponível: {saborEstoque.Quantidade}. Solicitado: {quantidadeVendida}."
                );
            }

            saborEstoque.Quantidade -= quantidadeVendida;
        }
    }
}