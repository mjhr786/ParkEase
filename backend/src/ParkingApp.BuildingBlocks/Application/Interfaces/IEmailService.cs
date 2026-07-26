using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParkingApp.Application.Interfaces
{
    public sealed record EmailAttachment(
        string FileName,
        string ContentType,
        byte[] Content);

    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);

        Task SendEmailAsync(
            string to,
            string subject,
            string body,
            IReadOnlyList<EmailAttachment>? attachments,
            bool isHtml = true);
    }
}
