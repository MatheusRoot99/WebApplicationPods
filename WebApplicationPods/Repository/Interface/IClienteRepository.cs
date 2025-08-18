
using WebApplicationPods.Models;

namespace WebApplicationPods.Repository.Interface
{
    public interface IClienteRepository
    {
        ClienteModel ObterPorTelefone(string telefone);
        ClienteModel ObterPorId(int id);
        void Adicionar(ClienteModel cliente);
        void Atualizar(ClienteModel cliente);
        bool TelefoneExiste(string telefone);
        EnderecoModel ObterEnderecoPrincipal(int clienteId);
        IEnumerable<EnderecoModel> ObterEnderecos(int clienteId);
        


    }


}
