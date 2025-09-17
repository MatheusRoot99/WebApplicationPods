using WebApplicationPods.Models;

namespace WebApplicationPods.Repository.Interface
{
    public interface ILojaConfigRepository
    {
        /// <summary>Retorna a config da loja do lojista logado.
        /// Faz fallback para a primeira loja configurada (ou null se não houver).</summary>
        LojaConfig? ObterDoLojistaAtual();

        /// <summary>Retorna a config de loja pelo UserId do lojista.</summary>
        LojaConfig? ObterPorUserId(string userId);

        /// <summary>Insere ou atualiza a config.</summary>
        LojaConfig Salvar(LojaConfig config);
    }
}
