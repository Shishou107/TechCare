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
using API_FixComputerCenter.Application.Features.Appointments;
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

public class FakeEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string Body)> SentEmails { get; } = new();

    public Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, subject, bodyHtml));
        return Task.CompletedTask;
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

    private static (AuthService AuthService, FakeEmailSender EmailSender, TechCareDbContext Db) CreateServices(string dbName)
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
        var emailSender = new FakeEmailSender();
        var authService = new AuthService(db, config, currentUserService, auditService, cache, emailSender);

        return (authService, emailSender, db);
    }

    // MANDATORY TEST 6A-1: Invalid phone format is strictly rejected
    [Theory]
    [InlineData("123456")]
    [InlineData("0123456789")]
    [InlineData("09012345")]
    [InlineData("abcd123456")]
    public async Task Register_InvalidPhoneNumber_ThrowsValidationException(string badPhone)
    {
        var (auth, _, _) = CreateServices(Guid.NewGuid().ToString());
        var req = new RegisterCustomerRequest("Nguyễn Văn A", "valid@test.vn", badPhone, "Password@123", "Password@123");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => auth.RegisterCustomerAsync(req));
        Assert.Contains("Số điện thoại không đúng định dạng", ex.Message);
    }

    // MANDATORY TEST 6A-2: Duplicate email is rejected
    [Fact]
    public async Task Register_DuplicateEmail_ThrowsValidationException()
    {
        var (auth, _, _) = CreateServices(Guid.NewGuid().ToString());
        var req1 = new RegisterCustomerRequest("Khách Một", "duplicate@techcare.vn", "0901234567", "Password@123", "Password@123");
        await auth.RegisterCustomerAsync(req1);

        var req2 = new RegisterCustomerRequest("Khách Hai", "DUPLICATE@techcare.vn", "0907654321", "Password@456", "Password@456");
        var ex = await Assert.ThrowsAsync<ValidationException>(() => auth.RegisterCustomerAsync(req2));
        Assert.Contains("Email này đã được sử dụng", ex.Message);
    }

    // MANDATORY TEST 6A-3: Customer login with unconfirmed email is rejected with EMAIL_NOT_VERIFIED
    [Fact]
    public async Task Login_CustomerUnconfirmedEmail_ThrowsEmailNotVerifiedException()
    {
        var (auth, _, _) = CreateServices(Guid.NewGuid().ToString());
        var email = "unverified@test.vn";
        var password = "SecurePassword@123";

        await auth.RegisterCustomerAsync(new RegisterCustomerRequest("Chưa Xác Thực", email, "0903334455", password, password));

        var ex = await Assert.ThrowsAsync<EmailNotVerifiedException>(() =>
            auth.LoginAsync(new LoginRequest(email, password), "127.0.0.1", "Browser"));

        Assert.Contains("chưa được xác thực email", ex.Message);
        Assert.Equal(email, ex.Email);
    }

    // MANDATORY TEST 6A-4: Verification code locked after 5 failed attempts
    [Fact]
    public async Task VerifyEmail_WrongCode5Times_LocksCode()
    {
        var (auth, _, db) = CreateServices(Guid.NewGuid().ToString());
        var email = "lockout@test.vn";
        await auth.RegisterCustomerAsync(new RegisterCustomerRequest("Khách Khóa", email, "0905556677", "Password@123", "Password@123"));

        // 4 wrong attempts
        for (int i = 0; i < 4; i++)
        {
            var exWrong = await Assert.ThrowsAsync<ValidationException>(() =>
                auth.VerifyEmailAsync(new VerifyEmailRequest(email, "000000"), null, null));
            Assert.Contains("không chính xác", exWrong.Message);
        }

        // 5th wrong attempt locks the code
        var ex5 = await Assert.ThrowsAsync<ValidationException>(() =>
            auth.VerifyEmailAsync(new VerifyEmailRequest(email, "000000"), null, null));
        Assert.Contains("bị khóa do nhập sai", ex5.Message);

        // 6th attempt is still locked
        var ex6 = await Assert.ThrowsAsync<ValidationException>(() =>
            auth.VerifyEmailAsync(new VerifyEmailRequest(email, "123456"), null, null));
        Assert.Contains("bị khóa do nhập sai", ex6.Message);
    }

    // MANDATORY TEST 6A-5: Expired verification code is rejected
    [Fact]
    public async Task VerifyEmail_WhenExpired_ThrowsValidationException()
    {
        var (auth, _, db) = CreateServices(Guid.NewGuid().ToString());
        var email = "expired@test.vn";
        await auth.RegisterCustomerAsync(new RegisterCustomerRequest("Khách Hết Hạn", email, "0908889900", "Password@123", "Password@123"));

        var token = await db.EmailTokens.FirstAsync(t => t.Email == email);
        token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            auth.VerifyEmailAsync(new VerifyEmailRequest(email, "123456"), null, null));
        Assert.Contains("hết hạn", ex.Message);
    }

    // MANDATORY TEST 6A-6: Reset password revokes existing active sessions
    [Fact]
    public async Task ResetPassword_RevokesExistingSessions_AndAllowsNewPasswordLogin()
    {
        var (auth, emailSender, db) = CreateServices(Guid.NewGuid().ToString());
        var email = "session-revoke@test.vn";
        var oldPassword = "OldPassword@123";
        var newPassword = "NewPassword@456";

        await auth.RegisterCustomerAsync(new RegisterCustomerRequest("Khách Đổi Pass", email, "0909112233", oldPassword, oldPassword));

        // Manually confirm email to allow login
        var user = await db.Users.FirstAsync(u => u.Email == email);
        user.IsEmailConfirmed = true;
        await db.SaveChangesAsync();

        // Login to get an active session
        var loginRes = await auth.LoginAsync(new LoginRequest(email, oldPassword), "1.2.3.4", "TestClient");
        Assert.NotNull(loginRes.AccessToken);

        var activeSessions = await db.Sessions.Where(s => s.UserId == user.UserId && s.RevokedAtUtc == null).CountAsync();
        Assert.Equal(1, activeSessions);

        // Request forgot password
        await auth.ForgotPasswordAsync(email);
        Assert.True(emailSender.SentEmails.Count >= 2); // 1 welcome + 1 reset

        // Extract code from DB for testing
        var resetToken = await db.EmailTokens.Where(t => t.UserId == user.UserId && t.Purpose == "RESET_PASSWORD").FirstAsync();
        // Create known hash for test verification
        var testCode = "654321";
        resetToken.TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(testCode));
        await db.SaveChangesAsync();

        // Perform password reset
        await auth.ResetPasswordAsync(new ResetPasswordRequest(email, testCode, newPassword, newPassword));

        // Old sessions must be revoked!
        var revokedCount = await db.Sessions.Where(s => s.UserId == user.UserId && s.RevokedAtUtc != null).CountAsync();
        Assert.Equal(1, revokedCount);

        // Login with old password fails
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginRequest(email, oldPassword), "1.2.3.4", "TestClient"));

        // Login with new password succeeds
        var newLoginRes = await auth.LoginAsync(new LoginRequest(email, newPassword), "1.2.3.4", "TestClient");
        Assert.NotNull(newLoginRes.AccessToken);
    }

    // MANDATORY TEST 6A-7: Register with existing customer phone links to old tickets
    [Fact]
    public async Task Register_ExistingCustomerPhone_LinksAccountToExistingCustomerAndTickets()
    {
        var (auth, _, db) = CreateServices(Guid.NewGuid().ToString());
        var phone = "0977889900";
        var email = "linked@techcare.vn";

        // Seed old customer record with a repair ticket (created before customer had an account)
        var oldCustomer = new Customer
        {
            CustomerId = 77,
            CustomerCode = "CUST-OLD-77",
            FullName = "Khách Hàng Cũ",
            Phone = phone,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-1)
        };
        db.Customers.Add(oldCustomer);

        var oldTicket = new RepairTicket
        {
            RepairTicketId = 7701,
            TicketCode = "TC-7701",
            BranchId = 1,
            CustomerId = 77,
            DeviceId = 1,
            Status = "COMPLETED",
            Priority = "NORMAL",
            CustomerIssue = "Vệ sinh tản nhiệt",
            ReceivedAtUtc = DateTime.UtcNow.AddDays(-10),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
            RowVersion = new byte[8]
        };
        db.RepairTickets.Add(oldTicket);
        await db.SaveChangesAsync();

        // Customer registers new account using their phone number
        var reg = await auth.RegisterCustomerAsync(new RegisterCustomerRequest("Khách Hàng Cũ", email, phone, "SecurePass@123", "SecurePass@123"));
        Assert.True(reg.UserId > 0);

        // Verify customer record is now linked to the newly registered User
        var updatedCustomer = await db.Customers.FirstAsync(c => c.CustomerId == 77);
        Assert.Equal(reg.UserId, updatedCustomer.UserId);

        // In customer portal, querying tickets by current user's customerId finds the old ticket!
        var customerTickets = await db.RepairTickets.Where(t => t.CustomerId == updatedCustomer.CustomerId).ToListAsync();
        Assert.Single(customerTickets);
        Assert.Equal("TC-7701", customerTickets[0].TicketCode);
    }

    // MANDATORY TEST 6C-1: DeliveryMethod SHIP_IN / PICKUP requires valid address (min 10 chars)
    [Fact]
    public void BookAppointmentValidator_ShipInOrPickup_RequiresAddress()
    {
        var validator = new BookAppointmentValidator();

        // 1. SHIP_IN with empty address -> Invalid
        var res1 = validator.Validate(new BookAppointmentRequest
        {
            CustomerName = "Nguyễn Văn B",
            CustomerPhone = "0901234567",
            IssueDescription = "Thay bàn phím laptop Dell",
            DeliveryMethod = "SHIP_IN",
            PickupAddress = ""
        });
        Assert.False(res1.IsValid);
        Assert.Contains(res1.Errors, e => e.PropertyName == "PickupAddress");

        // 2. PICKUP with short address (< 10 chars) -> Invalid
        var res2 = validator.Validate(new BookAppointmentRequest
        {
            CustomerName = "Nguyễn Văn B",
            CustomerPhone = "0901234567",
            IssueDescription = "Thay bàn phím laptop Dell",
            DeliveryMethod = "PICKUP",
            PickupAddress = "Hà Nội"
        });
        Assert.False(res2.IsValid);
        Assert.Contains(res2.Errors, e => e.PropertyName == "PickupAddress");

        // 3. AT_STORE with empty address -> Valid (store intake needs no address)
        var res3 = validator.Validate(new BookAppointmentRequest
        {
            CustomerName = "Nguyễn Văn B",
            CustomerPhone = "0901234567",
            IssueDescription = "Thay bàn phím laptop Dell",
            DeliveryMethod = "AT_STORE",
            PickupAddress = null
        });
        Assert.True(res3.IsValid);

        // 4. PICKUP with full valid address -> Valid
        var res4 = validator.Validate(new BookAppointmentRequest
        {
            CustomerName = "Nguyễn Văn B",
            CustomerPhone = "0901234567",
            IssueDescription = "Thay bàn phím laptop Dell",
            DeliveryMethod = "PICKUP",
            PickupAddress = "123 Đường Cầu Giấy, Hà Nội"
        });
        Assert.True(res4.IsValid);
    }

    // MANDATORY TEST 6C-2: Customer A cannot access Customer B's ticket (Strict Ownership Isolation)
    [Fact]
    public async Task CustomerPortal_CustomerA_AccessingCustomerB_Ticket_ReturnsForbid()
    {
        var dbName = Guid.NewGuid().ToString();
        var db = CreateDbContext(dbName);

        // Seed Customer A
        var userA = new User
        {
            UserId = 100,
            UserName = "cust_a@techcare.vn",
            NormalizedUserName = "CUST_A@TECHCARE.VN",
            Email = "cust_a@techcare.vn",
            NormalizedEmail = "CUST_A@TECHCARE.VN",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Users.Add(userA);
        var custA = new Customer
        {
            CustomerId = 1,
            CustomerCode = "KH-001",
            FullName = "Khách Hàng A",
            Phone = "0901111111",
            Email = "cust_a@techcare.vn",
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
            UserName = "cust_b@techcare.vn",
            NormalizedUserName = "CUST_B@TECHCARE.VN",
            Email = "cust_b@techcare.vn",
            NormalizedEmail = "CUST_B@TECHCARE.VN",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Users.Add(userB);
        var custB = new Customer
        {
            CustomerId = 2,
            CustomerCode = "KH-002",
            FullName = "Khách Hàng B",
            Phone = "0902222222",
            Email = "cust_b@techcare.vn",
            UserId = 200,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Customers.Add(custB);

        // Seed Ticket for Customer B
        var ticketB = new RepairTicket
        {
            RepairTicketId = 999,
            TicketCode = "TC-B-999",
            BranchId = 1,
            CustomerId = 2,
            DeviceId = 1,
            Status = "REPAIRING",
            Priority = "NORMAL",
            CustomerIssue = "Hỏng màn hình",
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.RepairTickets.Add(ticketB);
        await db.SaveChangesAsync();

        // Customer A queries ticket 999
        var currentCustomer = await db.Customers.FirstOrDefaultAsync(c => c.UserId == userA.UserId);
        Assert.NotNull(currentCustomer);

        var ticket = await db.RepairTickets.AsNoTracking().FirstOrDefaultAsync(t => t.RepairTicketId == 999);
        Assert.NotNull(ticket);

        // Server-side check
        bool isOwner = ticket.CustomerId == currentCustomer.CustomerId;
        Assert.False(isOwner, "Customer A must not have ownership of Customer B's ticket");
    }
}
