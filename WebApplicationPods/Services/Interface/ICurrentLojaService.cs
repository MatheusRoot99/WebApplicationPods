namespace WebApplicationPods.Services.Interface
{
    public interface ICurrentLojaService
    {
        int? LojaId { get; }
        bool HasLoja { get; }

        void SetLojaId(int lojaId);
        void ClearLoja();
    }
}
