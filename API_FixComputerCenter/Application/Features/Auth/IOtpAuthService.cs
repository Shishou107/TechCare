using System.Threading;
using System.Threading.Tasks;

namespace API_FixComputerCenter.Application.Features.Auth;

public record RequestOtpRequest(string PhoneNumber);
public record RequestOtpResponse(bool Success, string Message);
public record VerifyOtpRequest(string PhoneNumber, string Code);

public interface IOtpAuthService
{
    Task<RequestOtpResponse> RequestOtpAsync(string phoneNumber, string? ipAddress, CancellationToken ct = default);
    Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default);
}
