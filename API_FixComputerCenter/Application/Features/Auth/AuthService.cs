using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace API_FixComputerCenter.Application.Features.Auth;

public record LoginRequest(string UserName, string Password);
public record RefreshTokenRequest(string? RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UserSessionDto(Guid SessionId, string? DeviceLabel, string? CreatedByIp, DateTime CreatedAtUtc, DateTime LastUsedAtUtc, bool IsCurrent);
public record AuthResponse(
    string AccessToken,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    UserProfileDto User
);
public record UserProfileDto(long UserId, string UserName, string? Email, int? BranchId, List<string> Roles, List<string> Permissions);
public record RegisterCustomerRequest(string FullName, string Email, string PhoneNumber, string Password, string ConfirmPassword);
public record RegisterCustomerResponse(long UserId, string Email, string Message);
public record VerifyEmailRequest(string Email, string Code);
public record ResendVerificationEmailRequest(string Email);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Code, string NewPassword, string ConfirmPassword);

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> CreateSessionAndTokenForUserAsync(User user, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(string rawRefreshToken, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task LogoutAsync(string? rawRefreshToken, Guid? sessionId, CancellationToken ct = default);
    Task LogoutAllAsync(long userId, CancellationToken ct = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest req, CancellationToken ct = default);
    Task<List<UserSessionDto>> GetUserSessionsAsync(long userId, Guid? currentSessionId, CancellationToken ct = default);
    Task RevokeSessionAsync(long userId, Guid sessionId, CancellationToken ct = default);
    Task AdminRevokeUserSessionsAsync(long targetUserId, CancellationToken ct = default);
    Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken ct = default);
    Task<RegisterCustomerResponse> RegisterCustomerAsync(RegisterCustomerRequest req, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task ResendVerificationEmailAsync(string email, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private const int MaxActiveSessionsPerUser = 5;
    private const int Pbkdf2Iterations = 100000;
    private const int AccessTokenExpirationMinutes = 15;
    private const int RefreshTokenExpirationDays = 14;
    private const int SessionAbsoluteExpirationDays = 30;
    private const int GraceWindowSeconds = 20;

    private readonly ITechCareDbContext _db;
    private readonly IConfiguration _config;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IMemoryCache _cache;
    private readonly IEmailSender _emailSender;

    public AuthService(ITechCareDbContext db, IConfiguration config, ICurrentUserService currentUser, IAuditService audit, IMemoryCache cache)
        : this(db, config, currentUser, audit, cache, new Infrastructure.Email.LogOnlyEmailSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.Email.LogOnlyEmailSender>.Instance))
    {
    }

    public AuthService(ITechCareDbContext db, IConfiguration config, ICurrentUserService currentUser, IAuditService audit, IMemoryCache cache, IEmailSender emailSender)
    {
        _db = db;
        _config = config;
        _currentUser = currentUser;
        _audit = audit;
        _cache = cache;
        _emailSender = emailSender;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var normalized = req.UserName.Trim().ToUpperInvariant();
        var user = await _db.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized, ct);

        // Fail closed with identical generic error message
        if (user == null || !user.IsActive || user.DeletedAtUtc != null)
        {
            throw new UnauthorizedException("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // 1. Check lockout BEFORE verifying password
        if (user.LockoutEnabled && user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remaining = (user.LockoutEndUtc.Value - DateTime.UtcNow).Minutes + 1;
            throw new UnauthorizedException($"Tài khoản đang bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {remaining} phút.");
        }

        // 2. Verify password with OWASP PBKDF2 (No bypass, no plaintext)
        bool isValid = VerifyPasswordHash(req.Password, user.PasswordHash);
        if (!isValid)
        {
            // S3 / A7: Atomic single-statement update for lockout counter & lockout end time
            if (_db.Database.IsRelational())
            {
                await _db.Users.Where(u => u.UserId == user.UserId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.AccessFailedCount, x => x.AccessFailedCount + 1)
                        .SetProperty(x => x.LockoutEndUtc, x => (x.AccessFailedCount + 1 >= 5 && x.LockoutEnabled) ? DateTime.UtcNow.AddMinutes(15) : x.LockoutEndUtc), ct);
            }
            else
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5 && user.LockoutEnabled)
                {
                    user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                }
                await _db.SaveChangesAsync(ct);
            }

            throw new UnauthorizedException("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // Reset failed count on successful password
        if (_db.Database.IsRelational())
        {
            await _db.Users.Where(u => u.UserId == user.UserId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AccessFailedCount, 0)
                    .SetProperty(x => x.LockoutEndUtc, (DateTime?)null)
                    .SetProperty(x => x.LastLoginAtUtc, DateTime.UtcNow), ct);
        }
        else
        {
            user.AccessFailedCount = 0;
            user.LockoutEndUtc = null;
            user.LastLoginAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        // 3. Check email confirmation for CUSTOMER
        var isCustomer = user.UserRoleUsers.Any(ur => ur.Role != null && ur.Role.RoleCode == "CUSTOMER");
        if (isCustomer && !user.IsEmailConfirmed)
        {
            throw new EmailNotVerifiedException(user.Email ?? user.UserName, "Tài khoản của bạn chưa được xác thực email. Vui lòng nhập mã xác thực 6 số đã được gửi đến email của bạn.");
        }

        return await CreateSessionAndTokenForUserAsync(user, ipAddress, userAgent, ct);
    }

    public async Task<AuthResponse> CreateSessionAndTokenForUserAsync(User user, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString();
            if (_db.Database.IsRelational())
            {
                await _db.Users.Where(u => u.UserId == user.UserId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SecurityStamp, user.SecurityStamp), ct);
            }
            else
            {
                await _db.SaveChangesAsync(ct);
            }
        }

        // 3. Enforce session limit (Max 5 per user, revoke oldest if exceeded) in atomic lock (S4)
        IDbContextTransaction? tx = null;
        if (_db.Database.IsRelational())
        {
            tx = await _db.Database.BeginTransactionAsync(ct);
            // Acquire row-level update lock (UPDLOCK) on this user's record to prevent concurrent login race conditions
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM Security.Users WITH (UPDLOCK, ROWLOCK) WHERE UserId = {user.UserId}", ct);
        }

        var activeSessions = await _db.Sessions
            .Where(s => s.UserId == user.UserId && s.RevokedAtUtc == null && s.AbsoluteExpiresAtUtc > DateTime.UtcNow)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);

        if (activeSessions.Count >= MaxActiveSessionsPerUser)
        {
            var toRevokeCount = activeSessions.Count - MaxActiveSessionsPerUser + 1;
            for (int i = 0; i < toRevokeCount; i++)
            {
                var oldSession = activeSessions[i];
                oldSession.RevokedAtUtc = DateTime.UtcNow;
                oldSession.RevokeReason = "SESSION_LIMIT";

                if (_db.Database.IsRelational())
                {
                    await _db.RefreshTokens
                        .Where(t => t.SessionId == oldSession.SessionId && t.RevokedAtUtc == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
                }
                else
                {
                    var tokens = await _db.RefreshTokens
                        .Where(t => t.SessionId == oldSession.SessionId && t.RevokedAtUtc == null)
                        .ToListAsync(ct);
                    foreach (var tok in tokens) tok.RevokedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        // 4. Create new Session & Refresh Token
        var sessionId = Guid.NewGuid();
        var deviceLabel = ParseDeviceLabel(userAgent);
        var session = new Session
        {
            SessionId = sessionId,
            UserId = user.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            LastUsedAtUtc = DateTime.UtcNow,
            AbsoluteExpiresAtUtc = DateTime.UtcNow.AddDays(SessionAbsoluteExpirationDays),
            CreatedByIp = ipAddress,
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
            DeviceLabel = deviceLabel,
            RowVersion = Array.Empty<byte>()
        };
        _db.Sessions.Add(session);

        var (rawRefreshToken, tokenHash) = GenerateOpaqueRefreshToken();
        var refreshToken = new RefreshToken
        {
            SessionId = sessionId,
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(ct);
        if (tx != null)
        {
            await tx.CommitAsync(ct);
            await tx.DisposeAsync();
        }

        var roles = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.RoleCode)
            .Distinct()
            .ToList();

        var permissions = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => p.PermissionCode)
            .Distinct()
            .ToList();

        // 5. Generate 15-minute access token with sid and stamp
        var accessToken = GenerateJwtToken(user, sessionId, roles, permissions);

        await _audit.LogAsync("LOGIN_SUCCESS", nameof(User), user.UserId.ToString(), null, new { user.UserName, sessionId, ipAddress }, ct);

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresInSeconds: AccessTokenExpirationMinutes * 60,
            User: new UserProfileDto(user.UserId, user.UserName, user.Email, user.BranchId, roles, permissions)
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(string rawRefreshToken, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw new UnauthorizedException("Refresh token không được để trống.");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));

        // Atomic compare-and-swap (Only ONE concurrent request can rotate ACTIVE -> ROTATED)
        var affected = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RotatedAtUtc == null && t.RevokedAtUtc == null && t.ExpiresAtUtc > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RotatedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.LastUsedByIp, ipAddress), ct);

        if (affected == 1)
        {
            // WINNER: proceed to rotate and issue new token pair
            var oldToken = await _db.RefreshTokens
                .Include(t => t.Session)
                .Include(t => t.User)
                    .ThenInclude(u => u.UserRoleUsers)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.Permissions)
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            if (oldToken == null || oldToken.Session == null || oldToken.User == null)
            {
                throw new UnauthorizedException("Phiên đăng nhập không hợp lệ.");
            }

            var session = oldToken.Session;
            var user = oldToken.User;

            // Validate session alive & user active
            if (session.RevokedAtUtc != null || session.AbsoluteExpiresAtUtc <= DateTime.UtcNow ||
                !user.IsActive || user.DeletedAtUtc != null ||
                (user.LockoutEndUtc.HasValue && user.LockoutEndUtc > DateTime.UtcNow))
            {
                session.RevokedAtUtc ??= DateTime.UtcNow;
                session.RevokeReason ??= "USER_DISABLED";
                await _db.SaveChangesAsync(ct);
                throw new UnauthorizedException("Phiên đăng nhập đã hết hạn hoặc tài khoản bị vô hiệu hóa.");
            }

            // Issue new refresh token (Rotate)
            var (newRawRt, newHash) = GenerateOpaqueRefreshToken();
            oldToken.ReplacedByTokenHash = newHash;
            session.LastUsedAtUtc = DateTime.UtcNow;

            var newRefreshToken = new RefreshToken
            {
                SessionId = session.SessionId,
                UserId = user.UserId,
                TokenHash = newHash,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays) > session.AbsoluteExpiresAtUtc
                    ? session.AbsoluteExpiresAtUtc
                    : DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
            _db.RefreshTokens.Add(newRefreshToken);

            await _db.SaveChangesAsync(ct);

            var roles = user.UserRoleUsers
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role.RoleCode)
                .Distinct()
                .ToList();

            var permissions = user.UserRoleUsers
                .Where(ur => ur.Role != null)
                .SelectMany(ur => ur.Role.Permissions)
                .Select(p => p.PermissionCode)
                .Distinct()
                .ToList();

            var newAccessToken = GenerateJwtToken(user, session.SessionId, roles, permissions);

            // A2: Cache the issued pair keyed by old token hash for grace retry
            var oldHex = Convert.ToHexString(oldToken.TokenHash);
            _cache.Set($"rt_grace_{oldHex}", (newRawRt, newAccessToken), TimeSpan.FromSeconds(GraceWindowSeconds));

            return new AuthResponse(
                AccessToken: newAccessToken,
                RefreshToken: newRawRt,
                TokenType: "Bearer",
                ExpiresInSeconds: AccessTokenExpirationMinutes * 60,
                User: new UserProfileDto(user.UserId, user.UserName, user.Email, user.BranchId, roles, permissions)
            );
        }

        // LOSER or NON-ACTIVE TOKEN: Inspect token by hash to classify
        var token = await _db.RefreshTokens
            .Include(t => t.Session)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token == null)
        {
            throw new UnauthorizedException("Refresh token không tồn tại.");
        }

        // A2: Grace window handling: if rotated within 20s and from same IP -> Grace Retry with the new pair!
        if (token.RotatedAtUtc.HasValue && token.ReplacedByTokenHash != null)
        {
            var elapsed = DateTime.UtcNow - token.RotatedAtUtc.Value;
            if (elapsed <= TimeSpan.FromSeconds(GraceWindowSeconds) && (token.LastUsedByIp == ipAddress || token.CreatedByIp == ipAddress))
            {
                var hex = Convert.ToHexString(token.TokenHash);
                if (_cache.TryGetValue($"rt_grace_{hex}", out (string NewRt, string NewAt) cachedPair))
                {
                    var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == token.UserId, ct);
                    return new AuthResponse(
                        AccessToken: cachedPair.NewAt,
                        RefreshToken: cachedPair.NewRt, // Provides the replacement RT so client doesn't get locked out!
                        TokenType: "Bearer",
                        ExpiresInSeconds: AccessTokenExpirationMinutes * 60,
                        User: new UserProfileDto(token.UserId, u?.UserName ?? "", u?.Email, u?.BranchId, new List<string>(), new List<string>())
                    );
                }
            }

            // OUTSIDE GRACE WINDOW -> REUSE DETECTED!
            if (token.Session != null)
            {
                token.Session.RevokedAtUtc = DateTime.UtcNow;
                token.Session.RevokeReason = "REUSE_DETECTED";

                await _db.RefreshTokens
                    .Where(t => t.SessionId == token.SessionId && t.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow)
                        .SetProperty(x => x.ReuseDetectedAtUtc, DateTime.UtcNow), ct);

                await _db.SaveChangesAsync(ct);
            }

            await _audit.LogAsync("REUSE_DETECTED", nameof(RefreshToken), token.RefreshTokenId.ToString(), null, new { token.SessionId, ipAddress }, ct);
            throw new UnauthorizedException("Cảnh báo: Phát hiện Refresh Token bị sử dụng lại (Reuse Detected). Toàn bộ phiên đăng nhập đã bị hủy vì lý do bảo mật.");
        }

        if (token.RevokedAtUtc.HasValue)
        {
            throw new UnauthorizedException("Phiên đăng nhập đã bị thu hồi.");
        }

        throw new UnauthorizedException("Refresh token đã hết hạn. Vui lòng đăng nhập lại.");
    }

    public async Task LogoutAsync(string? rawRefreshToken, Guid? sessionId, CancellationToken ct = default)
    {
        if (sessionId.HasValue)
        {
            await RevokeSessionInternalAsync(sessionId.Value, "LOGOUT", ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (token != null)
            {
                await RevokeSessionInternalAsync(token.SessionId, "LOGOUT", ct);
            }
        }
    }

    public async Task LogoutAllAsync(long userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException(nameof(User), userId);

        // Invalidate all tokens by rotating security stamp
        user.SecurityStamp = Guid.NewGuid().ToString();

        // Revoke all sessions
        if (_db.Database.IsRelational())
        {
            await _db.Sessions
                .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow)
                    .SetProperty(x => x.RevokeReason, "LOGOUT_ALL"), ct);

            // Revoke all refresh tokens
            await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
        }
        else
        {
            var sessions = await _db.Sessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var s in sessions)
            {
                s.RevokedAtUtc = DateTime.UtcNow;
                s.RevokeReason = "LOGOUT_ALL";
            }
            var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var t in tokens)
            {
                t.RevokedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LOGOUT_ALL", nameof(User), userId.ToString(), null, null, ct);
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
        {
            throw new Common.Exceptions.ValidationException("Mật khẩu mới phải có tối thiểu 8 ký tự.");
        }

        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!VerifyPasswordHash(req.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = HashPassword(req.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Revoke all sessions (force all to re-authenticate)
        await _db.Sessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.RevokeReason, "PASSWORD_CHANGE"), ct);

        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CHANGE_PASSWORD", nameof(User), userId.ToString(), null, null, ct);
    }

    public async Task<List<UserSessionDto>> GetUserSessionsAsync(long userId, Guid? currentSessionId, CancellationToken ct = default)
    {
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.AbsoluteExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(s => s.LastUsedAtUtc)
            .Select(s => new UserSessionDto(
                s.SessionId,
                s.DeviceLabel,
                s.CreatedByIp,
                s.CreatedAtUtc,
                s.LastUsedAtUtc,
                currentSessionId.HasValue && s.SessionId == currentSessionId.Value
            ))
            .ToListAsync(ct);

        return sessions;
    }

    public async Task RevokeSessionAsync(long userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(Session), sessionId);

        await RevokeSessionInternalAsync(sessionId, "USER_REVOKED", ct);
    }

    public async Task AdminRevokeUserSessionsAsync(long targetUserId, CancellationToken ct = default)
    {
        await LogoutAllAsync(targetUserId, ct);
    }

    public async Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Chưa đăng nhập.");
        }

        var user = await _db.Users.AsNoTracking()
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId.Value);

        var roles = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role.RoleCode)
            .Distinct()
            .ToList();

        var permissions = user.UserRoleUsers
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => p.PermissionCode)
            .Distinct()
            .ToList();

        return new UserProfileDto(user.UserId, user.UserName, user.Email, user.BranchId, roles, permissions);
    }

    private async Task RevokeSessionInternalAsync(Guid sessionId, string reason, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.Sessions
                .Where(s => s.SessionId == sessionId && s.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow)
                    .SetProperty(x => x.RevokeReason, reason), ct);

            await _db.RefreshTokens
                .Where(t => t.SessionId == sessionId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
        }
        else
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId && s.RevokedAtUtc == null, ct);
            if (session != null)
            {
                session.RevokedAtUtc = DateTime.UtcNow;
                session.RevokeReason = reason;
            }
            var tokens = await _db.RefreshTokens.Where(t => t.SessionId == sessionId && t.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var t in tokens)
            {
                t.RevokedAtUtc = DateTime.UtcNow;
            }
        }
    }

    private string GenerateJwtToken(User user, Guid sessionId, List<string> roles, List<string> permissions)
    {
        var secret = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured!");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId.ToString()),
            new("stamp", user.SecurityStamp ?? "")
        };

        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));
        }

        foreach (var r in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        foreach (var p in permissions)
        {
            claims.Add(new Claim("permission", p));
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "TechCareAPI",
            audience: _config["Jwt:Audience"] ?? "TechCareWeb",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string RawToken, byte[] Hash) GenerateOpaqueRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return (raw, hash);
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPasswordHash(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);

            // Verify with 100,000 iterations
            var testHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
            if (CryptographicOperations.FixedTimeEquals(hash, testHash)) return true;

            // Also check 10,000 for legacy transition
            var testHash10k = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, testHash10k);
        }
        catch
        {
            return false;
        }
    }

    private static string ParseDeviceLabel(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Thiết bị không xác định";

        var browser = "Browser";
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Edge";
        else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) browser = "Chrome";
        else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";
        else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) browser = "Safari";

        var os = "Thiết bị";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) os = "Windows";
        else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) os = "macOS";
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) os = "iPhone";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";

        return $"{browser} · {os}";
    }

    public async Task<RegisterCustomerResponse> RegisterCustomerAsync(RegisterCustomerRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            throw new ValidationException("FullName", "Vui lòng nhập họ và tên.");

        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@') || !req.Email.Contains('.'))
            throw new ValidationException("Email", "Địa chỉ email không hợp lệ.");

        var cleanPhone = req.PhoneNumber?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(cleanPhone, @"^0(3|5|7|8|9)\d{8}$"))
            throw new ValidationException("PhoneNumber", "Số điện thoại không đúng định dạng di động Việt Nam (10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09).");

        var normalizedEmail = req.Email.Trim().ToUpperInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct))
            throw new ValidationException("Email", "Email này đã được sử dụng. Vui lòng chọn email khác hoặc đăng nhập.");

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8 || !req.Password.Any(char.IsLetter) || !req.Password.Any(char.IsDigit))
            throw new ValidationException("Password", "Mật khẩu phải có tối thiểu 8 ký tự, bao gồm cả chữ và số.");

        if (req.Password != req.ConfirmPassword)
            throw new ValidationException("ConfirmPassword", "Mật khẩu xác nhận không khớp.");

        var user = new User
        {
            UserName = req.Email.Trim().ToLowerInvariant(),
            NormalizedUserName = normalizedEmail,
            Email = req.Email.Trim().ToLowerInvariant(),
            NormalizedEmail = normalizedEmail,
            PhoneNumber = cleanPhone,
            PasswordHash = HashPassword(req.Password),
            SecurityStamp = Guid.NewGuid().ToString(),
            IsEmailConfirmed = false,
            IsPhoneConfirmed = false,
            IsActive = true,
            LockoutEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var customerRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleCode == "CUSTOMER", ct);
        if (customerRole != null)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = customerRole.RoleId,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == cleanPhone, ct)
            ?? await _db.Customers.FirstOrDefaultAsync(c => c.Email == user.Email, ct);

        if (customer != null)
        {
            customer.UserId = user.UserId;
            if (string.IsNullOrWhiteSpace(customer.Email)) customer.Email = user.Email;
        }
        else
        {
            customer = new Customer
            {
                CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 9999)}",
                FullName = req.FullName.Trim(),
                Phone = cleanPhone,
                Email = user.Email,
                UserId = user.UserId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Customers.Add(customer);
        }
        await _db.SaveChangesAsync(ct);

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(code));

        var emailToken = new EmailToken
        {
            UserId = user.UserId,
            Email = user.Email,
            TokenHash = tokenHash,
            Purpose = "EMAIL_CONFIRMATION",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            FailedAttempts = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.EmailTokens.Add(emailToken);
        await _db.SaveChangesAsync(ct);

        var body = $"<h3>Chào mừng bạn đến với TechCare!</h3><p>Mã xác thực tài khoản của bạn là: <strong style='font-size: 20px; color: #111;'>{code}</strong></p><p>Mã có hiệu lực trong vòng 15 phút. Tuyệt đối không chia sẻ mã cho bất kỳ ai.</p>";
        await _emailSender.SendEmailAsync(user.Email, "Xác thực tài khoản TechCare", body, ct);

        return new RegisterCustomerResponse(user.UserId, user.Email, "Đăng ký tài khoản thành công! Vui lòng kiểm tra email để lấy mã xác thực 6 số.");
    }

    public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var normalizedEmail = req.Email.Trim().ToUpperInvariant();
        var token = await _db.EmailTokens
            .Where(t => t.Email.ToUpper() == normalizedEmail && t.Purpose == "EMAIL_CONFIRMATION" && t.ConsumedAtUtc == null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (token == null)
        {
            throw new ValidationException("Code", "Không tìm thấy mã xác thực hợp lệ. Vui lòng yêu cầu gửi lại mã.");
        }

        if (token.FailedAttempts >= 5)
        {
            throw new ValidationException("Code", "Mã xác thực đã bị khóa do nhập sai quá 5 lần. Vui lòng yêu cầu gửi lại mã mới.");
        }

        if (DateTime.UtcNow > token.ExpiresAtUtc)
        {
            throw new ValidationException("Code", "Mã xác thực đã hết hạn. Vui lòng yêu cầu gửi lại mã mới.");
        }

        var inputHash = SHA256.HashData(Encoding.UTF8.GetBytes(req.Code.Trim()));
        if (!CryptographicOperations.FixedTimeEquals(inputHash, token.TokenHash))
        {
            token.FailedAttempts++;
            await _db.SaveChangesAsync(ct);
            var remaining = 5 - token.FailedAttempts;
            if (remaining <= 0)
            {
                throw new ValidationException("Code", "Mã xác thực không chính xác. Mã đã bị khóa do nhập sai 5 lần. Vui lòng gửi lại mã mới.");
            }
            throw new ValidationException("Code", $"Mã xác thực không chính xác. Bạn còn {remaining} lần thử.");
        }

        token.ConsumedAtUtc = DateTime.UtcNow;
        var user = await _db.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.UserId == token.UserId, ct);

        if (user == null) throw new NotFoundException("Người dùng không tồn tại.");

        user.IsEmailConfirmed = true;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await CreateSessionAndTokenForUserAsync(user, ipAddress, userAgent, ct);
    }

    public async Task ResendVerificationEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (user == null) return;

        if (user.IsEmailConfirmed)
        {
            throw new ValidationException("Email", "Tài khoản này đã được xác thực email trước đó. Vui lòng đăng nhập.");
        }

        var lastToken = await _db.EmailTokens
            .Where(t => t.UserId == user.UserId && t.Purpose == "EMAIL_CONFIRMATION")
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (lastToken != null && lastToken.CreatedAtUtc.AddSeconds(60) > DateTime.UtcNow)
        {
            var waitSec = (int)(lastToken.CreatedAtUtc.AddSeconds(60) - DateTime.UtcNow).TotalSeconds + 1;
            throw new ValidationException("Code", $"Vui lòng đợi {waitSec} giây trước khi yêu cầu gửi lại mã.");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(code));

        var emailToken = new EmailToken
        {
            UserId = user.UserId,
            Email = user.Email ?? email,
            TokenHash = tokenHash,
            Purpose = "EMAIL_CONFIRMATION",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            FailedAttempts = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.EmailTokens.Add(emailToken);
        await _db.SaveChangesAsync(ct);

        var body = $"<h3>Xác thực lại tài khoản TechCare</h3><p>Mã xác thực mới của bạn là: <strong style='font-size: 20px; color: #111;'>{code}</strong></p><p>Mã có hiệu lực trong vòng 15 phút.</p>";
        await _emailSender.SendEmailAsync(user.Email ?? email, "Mã xác thực tài khoản TechCare", body, ct);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.IsActive && u.DeletedAtUtc == null, ct);
        if (user == null) return;

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(code));

        var emailToken = new EmailToken
        {
            UserId = user.UserId,
            Email = user.Email ?? email,
            TokenHash = tokenHash,
            Purpose = "RESET_PASSWORD",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            FailedAttempts = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.EmailTokens.Add(emailToken);
        await _db.SaveChangesAsync(ct);

        var body = $"<h3>Yêu cầu đặt lại mật khẩu TechCare</h3><p>Mã xác thực đặt lại mật khẩu của bạn là: <strong style='font-size: 20px; color: #111;'>{code}</strong></p><p>Mã có hiệu lực trong vòng 30 phút. Nếu bạn không gửi yêu cầu này, vui lòng bỏ qua email.</p>";
        await _emailSender.SendEmailAsync(user.Email ?? email, "Đặt lại mật khẩu tài khoản TechCare", body, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default)
    {
        var normalizedEmail = req.Email.Trim().ToUpperInvariant();
        var token = await _db.EmailTokens
            .Where(t => t.Email.ToUpper() == normalizedEmail && t.Purpose == "RESET_PASSWORD" && t.ConsumedAtUtc == null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (token == null)
        {
            throw new ValidationException("Code", "Không tìm thấy mã đặt lại mật khẩu hợp lệ. Vui lòng gửi lại yêu cầu.");
        }

        if (token.FailedAttempts >= 5)
        {
            throw new ValidationException("Code", "Mã đặt lại mật khẩu đã bị khóa do nhập sai quá 5 lần. Vui lòng gửi lại yêu cầu mới.");
        }

        if (DateTime.UtcNow > token.ExpiresAtUtc)
        {
            throw new ValidationException("Code", "Mã đặt lại mật khẩu đã hết hạn. Vui lòng gửi lại yêu cầu mới.");
        }

        var inputHash = SHA256.HashData(Encoding.UTF8.GetBytes(req.Code.Trim()));
        if (!CryptographicOperations.FixedTimeEquals(inputHash, token.TokenHash))
        {
            token.FailedAttempts++;
            await _db.SaveChangesAsync(ct);
            var remaining = 5 - token.FailedAttempts;
            if (remaining <= 0)
            {
                throw new ValidationException("Code", "Mã đặt lại mật khẩu không chính xác và đã bị khóa. Vui lòng yêu cầu lại.");
            }
            throw new ValidationException("Code", $"Mã đặt lại mật khẩu không chính xác. Bạn còn {remaining} lần thử.");
        }

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8 || !req.NewPassword.Any(char.IsLetter) || !req.NewPassword.Any(char.IsDigit))
        {
            throw new ValidationException("NewPassword", "Mật khẩu mới phải có tối thiểu 8 ký tự, bao gồm cả chữ và số.");
        }

        if (req.NewPassword != req.ConfirmPassword)
        {
            throw new ValidationException("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
        }

        token.ConsumedAtUtc = DateTime.UtcNow;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == token.UserId, ct);
        if (user == null) throw new NotFoundException("Người dùng không tồn tại.");

        user.PasswordHash = HashPassword(req.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Revoke all existing sessions across all devices
        await LogoutAllAsync(user.UserId, ct);

        await _db.SaveChangesAsync(ct);
    }
}