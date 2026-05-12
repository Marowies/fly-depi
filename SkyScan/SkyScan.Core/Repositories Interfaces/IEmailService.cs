using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }
}
