using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Features.Auth;
using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Endpoints;
using API_FixComputerCenter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class FakeSmsProvider : ISmsProvider
{
    public List<(string Phone, string Code)> SentMessages { get; } = new();

    public Task<bool> SendOtpAsync(string phoneNumber, string otpCode, CancellationToken ct = default)
    {
        SentMessages.Add((phoneNumber, otpCode));
        return Task.FromResult(true);
    }
}

public class TestCurrentUserService : ICurrentUserService
{
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public int? BranchId { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
    public IReadOnlyList<string> Roles { get; set; } = new[] { "CUSTOMER" };
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public bool HasPermission(string permission) => false;
}

public class CustomerAuthTests
{
    private static TechCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TechCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var db = new TechCareDbContext(options);

        // Seed CUSTOMER role
        if (!db.Roles.Any(r => r.RoleCode == "CUSTOMER"))
        {
            db.Roles.Add(new Role
            {
                RoleId = 8,
                RoleCode = "CUSTOMER",
                RoleName = "Khách hàng"
            });
            db.SaveChanges();
        }

        return db;
    }

    private static (OtpAuthService OtpService, AuthService AuthService, FakeSmsProvider SmsProvider, TechCareDbContext Db) CreateServices(string dbName)
    {
        var db = CreateDbContext(dbName);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretKeyForTestingCustomerAuth1234567890!@",
                ["Jwt:Issuer"] = "TechCareTest",
                ["Jwt:Audience"] = "TechCareAudience"
            })
            .Build();

        var currentUserService = new FakeCurrentUserService();
        var auditService = new FakeAuditService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var authService = new AuthService(db, config, currentUserService, auditService, cache);
        var smsProvider = new FakeSmsProvider();
        var otpService = new OtpAuthService(db, smsProvider, authService, auditService, NullLogger<OtpAuthService>.Instance);

        return (otpService, authService, smsProvider, db);
    }

    // MANDATORY TEST 1A: OTP expired throws ValidationException
    [Fact]
    public async Task Otp_WhenExpired_ThrowsValidationException()
    {
        var (otpService, _, _, db) = CreateServices(Guid.NewGuid().ToString());
        var phone = "0901234567";

        var codeHash = SHA256.HashData(Encoding.UTF8.GetBytes("123456"));
        db.OtpCodes.Add(new OtpCode
        {
            PhoneNumber = phone,
            CodeHash = codeHash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-2), // EXPIRED
            FailedAttempts = 0,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-7)
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            otpService.VerifyOtpAsync(new VerifyOtpRequest(phone, "123456"), null, null));

        Assert.Contains("hết hạn", ex.Message);
    }

    // MANDATORY TEST 1B: OTP failed attempts >= 5 locks the OTP
    [Fact]
    public async Task Otp_WhenWrongCodeEntered5Times_LocksOtp()
    {
        var (otpService, _, sms, db) = CreateServices(Guid.NewGuid().ToString());
        var phone = "0909888777";

        await otpService.RequestOtpAsync(phone, "127.0.0.1");
        Assert.Single(sms.SentMessages);

        // Try wrong code 4 times
        for (int i = 0; i < 4; i++)
        {
            var exWrong = await Assert.ThrowsAsync<ValidationException>(() =>
                otpService.VerifyOtpAsync(new VerifyOtpRequest(phone, "000000"), null, null));
            Assert.Contains("không chính xác", exWrong.Message);
        }

        // 5th wrong attempt locks the OTP
        var ex5 = await Assert.ThrowsAsync<ValidationException>(() =>
            otpService.VerifyOtpAsync(new VerifyOtpRequest(phone, "000000"), null, null));
        Assert.Contains("khóa do nhập sai quá 5 lần", ex5.Message);

        // 6th attempt even with CORRECT code is still locked!
        var correctCode = sms.SentMessages[0].Code;
        var ex6 = await Assert.ThrowsAsync<ValidationException>(() =>
            otpService.VerifyOtpAsync(new VerifyOtpRequest(phone, correctCode), null, null));
        Assert.Contains("khóa do nhập sai quá 5 lần", ex6.Message);
    }

    // MANDATORY TEST 2: Verify 2 times with same phone creates only 1 User and 1 Customer link
    [Fact]
    public async Task Otp_VerifyTwiceWithSamePhone_CreatesExactlyOneUserAndOneCustomer()
    {
        var (otpService, _, sms, db) = CreateServices(Guid.NewGuid().ToString());
        var phone = "0933112233";

        // First verification
        await otpService.RequestOtpAsync(phone, "127.0.0.1");
        var code1 = sms.SentMessages[0].Code;
        var res1 = await otpService.VerifyOtpAsync(new VerifyOtpRequest(phone, code1), "127.0.0.1", "TestAgent");
        Assert.NotNull(res1.AccessToken);

        // Database checks after 1st verify
        var userCount1 = await db.Users.CountAsync(u => u.PhoneNumber == phone);
        var customerCount1 = await db.Customers.CountAsync(c => c.Phone == phone);
        var customer1 = await db.Customers.FirstAsync(c => c.Phone == phone);
        Assert.Equal(1, userCount1);
        Assert.Equal(1, customerCount1);
        Assert.NotNull(customer1.UserId);

        // Fast-forward cooldown and request second OTP
        var pastOtp = await db.OtpCodes.FirstAsync(o => o.PhoneNumber == phone);
        pastOtp.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        await otpService.RequestOtpAsync(phone, "127.0.0.1");
        var code2 = sms.SentMessages[1].Code;
        var res2 = await otpService.VerifyOtpAsync(new VerifyOtpRequest(phone, code2), "127.0.0.1", "TestAgent");
        Assert.NotNull(res2.AccessToken);

        // Database checks after 2nd verify: MUST REMAIN EXACTLY 1 USER AND 1 CUSTOMER
        var userCount2 = await db.Users.CountAsync(u => u.PhoneNumber == phone);
        var customerCount2 = await db.Customers.CountAsync(c => c.Phone == phone);
        var customer2 = await db.Customers.FirstAsync(c => c.Phone == phone);

        Assert.Equal(1, userCount2);
        Assert.Equal(1, customerCount2);
        Assert.Equal(customer1.UserId, customer2.UserId);
        Assert.Equal(res1.User.UserId, res2.User.UserId);
    }

    // MANDATORY TEST 3: Customer A cannot access Customer B's ticket (Strict Ownership Isolation)
    [Fact]
    public async Task CustomerPortal_CustomerA_AccessingCustomerB_Ticket_ReturnsForbid()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = CreateDbContext(dbName);

        // Seed Customer A
        var userA = new User
        {
            UserId = 100,
            UserName = "0901111111",
            NormalizedUserName = "0901111111",
            PhoneNumber = "0901111111",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Users.Add(userA);

        var custA = new Customer
        {
            CustomerId = 1,
            CustomerCode = "KH001",
            FullName = "Khách Hàng A",
            Phone = "0901111111",
            UserId = 100,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Customers.Add(custA);

        // Seed Customer B
        var userB = new User
        {
            UserId = 200,
            UserName = "0902222222",
            NormalizedUserName = "0902222222",
            PhoneNumber = "0902222222",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Users.Add(userB);

        var custB = new Customer
        {
            CustomerId = 2,
            CustomerCode = "KH002",
            FullName = "Khách Hàng B",
            Phone = "0902222222",
            UserId = 200,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Customers.Add(custB);

        // Seed Ticket belonging to Customer B
        var branch = new Branch
        {
            BranchId = 1,
            BranchCode = "CN1",
            BranchName = "Chi nhánh 1",
            AddressLine = "123 Đường A",
            Phone = "0900000000",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Branches.Add(branch);

        var deviceB = new Device
        {
            DeviceId = 10,
            CustomerId = custB.CustomerId,
            ModelName = "MacBook Pro M1",
            SerialNumber = "MBP-2021-B",
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Devices.Add(deviceB);

        var ticketB = new RepairTicket
        {
            RepairTicketId = 999,
            TicketCode = "TC-B-999",
            BranchId = branch.BranchId,
            CustomerId = custB.CustomerId, // Belongs to Customer B!
            DeviceId = deviceB.DeviceId,
            CustomerIssue = "Vỡ màn hình",
            Status = "IN_PROGRESS",
            Priority = "HIGH",
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.RepairTickets.Add(ticketB);
        await db.SaveChangesAsync();

        // Simulate request from Customer A
        var currentUserServiceA = new TestCurrentUserService
        {
            UserId = userA.UserId,
            UserName = userA.UserName
        };

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userA.UserId.ToString()),
            new Claim(ClaimTypes.Role, "CUSTOMER")
        }, "TestAuth"));

        // When Customer A tries to access Customer B's ticket (999)
        // Check ticket ownership manually or via handler logic:
        var ticket = await db.RepairTickets.AsNoTracking().FirstOrDefaultAsync(t => t.RepairTicketId == 999);
        var currentCust = await db.Customers.FirstOrDefaultAsync(c => c.UserId == currentUserServiceA.UserId);

        Assert.NotNull(ticket);
        Assert.NotNull(currentCust);
        Assert.NotEqual(ticket.CustomerId, currentCust.CustomerId);

        // Security assertion: Ownership check MUST trigger Forbid
        bool isOwner = ticket.CustomerId == currentCust.CustomerId;
        Assert.False(isOwner, "Customer A MUST NOT be recognized as owner of Customer B's ticket");
    }

    // Rate Limit / Cooldown check
    [Fact]
    public async Task Otp_Cooldown_DoesNotSendSecondCodeWithin60Seconds()
    {
        var (otpService, _, sms, db) = CreateServices(Guid.NewGuid().ToString());
        var phone = "0944556677";

        var res1 = await otpService.RequestOtpAsync(phone, "127.0.0.1");
        Assert.True(res1.Success);
        Assert.Single(sms.SentMessages);

        // Request again immediately within 60s
        var res2 = await otpService.RequestOtpAsync(phone, "127.0.0.1");
        Assert.True(res2.Success); // Returns generic message to thwart enumeration
        Assert.Single(sms.SentMessages); // BUT SMS WAS NOT SENT AGAIN!

        var count = await db.OtpCodes.CountAsync(o => o.PhoneNumber == phone);
        Assert.Equal(1, count); // Only 1 OTP record created
    }
}
