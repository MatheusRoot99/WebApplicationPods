using SitePodsInicial.Models;
using System.Collections.Generic;

namespace SitePodsInicial.Repository.Interface
{
    public interface ICategoriaRepository
    {
        IEnumerable<CategoriaModel> ObterTodos();
        CategoriaModel ObterPorId(int id);
        void Adicionar(CategoriaModel categoria);
        void Atualizar(CategoriaModel categoria);
        void Remover(int id);
        IEnumerable<CategoriaModel> ObterCategoriasAtivas();
    }
}