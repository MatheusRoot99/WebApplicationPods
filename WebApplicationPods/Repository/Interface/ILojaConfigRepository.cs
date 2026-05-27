using WebApplicationPods.Models;

namespace WebApplicationPods.Repository.Interface
{
    public interface ILojaConfigRepository
    {
        LojaConfig? ObterDoLojistaAtual();

        LojaConfig? ObterPorUserId(string userId);

        LojaConfig? ObterPorLojaId(int lojaId);

        LojaConfig Salvar(LojaConfig config);
    }
}
