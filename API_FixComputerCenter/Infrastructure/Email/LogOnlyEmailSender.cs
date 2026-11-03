using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Infrastructure.Email;

public class LogOnlyEmailSender : IEmailSender
{
    private readonly ILogger<LogOnlyEmailSender> _logger;

    public LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV EMAIL SENDER] To: {ToEmail} | Subject: {Subject} | Body: {Body}", toEmail, subject, bodyHtml);
        return Task.CompletedTask;
    }
}
