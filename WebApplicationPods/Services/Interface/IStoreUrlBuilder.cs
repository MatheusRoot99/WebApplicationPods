namespace WebApplicationPods.Services.Interface
{
    public interface IStoreUrlBuilder
    {
        string GetRootHostWithPort();
        string GetScheme();
        string BuildPublicStoreUrl(string subdominio);
    }
}
