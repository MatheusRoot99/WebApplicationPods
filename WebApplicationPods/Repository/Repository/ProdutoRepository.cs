using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WebApplicationPods.Data;
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
                .Where(p => p.Ativo)
                .OrderBy(p => p.Nome)
                .ToList();
        }

        public ProdutoModel? ObterPorId(int id)
        {
            var produto = _context.Produtos
                .Include(p => p.Categoria)
                .FirstOrDefault(p => p.Id == id);

            if (produto == null) return null;   // <- evita NRE

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
                .Where(p => p.CategoriaId == categoriaId && p.Ativo)
                .OrderBy(p => p.Nome)
                .ToList();
        }

        public IEnumerable<ProdutoModel> ObterMaisVendidos(int quantidade = 5)
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Where(p => p.Ativo)
                .OrderByDescending(p => p.PedidoItens.Sum(pi => pi.Quantidade))
                .Take(quantidade)
                .ToList();
        }


        //////////////////pods filtros////////////////
        public List<ProdutoModel> FiltrarProdutos(FiltrosModel filtros)
        {
            var query = _context.Produtos
        .Include(p => p.Categoria)
        .Where(p => p.Ativo);

            // Aplicar filtros
            if (!string.IsNullOrEmpty(filtros.Categoria))
                query = query.Where(p => p.Categoria.Nome == filtros.Categoria);

            if (!string.IsNullOrEmpty(filtros.Sabor))
                query = query.Where(p => p.Sabor == filtros.Sabor);

            if (!string.IsNullOrEmpty(filtros.Cor))
                query = query.Where(p => p.Cor == filtros.Cor);

            if (filtros.PrecoMin.HasValue)
                query = query.Where(p => p.Preco >= filtros.PrecoMin.Value);

            if (filtros.PrecoMax.HasValue)
                query = query.Where(p => p.Preco <= filtros.PrecoMax.Value);

            if (filtros.AvaliacaoMin.HasValue)
                query = query.Where(p => p.Avaliacao >= filtros.AvaliacaoMin.Value);

            if (filtros.ApenasPromocoes)
                query = query.Where(p => p.EmPromocao && p.PrecoPromocional.HasValue);

            if (filtros.ApenasEstoque)
                query = query.Where(p => p.Estoque > 0);

            // Ordenação
            query = filtros.OrdenarPor switch
            {
                "avaliacao" => query.OrderByDescending(p => p.Avaliacao),
                "recente" => query.OrderByDescending(p => p.DataCadastro),
                "preco-asc" => query.OrderBy(p => p.Preco),
                "preco-desc" => query.OrderByDescending(p => p.Preco),
                _ => query.OrderByDescending(p => p.MaisVendido) // Default: popularidade
            };

            return query.ToList(); // Já retorna List<ProdutoModel>
        }

        public List<string> ObterCategoriasDistintas()
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Select(p => p.Categoria.Nome)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public List<string> ObterSaboresDistintos()
        {
            return _context.Produtos
                .Where(p => !string.IsNullOrEmpty(p.Sabor))
                .Select(p => p.Sabor)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        public List<string> ObterCoresDistintas()
        {
            return _context.Produtos
                .Where(p => !string.IsNullOrEmpty(p.Cor))
                .Select(p => p.Cor)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public IQueryable<ProdutoModel> Query()
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .Where(p => p.Ativo)      // só ativos (ajuste se quiser)
                .AsNoTracking();           // leitura
        }

    }
}