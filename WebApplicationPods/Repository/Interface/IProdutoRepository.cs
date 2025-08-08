using SitePodsInicial.Models;
using System.Collections.Generic;

namespace SitePodsInicial.Repository.Interface
{
    public interface IProdutoRepository
    {
        IEnumerable<ProdutoModel> ObterTodos();
        ProdutoModel ObterPorId(int id);
        void Adicionar(ProdutoModel produto);
        void Atualizar(ProdutoModel produto);
        void Remover(int id);
        IEnumerable<ProdutoModel> ObterPorCategoria(int categoriaId);
        IEnumerable<ProdutoModel> ObterMaisVendidos(int quantidade);
    }
}