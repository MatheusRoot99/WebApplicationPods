using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Repository.Repository
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly BancoContext _context;
        private readonly ICurrentLojaService _currentLoja;
        private readonly IHttpContextAccessor _http;

        public CategoriaRepository(
            BancoContext context,
            ICurrentLojaService currentLoja,
            IHttpContextAccessor http)
        {
            _context = context;
            _currentLoja = currentLoja;
            _http = http;
        }

        private IQueryable<CategoriaModel> BaseQuery()
        {
            var q = _context.Categorias.AsQueryable();

            if (_http.HttpContext?.User?.IsInRole("Admin") == true)
                return q;

            if (_currentLoja?.LojaId is int lojaId && lojaId > 0)
                q = q.Where(c => c.LojaId == lojaId);

            return q;
        }

        public IEnumerable<CategoriaModel> ObterTodos()
        {
            return BaseQuery()
                .AsNoTracking()
                .OrderBy(c => c.Nome)
                .ToList();
        }

        public CategoriaModel ObterPorId(int id)
        {
            return BaseQuery()
                .Include(c => c.Produtos)
                .FirstOrDefault(c => c.Id == id)!;
        }

        public void Adicionar(CategoriaModel categoria)
        {
            if (categoria == null)
                throw new ArgumentNullException(nameof(categoria));

            if (_http.HttpContext?.User?.IsInRole("Admin") != true &&
                _currentLoja?.LojaId is int lojaId &&
                lojaId > 0)
            {
                categoria.LojaId = lojaId;
            }

            _context.Categorias.Add(categoria);
            _context.SaveChanges();
        }

        public void Atualizar(CategoriaModel categoria)
        {
            if (categoria == null)
                throw new ArgumentNullException(nameof(categoria));

            if (_http.HttpContext?.User?.IsInRole("Admin") != true &&
                _currentLoja?.LojaId is int lojaId &&
                lojaId > 0)
            {
                var pertenceLoja = BaseQuery()
                    .Any(c => c.Id == categoria.Id);

                if (!pertenceLoja)
                    throw new UnauthorizedAccessException("Categoria não pertence à loja atual.");

                categoria.LojaId = lojaId;
            }

            _context.Categorias.Update(categoria);
            _context.SaveChanges();
        }

        public void Remover(int id)
        {
            var categoria = BaseQuery()
                .Include(c => c.Produtos)
                .FirstOrDefault(c => c.Id == id);

            if (categoria == null)
                return;

            if (categoria.Produtos.Any())
                throw new InvalidOperationException("Não é possível excluir categoria com produtos vinculados.");

            _context.Categorias.Remove(categoria);
            _context.SaveChanges();
        }

        public IEnumerable<CategoriaModel> ObterCategoriasAtivas()
        {
            return BaseQuery()
                .AsNoTracking()
                .Where(c => c.Produtos.Any(p => p.Ativo))
                .OrderBy(c => c.Nome)
                .ToList();
        }
    }
}