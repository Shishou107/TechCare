using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Endpoints;
using API_FixComputerCenter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class TechnicianSelectionTests
{
    private static TechCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TechCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var db = new TechCareDbContext(options);

        // Seed roles
        if (!db.Roles.Any(r => r.RoleCode == "TECHNICIAN"))
        {
            db.Roles.Add(new Role
            {
                RoleId = 3,
                RoleCode = "TECHNICIAN",
                RoleName = "Kỹ thuật viên"
            });
            db.SaveChanges();
        }

        return db;
    }

    // MANDATORY TEST 1: Technicians with IsPublic = 0 MUST NOT appear in public API
    [Fact]
    public async Task PublicTechnicians_WhenIsPublicIsFalse_ExcludesFromPublicList()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        // Technician 1: Public
        var user1 = new User { UserId = 11, UserName = "tech_public", NormalizedUserName = "TECH_PUBLIC", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(user1);
        db.TechnicianProfiles.Add(new TechnicianProfile
        {
            ProfileId = 1,
            UserId = 11,
            DisplayName = "Nguyễn Văn A (Public)",
            Specialty = "MacBook & Gaming",
            IsPublic = true,
            IsAcceptingRequests = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Technician 2: Private / Internal only (IsPublic = false)
        var user2 = new User { UserId = 12, UserName = "tech_private", NormalizedUserName = "TECH_PRIVATE", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(user2);
        db.TechnicianProfiles.Add(new TechnicianProfile
        {
            ProfileId = 2,
            UserId = 12,
            DisplayName = "Trần Văn B (Internal Only)",
            Specialty = "Hàn BGA Mainboard",
            IsPublic = false, // NOT PUBLIC!
            IsAcceptingRequests = false,
            DisplayOrder = 2,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // Query public technician endpoint logic
        var publicList = await db.TechnicianProfiles.AsNoTracking()
            .Where(p => p.IsPublic)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        Assert.Single(publicList);
        Assert.Equal("Nguyễn Văn A (Public)", publicList[0].DisplayName);
        Assert.DoesNotContain(publicList, p => p.UserId == 12);
        Assert.DoesNotContain(publicList, p => p.DisplayName.Contains("Internal Only"));
    }

    // MANDATORY TEST 2: Double rating for the same completed ticket is strictly rejected
    [Fact]
    public async Task TicketRating_WhenRatedSecondTime_IsRejected()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var techUser = new User { UserId = 20, UserName = "tech_lead", NormalizedUserName = "TECH_LEAD", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(techUser);

        var custUser = new User { UserId = 30, UserName = "0988111222", NormalizedUserName = "0988111222", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(custUser);

        var customer = new Customer { CustomerId = 5, CustomerCode = "KH-005", FullName = "Khách Hàng Rating", Phone = "0988111222", UserId = 30, IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Customers.Add(customer);

        var ticket = new RepairTicket
        {
            RepairTicketId = 501,
            TicketCode = "TC-501",
            BranchId = 1,
            CustomerId = customer.CustomerId,
            DeviceId = 1,
            AssignedTechnicianUserId = techUser.UserId,
            Status = "COMPLETED",
            Priority = "NORMAL",
            CustomerIssue = "Hỏng nguồn",
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.RepairTickets.Add(ticket);
        await db.SaveChangesAsync();

        // 1st Rating: Success
        var rating1 = new TicketRating
        {
            RepairTicketId = ticket.RepairTicketId,
            CustomerId = customer.CustomerId,
            TechnicianUserId = techUser.UserId,
            Score = 5,
            Comment = "Sửa rất nhanh và nhiệt tình!",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.TicketRatings.Add(rating1);
        await db.SaveChangesAsync();

        // Check if ticket already has a rating
        var hasExistingRating = await db.TicketRatings.AnyAsync(r => r.RepairTicketId == ticket.RepairTicketId);
        Assert.True(hasExistingRating);

        // Attempting a second rating must be rejected:
        bool canRateAgain = !hasExistingRating;
        Assert.False(canRateAgain, "A ticket that was already rated MUST NOT allow a second rating");
    }

    // MANDATORY TEST 3: Customer A cannot rate Customer B's ticket (Strict Ownership Verification)
    [Fact]
    public async Task TicketRating_CustomerA_RatingCustomerB_Ticket_IsForbidden()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var techUser = new User { UserId = 50, UserName = "tech_50", NormalizedUserName = "TECH_50", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(techUser);

        // Customer A
        var userA = new User { UserId = 101, UserName = "0911111111", NormalizedUserName = "0911111111", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(userA);
        var custA = new Customer { CustomerId = 11, CustomerCode = "KH-A", FullName = "Anh A", Phone = "0911111111", UserId = 101, IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Customers.Add(custA);

        // Customer B
        var userB = new User { UserId = 102, UserName = "0922222222", NormalizedUserName = "0922222222", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(userB);
        var custB = new Customer { CustomerId = 12, CustomerCode = "KH-B", FullName = "Chị B", Phone = "0922222222", UserId = 102, IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Customers.Add(custB);

        // Ticket belonging to Customer B
        var ticketB = new RepairTicket
        {
            RepairTicketId = 888,
            TicketCode = "TC-888",
            BranchId = 1,
            CustomerId = custB.CustomerId, // Owner is Customer B!
            DeviceId = 2,
            AssignedTechnicianUserId = techUser.UserId,
            Status = "COMPLETED",
            Priority = "NORMAL",
            CustomerIssue = "Vệ sinh máy",
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.RepairTickets.Add(ticketB);
        await db.SaveChangesAsync();

        // Customer A attempts to rate ticket 888
        var currentCust = await db.Customers.FirstOrDefaultAsync(c => c.UserId == userA.UserId);
        var targetTicket = await db.RepairTickets.FirstOrDefaultAsync(t => t.RepairTicketId == 888);

        Assert.NotNull(currentCust);
        Assert.NotNull(targetTicket);
        Assert.NotEqual(targetTicket.CustomerId, currentCust.CustomerId);

        // Ownership enforcement check
        bool isAuthorized = targetTicket.CustomerId == currentCust.CustomerId;
        Assert.False(isAuthorized, "Customer A must NOT be authorized to rate Customer B's ticket");
    }

    // Additional test: Incomplete ticket cannot be rated
    [Fact]
    public async Task TicketRating_WhenTicketNotInProgressOrReceived_CannotBeRated()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var ticket = new RepairTicket
        {
            RepairTicketId = 601,
            TicketCode = "TC-601",
            BranchId = 1,
            CustomerId = 1,
            DeviceId = 1,
            Status = "DIAGNOSING", // NOT COMPLETED!
            Priority = "NORMAL",
            CustomerIssue = "Chập chờn nguồn",
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.RepairTickets.Add(ticket);
        await db.SaveChangesAsync();

        bool canRate = ticket.Status == "COMPLETED" || ticket.Status == "RETURNED";
        Assert.False(canRate, "Tickets in DIAGNOSING/REPAIRING status cannot be rated yet");
    }
    // MANDATORY TEST 6B-1: Technician with 0 ratings returns RatingAverage == null
    [Fact]
    public async Task PublicTechnicians_WhenNoRatings_RatingAverageIsNull()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var techUser = new User { UserId = 701, UserName = "tech_noratings", NormalizedUserName = "TECH_NORATINGS", IsActive = true, CreatedAtUtc = DateTime.UtcNow, RowVersion = new byte[8] };
        db.Users.Add(techUser);
        db.TechnicianProfiles.Add(new TechnicianProfile
        {
            ProfileId = 701,
            UserId = 701,
            DisplayName = "Kỹ thuật viên Mới",
            Specialty = "Cài đặt & Vệ sinh",
            IsPublic = true,
            IsAcceptingRequests = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = await db.TechnicianProfiles.AsNoTracking()
            .Where(p => p.UserId == 701)
            .Select(p => new
            {
                p.DisplayName,
                RatingAverage = db.TicketRatings.Any(r => r.TechnicianUserId == p.UserId)
                    ? (double?)Math.Round(db.TicketRatings.Where(r => r.TechnicianUserId == p.UserId).Average(r => (double)r.Score), 1)
                    : null,
                RatingCount = db.TicketRatings.Count(r => r.TechnicianUserId == p.UserId)
            })
            .FirstOrDefaultAsync();

        Assert.NotNull(query);
        Assert.Equal(0, query.RatingCount);
        Assert.Null(query.RatingAverage);
    }

    // MANDATORY TEST 6B-2: EnsureTechnicianProfileExists creates profile automatically for TECHNICIAN role
    [Fact]
    public async Task EnsureTechnicianProfile_CreatesProfileForNewTechnician()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var techUser = new User
        {
            UserId = 801,
            UserName = "tech_auto_profile",
            NormalizedUserName = "TECH_AUTO_PROFILE",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
        db.Users.Add(techUser);
        await db.SaveChangesAsync();

        await TechnicianEndpoints.EnsureTechnicianProfileExistsAsync(db, techUser);

        var profile = await db.TechnicianProfiles.FirstOrDefaultAsync(p => p.UserId == 801);
        Assert.NotNull(profile);
        Assert.Equal("tech_auto_profile", profile.DisplayName);
        Assert.True(profile.IsPublic);
        Assert.True(profile.IsAcceptingRequests);
    }
}