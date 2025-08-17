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
                .OrderBy(c => c.Nome)
                .ToList();
        }

        public CategoriaModel ObterPorId(int id)
        {
            return _context.Categorias.Find(id);
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
            _context.Categorias.Update(categoria);
            _context.SaveChanges();
        }

        public void Remover(int id)
        {
            var categoria = _context.Categorias.Find(id);
            if (categoria != null)
            {
                _context.Categorias.Remove(categoria);
                _context.SaveChanges();
            }
        }

        public IEnumerable<CategoriaModel> ObterCategoriasAtivas()
        {
            return _context.Categorias
                .Where(c => !c.Produtos.Any(p => !p.Ativo))
                .OrderBy(c => c.Nome)
                .ToList();
        }
    }
}