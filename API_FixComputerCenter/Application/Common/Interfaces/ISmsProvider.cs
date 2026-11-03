namespace API_FixComputerCenter.Application.Common.Interfaces;

public interface ISmsProvider
{
    Task<bool> SendOtpAsync(string phoneNumber, string otpCode, CancellationToken ct = default);
}
