using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Application.Features.Auth;

public class OtpAuthService : IOtpAuthService
{
    private const int MaxFailedAttempts = 5;
    private const int CooldownSeconds = 60;
    private const int ExpirationMinutes = 5;
    private const int MaxDailyRequestsPerPhone = 10;
    private const string GenericMessage = "Nếu số điện thoại hợp lệ, mã xác thực đã được gửi.";

    private readonly ITechCareDbContext _db;
    private readonly ISmsProvider _smsProvider;
    private readonly IAuthService _authService;
    private readonly IAuditService _audit;
    private readonly ILogger<OtpAuthService> _logger;

    public OtpAuthService(
        ITechCareDbContext db,
        ISmsProvider smsProvider,
        IAuthService authService,
        IAuditService audit,
        ILogger<OtpAuthService> logger)
    {
        _db = db;
        _smsProvider = smsProvider;
        _authService = authService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<RequestOtpResponse> RequestOtpAsync(string phoneNumber, string? ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new RequestOtpResponse(true, GenericMessage);
        }

        var normalizedPhone = NormalizePhone(phoneNumber);
        if (!Regex.IsMatch(normalizedPhone, @"^0\d{9,10}$"))
        {
            // Generic message to avoid phone enumeration
            return new RequestOtpResponse(true, GenericMessage);
        }

        // Check 60-second cooldown
        var cooldownCutoff = DateTime.UtcNow.AddSeconds(-CooldownSeconds);
        var recentRequest = await _db.OtpCodes.AsNoTracking()
            .AnyAsync(o => o.PhoneNumber == normalizedPhone && o.ConsumedAtUtc == null && o.CreatedAtUtc > cooldownCutoff, ct);

        if (recentRequest)
        {
            _logger.LogWarning("OTP request rate limited by cooldown for phone {Phone}", normalizedPhone);
            return new RequestOtpResponse(true, GenericMessage);
        }

        // Check daily request limit
        var dailyCutoff = DateTime.UtcNow.AddHours(-24);
        var dailyCount = await _db.OtpCodes.AsNoTracking()
            .CountAsync(o => o.PhoneNumber == normalizedPhone && o.CreatedAtUtc > dailyCutoff, ct);

        if (dailyCount >= MaxDailyRequestsPerPhone)
        {
            _logger.LogWarning("OTP request exceeded daily limit for phone {Phone}", normalizedPhone);
            return new RequestOtpResponse(true, GenericMessage);
        }

        // Cryptographically secure 6-digit OTP code
        var codeNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
        var code = codeNumber.ToString("D6");
        var codeHash = SHA256.HashData(Encoding.UTF8.GetBytes(code));

        var otp = new OtpCode
        {
            PhoneNumber = normalizedPhone,
            CodeHash = codeHash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(ExpirationMinutes),
            FailedAttempts = 0,
            IpAddress = ipAddress,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.OtpCodes.Add(otp);
        await _db.SaveChangesAsync(ct);

        await _smsProvider.SendOtpAsync(normalizedPhone, code, ct);
        await _audit.LogAsync("OTP_REQUEST", nameof(OtpCode), otp.OtpId.ToString(), null, new { phone = normalizedPhone, ipAddress }, ct);

        return new RequestOtpResponse(true, GenericMessage);
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.PhoneNumber))
        {
            throw new ValidationException("Số điện thoại không được để trống.");
        }

        var normalizedPhone = NormalizePhone(req.PhoneNumber);
        var inputCode = (req.Code ?? string.Empty).Trim();

        var otp = await _db.OtpCodes
            .Where(o => o.PhoneNumber == normalizedPhone && o.ConsumedAtUtc == null)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (otp == null)
        {
            throw new ValidationException("Mã xác thực không hợp lệ hoặc đã được sử dụng.");
        }

        if (otp.FailedAttempts >= MaxFailedAttempts)
        {
            throw new ValidationException("Mã xác thực đã bị khóa do nhập sai quá 5 lần. Vui lòng yêu cầu mã mới.");
        }

        if (otp.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ValidationException("Mã xác thực đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        var inputHash = SHA256.HashData(Encoding.UTF8.GetBytes(inputCode));
        if (!CryptographicOperations.FixedTimeEquals(inputHash, otp.CodeHash))
        {
            otp.FailedAttempts++;
            await _db.SaveChangesAsync(ct);

            var remaining = Math.Max(0, MaxFailedAttempts - otp.FailedAttempts);
            if (remaining == 0)
            {
                throw new ValidationException("Mã xác thực đã bị khóa do nhập sai quá 5 lần. Vui lòng yêu cầu mã mới.");
            }

            throw new ValidationException($"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
        }

        // OTP Valid - consume it
        otp.ConsumedAtUtc = DateTime.UtcNow;

        // 1. Find or create CRM Customer
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == normalizedPhone, ct);
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = "KH" + DateTime.UtcNow.ToString("yyMMddHHmmss") + RandomNumberGenerator.GetInt32(100, 999),
                FullName = "Khách hàng " + normalizedPhone,
                Phone = normalizedPhone,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                RowVersion = new byte[8]
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
        }

        // 2. Find or create Security User (role CUSTOMER)
        User? user = null;
        if (customer.UserId.HasValue)
        {
            user = await _db.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.Permissions)
                .FirstOrDefaultAsync(u => u.UserId == customer.UserId.Value, ct);
        }

        var normalizedUserName = normalizedPhone.ToUpperInvariant();
        if (user == null)
        {
            user = await _db.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.Permissions)
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);
        }

        if (user == null)
        {
            user = new User
            {
                UserName = normalizedPhone,
                NormalizedUserName = normalizedUserName,
                PhoneNumber = normalizedPhone,
                IsPhoneConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString(),
                RowVersion = new byte[8]
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            var customerRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleCode == "CUSTOMER", ct);
            if (customerRole != null)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = customerRole.RoleId
                });
                await _db.SaveChangesAsync(ct);
            }

            // Reload user with role & permissions
            user = await _db.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.Permissions)
                .FirstAsync(u => u.UserId == user.UserId, ct);
        }

        // Link Customer to User if not linked
        if (customer.UserId != user.UserId)
        {
            customer.UserId = user.UserId;
            customer.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("OTP_VERIFY_SUCCESS", nameof(Customer), customer.CustomerId.ToString(), null, new { phone = normalizedPhone, user.UserId, ipAddress }, ct);

        // Issue tokens via standard AuthService session management
        return await _authService.CreateSessionAndTokenForUserAsync(user, ipAddress, userAgent, ct);
    }

    private static string NormalizePhone(string phone)
    {
        var cleaned = Regex.Replace(phone.Trim(), @"[\s\-\.\(\)]", "");
        if (cleaned.StartsWith("+84"))
        {
            cleaned = "0" + cleaned[3..];
        }
        else if (cleaned.StartsWith("84") && cleaned.Length >= 11)
        {
            cleaned = "0" + cleaned[2..];
        }
        return cleaned;
    }
}

