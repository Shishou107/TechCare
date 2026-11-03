using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        var host = _config["TECHCARE_SMTP_HOST"] ?? _config["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SMTP Host chưa được cấu hình. Bỏ qua gửi email thật đến {ToEmail}.", toEmail);
            return;
        }

        var portStr = _config["TECHCARE_SMTP_PORT"] ?? _config["Email:Smtp:Port"] ?? "587";
        int.TryParse(portStr, out var port);
        if (port <= 0) port = 587;

        var user = _config["TECHCARE_SMTP_USER"] ?? _config["Email:Smtp:User"];
        var pass = _config["TECHCARE_SMTP_PASSWORD"] ?? _config["Email:Smtp:Password"];
        var from = _config["TECHCARE_SMTP_FROM"] ?? _config["Email:Smtp:From"] ?? "no-reply@techcare.vn";
        var fromName = _config["TECHCARE_SMTP_FROM_NAME"] ?? _config["Email:Smtp:FromName"] ?? "TechCare Center";
        var sslStr = _config["TECHCARE_SMTP_ENABLE_SSL"] ?? _config["Email:Smtp:EnableSsl"] ?? "true";
        bool.TryParse(sslStr, out var enableSsl);

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(from, fromName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Đã gửi email thành công đến {ToEmail} qua SMTP server {Host}.", toEmail, host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email đến {ToEmail} qua SMTP: {Message}", toEmail, ex.Message);
            throw;
        }
    }
}
