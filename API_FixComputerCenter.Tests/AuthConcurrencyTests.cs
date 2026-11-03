using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Features.Auth;
using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class FakeAuditService : IAuditService
{
    public Task LogAsync(string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeCurrentUserService : ICurrentUserService
{
    public long? UserId => 1;
    public string? UserName => "admin";
    public int? BranchId => 1;
    public bool IsAuthenticated => true;
    public IReadOnlyList<string> Roles => new[] { "ADMIN" };
    public IReadOnlyList<string> Permissions => new[] { "SYSTEM.MANAGE" };
    public bool HasPermission(string permission) => true;
}

public class AuthConcurrencyTests
{
    private static TechCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TechCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new TechCareDbContext(options);
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100000,
            HashAlgorithmName.SHA256,
            outputLength: 32
        );
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static AuthService CreateAuthService(TechCareDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "Unit_Test_Super_Secret_Key_At_Least_32_Chars_Long!",
                ["Jwt:Issuer"] = "TechCareAPI",
                ["Jwt:Audience"] = "TechCareWeb"
            })
            .Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new AuthService(db, config, new FakeCurrentUserService(), new FakeAuditService(), cache);
    }

    // TEST S3: Lockout counter increments on failed password and locks account after 5 attempts
    [Fact]
    public async Task Lockout_FiveConsecutiveFailedAttempts_LocksAccount()
    {
        using var db = CreateDbContext("Test_Lockout_" + Guid.NewGuid());
        var user = new User
        {
            UserId = 10,
            UserName = "testlockout",
            NormalizedUserName = "TESTLOCKOUT",
            Email = "lockout@techcare.vn",
            NormalizedEmail = "LOCKOUT@TECHCARE.VN",
            PasswordHash = HashPassword("CorrectPassword@123"),
            LockoutEnabled = true,
            AccessFailedCount = 0,
            IsActive = true,
            RowVersion = new byte[8]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var authSvc = CreateAuthService(db);

        // 4 failed attempts: account should not be locked yet
        for (int i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                authSvc.LoginAsync(new LoginRequest("testlockout", "WrongPassword!"), "127.0.0.1", "TestAgent"));
        }

        var userAfter4 = await db.Users.FindAsync(10L);
        Assert.NotNull(userAfter4);
        Assert.Equal(4, userAfter4.AccessFailedCount);
        Assert.Null(userAfter4.LockoutEndUtc);

        // 5th failed attempt: triggers lockout
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            authSvc.LoginAsync(new LoginRequest("testlockout", "WrongPassword!"), "127.0.0.1", "TestAgent"));

        var userAfter5 = await db.Users.FindAsync(10L);
        Assert.NotNull(userAfter5);
        Assert.Equal(5, userAfter5.AccessFailedCount);
        Assert.NotNull(userAfter5.LockoutEndUtc);
        Assert.True(userAfter5.LockoutEndUtc > DateTime.UtcNow);

        // 6th attempt (even with correct password): rejected with lockout notice
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            authSvc.LoginAsync(new LoginRequest("testlockout", "CorrectPassword@123"), "127.0.0.1", "TestAgent"));
        Assert.Contains("khóa", ex.Message);
    }

    // TEST S4: Maximum 5 active sessions; 6th login revokes oldest active session
    [Fact]
    public async Task SessionLimit_SixLogins_RevokesOldestActiveSession()
    {
        using var db = CreateDbContext("Test_SessionLimit_" + Guid.NewGuid());
        var user = new User
        {
            UserId = 20,
            UserName = "multisession",
            NormalizedUserName = "MULTISESSION",
            Email = "session@techcare.vn",
            NormalizedEmail = "SESSION@TECHCARE.VN",
            PasswordHash = HashPassword("Password@123"),
            LockoutEnabled = true,
            AccessFailedCount = 0,
            IsActive = true,
            RowVersion = new byte[8]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var authSvc = CreateAuthService(db);

        // Perform 5 logins
        for (int i = 1; i <= 5; i++)
        {
            var res = await authSvc.LoginAsync(new LoginRequest("multisession", "Password@123"), $"192.168.1.{i}", $"Browser-{i}");
            Assert.NotNull(res.AccessToken);
        }

        var activeSessions5 = await db.Sessions
            .Where(s => s.UserId == 20 && s.RevokedAtUtc == null)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal(5, activeSessions5.Count);

        var firstSessionId = activeSessions5[0].SessionId;

        // 6th login: must enforce MaxActiveSessions = 5 by revoking oldest
        var res6 = await authSvc.LoginAsync(new LoginRequest("multisession", "Password@123"), "192.168.1.6", "Browser-6");
        Assert.NotNull(res6.AccessToken);

        var activeSessionsAfter6 = await db.Sessions
            .Where(s => s.UserId == 20 && s.RevokedAtUtc == null)
            .ToListAsync();
        Assert.Equal(5, activeSessionsAfter6.Count);

        // Verify the very first session was revoked with "SESSION_LIMIT"
        var firstSession = await db.Sessions.FindAsync(firstSessionId);
        Assert.NotNull(firstSession);
        Assert.NotNull(firstSession.RevokedAtUtc);
        Assert.Equal("SESSION_LIMIT", firstSession.RevokeReason);
    }
}
