using API_FixComputerCenter.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Infrastructure.Sms;

public class LogOnlySmsProvider : ISmsProvider
{
    private readonly ILogger<LogOnlySmsProvider> _logger;
    private readonly IHostEnvironment _env;

    public LogOnlySmsProvider(ILogger<LogOnlySmsProvider> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public Task<bool> SendOtpAsync(string phoneNumber, string otpCode, CancellationToken ct = default)
    {
        if (_env.IsDevelopment())
        {
            _logger.LogInformation("[DEV SMS OTP] To: {PhoneNumber} | Code: {OtpCode} (Expires in 5 minutes)", phoneNumber, otpCode);
        }
        else
        {
            _logger.LogInformation("[SMS OTP] Dispatched OTP code to {PhoneNumber}", phoneNumber);
        }
        return Task.FromResult(true);
    }
}
