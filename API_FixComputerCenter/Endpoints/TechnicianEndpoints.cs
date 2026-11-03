using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Endpoints;

public record UpdateTechnicianProfileRequest(
    string? DisplayName,
    string? Specialty,
    string? Bio,
    string? AvatarUrl,
    int? ExperienceYears,
    bool? IsPublic,
    bool? IsAcceptingRequests,
    int? DisplayOrder
);

public static class TechnicianEndpoints
{
    public static async Task EnsureTechnicianProfileExistsAsync(ITechCareDbContext db, User user, CancellationToken ct = default)
    {
        var existing = await db.TechnicianProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId, ct);
        if (existing == null)
        {
            db.TechnicianProfiles.Add(new TechnicianProfile
            {
                UserId = user.UserId,
                DisplayName = !string.IsNullOrWhiteSpace(user.UserName) ? user.UserName : "Kỹ thuật viên TechCare",
                Specialty = "Kỹ thuật viên",
                Bio = "Chuyên viên kỹ thuật tiếp nhận và xử lý máy tính tại xưởng TechCare.",
                ExperienceYears = 1,
                IsPublic = true,
                IsAcceptingRequests = true,
                DisplayOrder = 0,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }

    public static void MapTechnicianEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/technicians").WithTags("Technicians");

        // 1. Public list of active technicians (only safe fields, IsPublic = true)
        group.MapGet("/public", async (ITechCareDbContext db) =>
        {
            var technicians = await db.TechnicianProfiles.AsNoTracking()
                .Where(p => p.IsPublic)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.ProfileId)
                .Select(p => new
                {
                    p.ProfileId,
                    p.UserId,
                    p.DisplayName,
                    p.AvatarUrl,
                    p.Specialty,
                    p.Bio,
                    p.ExperienceYears,
                    p.IsAcceptingRequests,
                    p.DisplayOrder,
                    RatingAverage = db.TicketRatings.Any(r => r.TechnicianUserId == p.UserId)
                        ? (double?)Math.Round(db.TicketRatings.Where(r => r.TechnicianUserId == p.UserId).Average(r => (double)r.Score), 1)
                        : null,
                    RatingCount = db.TicketRatings.Count(r => r.TechnicianUserId == p.UserId),
                    CompletedTicketsCount = db.RepairTickets.Count(t => t.AssignedTechnicianUserId == p.UserId && (t.Status == "COMPLETED" || t.Status == "RETURNED"))
                })
                .ToListAsync();

            return Results.Ok(technicians);
        }).AllowAnonymous();

        // 2. Admin / Manager list of all technicians
        group.MapGet("/", async (ITechCareDbContext db, ICurrentUserService current) =>
        {
            if (!current.UserId.HasValue) return Results.Unauthorized();
            var isAdmin = current.Roles.Contains("ADMIN") || current.Roles.Contains("MANAGER");
            if (!isAdmin) return Results.Forbid();

            var list = await db.TechnicianProfiles.AsNoTracking()
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.ProfileId)
                .Select(p => new
                {
                    p.ProfileId,
                    p.UserId,
                    p.DisplayName,
                    p.AvatarUrl,
                    p.Specialty,
                    p.Bio,
                    p.ExperienceYears,
                    p.IsPublic,
                    p.IsAcceptingRequests,
                    p.DisplayOrder,
                    RatingAverage = db.TicketRatings.Any(r => r.TechnicianUserId == p.UserId)
                        ? (double?)Math.Round(db.TicketRatings.Where(r => r.TechnicianUserId == p.UserId).Average(r => (double)r.Score), 1)
                        : null,
                    RatingCount = db.TicketRatings.Count(r => r.TechnicianUserId == p.UserId),
                    CompletedTicketsCount = db.RepairTickets.Count(t => t.AssignedTechnicianUserId == p.UserId && (t.Status == "COMPLETED" || t.Status == "RETURNED"))
                })
                .ToListAsync();

            return Results.Ok(list);
        }).RequireAuthorization();

        // 3. Update profile (self for info; admin for public / accepting status)
        group.MapPut("/{userId:long}/profile", async (long userId, UpdateTechnicianProfileRequest req, ITechCareDbContext db, ICurrentUserService current) =>
        {
            if (!current.UserId.HasValue) return Results.Unauthorized();
            var isAdmin = current.Roles.Contains("ADMIN") || current.Roles.Contains("MANAGER");
            var isSelf = current.UserId.Value == userId;

            if (!isAdmin && !isSelf) return Results.Forbid();

            var profile = await db.TechnicianProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null) return Results.NotFound();
                profile = new TechnicianProfile
                {
                    UserId = userId,
                    DisplayName = req.DisplayName ?? user.UserName,
                    Specialty = req.Specialty ?? "Kỹ thuật viên",
                    IsPublic = true,
                    IsAcceptingRequests = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.TechnicianProfiles.Add(profile);
            }

            if (!string.IsNullOrWhiteSpace(req.DisplayName)) profile.DisplayName = req.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(req.Specialty)) profile.Specialty = req.Specialty.Trim();
            if (req.Bio != null) profile.Bio = req.Bio.Trim();
            if (req.AvatarUrl != null) profile.AvatarUrl = req.AvatarUrl.Trim();
            if (req.ExperienceYears.HasValue) profile.ExperienceYears = req.ExperienceYears.Value;

            if (isAdmin)
            {
                if (req.IsPublic.HasValue) profile.IsPublic = req.IsPublic.Value;
                if (req.IsAcceptingRequests.HasValue) profile.IsAcceptingRequests = req.IsAcceptingRequests.Value;
                if (req.DisplayOrder.HasValue) profile.DisplayOrder = req.DisplayOrder.Value;
            }

            profile.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { success = true, profile });
        }).RequireAuthorization();
    }
}
