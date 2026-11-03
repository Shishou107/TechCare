using System.Threading;
using System.Threading.Tasks;

namespace API_FixComputerCenter.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
}
