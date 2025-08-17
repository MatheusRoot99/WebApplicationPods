
using System.Collections.Generic;
using WebApplicationPods.Models;

namespace WebApplicationPods.Repository.Interface
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

        List<ProdutoModel> FiltrarProdutos(FiltrosModel filtros);
        List<string> ObterCategoriasDistintas();
        List<string> ObterSaboresDistintos();
        List<string> ObterCoresDistintas();
    }
}