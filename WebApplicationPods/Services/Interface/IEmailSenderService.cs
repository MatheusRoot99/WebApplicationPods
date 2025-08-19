namespace WebApplicationPods.Services.Interface
{
    public interface IEmailSenderService
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}
