using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Endpoints;

public static class TechnicianEndpoints
{
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
                    RatingAverage = Math.Round(db.TicketRatings.Where(r => r.TechnicianUserId == p.UserId).Select(r => (double?)r.Score).Average() ?? 5.0, 1),
                    RatingCount = db.TicketRatings.Count(r => r.TechnicianUserId == p.UserId),
                    CompletedTicketsCount = db.RepairTickets.Count(t => t.AssignedTechnicianUserId == p.UserId && (t.Status == "COMPLETED" || t.Status == "RETURNED"))
                })
                .ToListAsync();

            return Results.Ok(technicians);
        }).AllowAnonymous();
    }
}
