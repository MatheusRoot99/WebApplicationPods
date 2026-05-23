using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Repository.Repository
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly BancoContext _context;

        public CategoriaRepository(BancoContext context)
        {
            _context = context;
        }

        public IEnumerable<CategoriaModel> ObterTodos()
        {
            return _context.Categorias
                .AsNoTracking()
                .OrderBy(c => c.Nome)
                .ToList();
        }

        public CategoriaModel ObterPorId(int id)
        {
            return _context.Categorias
                .Include(c => c.Produtos)
                .FirstOrDefault(c => c.Id == id)!;
        }

        public void Adicionar(CategoriaModel categoria)
        {
            if (categoria == null)
                throw new ArgumentNullException(nameof(categoria));

            _context.Categorias.Add(categoria);
            _context.SaveChanges();
        }

        public void Atualizar(CategoriaModel categoria)
        {
            if (categoria == null)
                throw new ArgumentNullException(nameof(categoria));

            _context.Categorias.Update(categoria);
            _context.SaveChanges();
        }

        public void Remover(int id)
        {
            var categoria = _context.Categorias
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
            return _context.Categorias
                .AsNoTracking()
                .Where(c => c.Produtos.Any(p => p.Ativo))
                .OrderBy(c => c.Nome)
                .ToList();
        }
    }
}