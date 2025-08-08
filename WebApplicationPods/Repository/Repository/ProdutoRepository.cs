using Microsoft.EntityFrameworkCore;
using SitePodsInicial.Data;
using SitePodsInicial.Models;
using SitePodsInicial.Repository.Interface;
using System.Collections.Generic;
using System.Linq;

namespace SitePodsInicial.Repository.Repository
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

        public ProdutoModel ObterPorId(int id)
        {
            return _context.Produtos
                .Include(p => p.Categoria)
                .FirstOrDefault(p => p.Id == id);
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
    }
}