using WebApplicationPods.Models;

namespace WebApplicationPods.Services.Interface
{
    public interface ICepService
    {
        Task<EnderecoModel> BuscarCepAsync(string cep);
    }
}
