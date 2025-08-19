using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class GmailEmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _cfg;
        public GmailEmailSenderService(IConfiguration cfg) => _cfg = cfg;

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _cfg["Smtp:Host"];
            var port = int.Parse(_cfg["Smtp:Port"] ?? "587");
            var user = _cfg["Smtp:User"];
            var pass = _cfg["Smtp:Password"];
            var fromName = _cfg["Smtp:FromName"] ?? user;
            var fromAddress = _cfg["Smtp:FromAddress"] ?? user;

            using var msg = new MailMessage();
            msg.From = new MailAddress(fromAddress, fromName);
            msg.To.Add(toEmail);
            msg.Subject = subject;
            msg.Body = htmlBody;
            msg.IsBodyHtml = true;

            using var smtp = new SmtpClient(host, port);
            smtp.Credentials = new NetworkCredential(user, pass);
            smtp.EnableSsl = true;

            await smtp.SendMailAsync(msg);
        }
    }
}
