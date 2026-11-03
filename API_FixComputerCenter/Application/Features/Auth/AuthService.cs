using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace API_FixComputerCenter.Application.Features.Auth;

public record LoginRequest(string UserName, string Password);
public record RefreshTokenRequest(string? RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UserSessionDto(Guid SessionId, string? DeviceLabel, string? CreatedByIp, DateTime CreatedAtUtc, DateTime LastUsedAtUtc, bool IsCurrent);
public record AuthResponse(string AccessToken, string? RefreshToken, string TokenType, int ExpiresInSeconds, UserProfileDto User);
public record UserProfileDto(long UserId, string UserName, string? Email, int? BranchId, List<string> Roles, List<string> Permissions);

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(string rawRefreshToken, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task LogoutAsync(string? rawRefreshToken, Guid? sessionId, CancellationToken ct = default);
    Task LogoutAllAsync(long userId, CancellationToken ct = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest req, CancellationToken ct = default);
    Task<List<UserSessionDto>> GetUserSessionsAsync(long userId, Guid? currentSessionId, CancellationToken ct = default);
    Task RevokeSessionAsync(long userId, Guid sessionId, CancellationToken ct = default);
    Task AdminRevokeUserSessionsAsync(long targetUserId, CancellationToken ct = default);
    Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken ct = default);
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

    public AuthService(ITechCareDbContext db, IConfiguration config, ICurrentUserService currentUser, IAuditService audit, IMemoryCache cache)
    {
        _db = db;
        _config = config;
        _currentUser = currentUser;
        _audit = audit;
        _cache = cache;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var normalized = req.UserName.Trim().ToUpperInvariant();
        var user = await _db.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);

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
            // A7: Atomic update for lockout counter
            var nextCount = user.AccessFailedCount + 1;
            DateTime? lockoutEnd = (nextCount >= 5 && user.LockoutEnabled) ? DateTime.UtcNow.AddMinutes(15) : user.LockoutEndUtc;

            await _db.Users.Where(u => u.UserId == user.UserId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AccessFailedCount, nextCount)
                    .SetProperty(x => x.LockoutEndUtc, lockoutEnd), ct);

            throw new UnauthorizedException("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // Reset failed count on successful password
        await _db.Users.Where(u => u.UserId == user.UserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.AccessFailedCount, 0)
                .SetProperty(x => x.LockoutEndUtc, (DateTime?)null)
                .SetProperty(x => x.LastLoginAtUtc, DateTime.UtcNow), ct);

        if (string.IsNullOrEmpty(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString();
            await _db.Users.Where(u => u.UserId == user.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SecurityStamp, user.SecurityStamp), ct);
        }

        // 3. Enforce session limit (Max 5 per user, revoke oldest if exceeded) in atomic lock
        using var tx = await _db.Database.BeginTransactionAsync(ct);

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

                await _db.RefreshTokens
                    .Where(t => t.SessionId == oldSession.SessionId && t.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
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
            DeviceLabel = deviceLabel
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
        await tx.CommitAsync(ct);

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
        await _db.Sessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.RevokeReason, "LOGOUT_ALL"), ct);

        // Revoke all refresh tokens
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);

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
        await _db.Sessions
            .Where(s => s.SessionId == sessionId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.RevokeReason, reason), ct);

        await _db.RefreshTokens
            .Where(t => t.SessionId == sessionId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
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
}
