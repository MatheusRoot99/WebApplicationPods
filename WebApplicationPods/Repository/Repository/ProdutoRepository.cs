using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Repository.Repository
{
    public class ProdutoRepository : IProdutoRepository
    {
        private readonly BancoContext _context;

        public ProdutoRepository(BancoContext context)
        {
            _context = context;
        }

        public IEnumerable<ProdutoModel> ObterTodos()
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Include(p => p.Variacoes)
                .Where(p => p.Ativo)
                .OrderBy(p => p.Nome)
                .ToList();
        }

        public ProdutoModel? ObterPorId(int id)
        {
            var produto = _context.Produtos
                .Include(p => p.Categoria)
                .Include(p => p.Variacoes)
                .FirstOrDefault(p => p.Id == id);

            if (produto == null) return null;

            if (!string.IsNullOrEmpty(produto.SaboresQuantidades))
            {
                try
                {
                    produto.SaboresQuantidadesList =
                        JsonConvert.DeserializeObject<List<ProdutoModel.SaborQuantidade>>(produto.SaboresQuantidades)
                        ?? new List<ProdutoModel.SaborQuantidade>();
                }
                catch
                {
                    produto.SaboresQuantidadesList = new List<ProdutoModel.SaborQuantidade>();
                }
            }
            else
            {
                produto.SaboresQuantidadesList = new List<ProdutoModel.SaborQuantidade>();
            }

            return produto;
        }

        public void Adicionar(ProdutoModel produto)
        {
            _context.Produtos.Add(produto);
            _context.SaveChanges();
        }

        public void Atualizar(ProdutoModel produto)
        {
            _context.Produtos.Update(produto);
            _context.SaveChanges();
        }

        public void Remover(int id)
        {
            var produto = _context.Produtos.Find(id);
            if (produto != null)
            {
                produto.Ativo = false;
                _context.SaveChanges();
            }
        }

        public IEnumerable<ProdutoModel> ObterPorCategoria(int categoriaId)
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Include(p => p.Variacoes)
                .Where(p => p.CategoriaId == categoriaId && p.Ativo)
                .OrderBy(p => p.Nome)
                .ToList();
        }

        public IEnumerable<ProdutoModel> ObterMaisVendidos(int quantidade = 5)
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Include(p => p.Variacoes)
                .Where(p => p.Ativo)
                .OrderByDescending(p => p.PedidoItens.Sum(pi => pi.Quantidade))
                .Take(quantidade)
                .ToList();
        }

        public IQueryable<ProdutoModel> Query()
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Include(p => p.Variacoes)
                .Where(p => p.Ativo)
                .AsNoTracking();
        }

        public IEnumerable<ProdutoModel> ObterMaisPopulares(int take = 8)
        {
            return _context.Produtos
                .Include(p => p.Variacoes)
                .Where(p => p.Ativo && p.Estoque > 0)
                .OrderByDescending(p => p.PedidoItens.Count())
                .ThenByDescending(p => p.MaisVendido)
                .ThenByDescending(p => p.Avaliacao)
                .ThenByDescending(p => p.EmPromocao)
                .ThenByDescending(p => p.DataCadastro)
                .Take(take)
                .ToList();
        }

        // =============================
        // HOME: FILTROS (CORRIGIDO)
        // =============================
        public List<ProdutoModel> FiltrarProdutos(FiltrosModel filtros)
        {
            var query = _context.Produtos
                .Include(p => p.Categoria)
                .Where(p => p.Ativo);

            if (!string.IsNullOrWhiteSpace(filtros.Termo))
            {
                var t = filtros.Termo.Trim();
                query = query.Where(p =>
                    p.Nome.Contains(t) ||
                    (p.Descricao != null && p.Descricao.Contains(t)) ||
                    (p.Marca != null && p.Marca.Contains(t)));
            }

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
                query = query.Where(p => p.Categoria != null && p.Categoria.Nome == filtros.Categoria);

            // (Se ainda quiser usar enquanto existir campo)
            if (!string.IsNullOrWhiteSpace(filtros.Sabor))
                query = query.Where(p => p.Sabor == filtros.Sabor);

            if (!string.IsNullOrWhiteSpace(filtros.Cor))
                query = query.Where(p => p.Cor == filtros.Cor);

            if (filtros.PrecoMin.HasValue)
                query = query.Where(p => p.Preco >= filtros.PrecoMin.Value);

            if (filtros.PrecoMax.HasValue)
                query = query.Where(p => p.Preco <= filtros.PrecoMax.Value);

            if (filtros.AvaliacaoMin.HasValue)
                query = query.Where(p => p.Avaliacao >= filtros.AvaliacaoMin.Value);

            if (filtros.ApenasPromocoes)
                query = query.Where(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco);

            if (filtros.ApenasEstoque)
                query = query.Where(p => p.Estoque > 0);

            query = filtros.OrdenarPor switch
            {
                "avaliacao" => query.OrderByDescending(p => p.Avaliacao),
                "recente" => query.OrderByDescending(p => p.DataCadastro),
                "preco-asc" => query.OrderBy(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco ? p.PrecoPromocional : p.Preco),
                "preco-desc" => query.OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco ? p.PrecoPromocional : p.Preco),
                _ => query.OrderByDescending(p => p.MaisVendido)
            };

            return query.AsNoTracking().ToList();
        }



        public List<string> ObterCategoriasDistintas()
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Where(p => p.Ativo && p.Categoria != null && !string.IsNullOrEmpty(p.Categoria.Nome))
                .Select(p => p.Categoria!.Nome)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        // ✅ AGORA PEGA OS SABORES DO POD NAS VARIAÇÕES
        public List<string> ObterSaboresDistintos()
        {
            return new List<string>(); // não usamos mais
        }


        public List<string> ObterCoresDistintas()
        {
            return _context.Produtos
                .Where(p => p.Ativo && !string.IsNullOrEmpty(p.Cor))
                .Select(p => p.Cor)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }
    }
}
