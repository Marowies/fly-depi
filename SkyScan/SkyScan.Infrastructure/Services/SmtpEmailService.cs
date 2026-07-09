using Microsoft.Extensions.Configuration;
using SkyScan.Core.Services.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var smtpSection = _configuration.GetSection("Smtp");
            var host     = smtpSection["Host"]     ?? "smtp.gmail.com";
            var port     = int.Parse(smtpSection["Port"] ?? "587");
            var fromEmail = smtpSection["FromEmail"] ?? "";
            var fromName  = smtpSection["FromName"]  ?? "SkyScan";
            var password  = smtpSection["Password"]  ?? "";

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(fromEmail, password)
            };

            var message = new MailMessage
            {
                From       = new MailAddress(fromEmail, fromName),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
    }
}
