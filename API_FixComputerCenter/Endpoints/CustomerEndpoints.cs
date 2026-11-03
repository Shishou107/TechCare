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

public record SubmitTicketRatingRequest(int Score, string? Comment);

public record UpdateCustomerProfileRequest(
    string? FullName,
    string? Email,
    string? AddressLine,
    string? Ward,
    string? District,
    string? Province,
    string? Notes
);

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customer")
            .WithTags("Customer Portal")
            .RequireAuthorization();

        // 1. Customer Profile
        group.MapGet("/profile", async (HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            return Results.Ok(new
            {
                customer!.CustomerId,
                customer.CustomerCode,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.AddressLine,
                customer.Ward,
                customer.District,
                customer.Province,
                customer.Notes,
                customer.CreatedAtUtc
            });
        });

        group.MapPut("/profile", async (UpdateCustomerProfileRequest req, HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            if (!string.IsNullOrWhiteSpace(req.FullName)) customer!.FullName = req.FullName.Trim();
            if (req.Email != null) customer!.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
            if (req.AddressLine != null) customer!.AddressLine = req.AddressLine.Trim();
            if (req.Ward != null) customer!.Ward = req.Ward.Trim();
            if (req.District != null) customer!.District = req.District.Trim();
            if (req.Province != null) customer!.Province = req.Province.Trim();
            if (req.Notes != null) customer!.Notes = req.Notes.Trim();
            customer!.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                customer.CustomerId,
                customer.CustomerCode,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.AddressLine,
                customer.Ward,
                customer.District,
                customer.Province,
                customer.Notes,
                customer.UpdatedAtUtc
            });
        });

        // 2. Customer Devices
        group.MapGet("/devices", async (HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            var devices = await db.Devices.AsNoTracking()
                .Where(d => d.CustomerId == customer!.CustomerId)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Select(d => new
                {
                    d.DeviceId,
                    d.CustomerId,
                    d.ModelName,
                    d.SerialNumber,
                    d.AssetTag,
                    d.Cpu,
                    d.RamDescription,
                    d.StorageDescription,
                    d.OperatingSystem,
                    d.Color,
                    d.Notes,
                    d.CreatedAtUtc
                })
                .ToListAsync();

            return Results.Ok(devices);
        });

        // 3. Customer Tickets (Strictly filtered by CustomerId)
        group.MapGet("/tickets", async (HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            var tickets = await db.RepairTickets.AsNoTracking()
                .Where(t => t.CustomerId == customer!.CustomerId)
                .OrderByDescending(t => t.ReceivedAtUtc)
                .Select(t => new
                {
                    t.RepairTicketId,
                    t.TicketCode,
                    t.CustomerId,
                    t.DeviceId,
                    DeviceModel = t.Device != null ? t.Device.ModelName : "Không xác định",
                    DeviceSerial = t.Device != null ? t.Device.SerialNumber : null,
                    IssueDescription = t.CustomerIssue,
                    StatusCode = t.Status,
                    t.Priority,
                    t.InspectionFee,
                    t.ReceivedAtUtc,
                    t.ExpectedCompletionAtUtc,
                    t.ActualCompletionAtUtc,
                    t.ClosedAtUtc,
                    AssignedTechnician = t.AssignedTechnicianUser != null ? new
                    {
                        t.AssignedTechnicianUser.UserId,
                        UserName = t.AssignedTechnicianUser.TechnicianProfile != null ? t.AssignedTechnicianUser.TechnicianProfile.DisplayName : t.AssignedTechnicianUser.UserName,
                        AvatarUrl = t.AssignedTechnicianUser.TechnicianProfile != null ? t.AssignedTechnicianUser.TechnicianProfile.AvatarUrl : null,
                        Specialty = t.AssignedTechnicianUser.TechnicianProfile != null ? t.AssignedTechnicianUser.TechnicianProfile.Specialty : null
                    } : null,
                    PreferredTechnician = t.PreferredTechnicianUser != null ? new
                    {
                        t.PreferredTechnicianUser.UserId,
                        t.PreferredTechnicianUser.UserName
                    } : null
                })
                .ToListAsync();

            return Results.Ok(tickets);
        });

        // 4. Ticket Detail with strict ownership validation (Customer A cannot access Customer B's ticket)
        group.MapGet("/tickets/{ticketId:long}", async (long ticketId, HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            var ticket = await db.RepairTickets.AsNoTracking()
                .Include(t => t.Device)
                .Include(t => t.AssignedTechnicianUser)
                    .ThenInclude(u => u!.TechnicianProfile)
                .Include(t => t.PreferredTechnicianUser)
                .Include(t => t.TicketRating)
                .FirstOrDefaultAsync(t => t.RepairTicketId == ticketId);

            if (ticket == null)
            {
                return Results.NotFound(new { message = "Không tìm thấy phiếu sửa chữa." });
            }

            // CRITICAL OWNERSHIP CHECK: Fail with 403 Forbidden if not the owner
            if (ticket.CustomerId != customer!.CustomerId)
            {
                return Results.Forbid();
            }

            var shipments = await db.Shipments.AsNoTracking()
                .Include(s => s.Events)
                .Where(s => s.RepairTicketId == ticketId)
                .ToListAsync();

            return Results.Ok(new
            {
                ticket.RepairTicketId,
                ticket.TicketCode,
                ticket.CustomerId,
                ticket.DeviceId,
                Device = ticket.Device != null ? new
                {
                    ticket.Device.DeviceId,
                    ticket.Device.ModelName,
                    ticket.Device.SerialNumber,
                    ticket.Device.Cpu,
                    ticket.Device.RamDescription,
                    ticket.Device.StorageDescription
                } : null,
                IssueDescription = ticket.CustomerIssue,
                ticket.InitialCondition,
                ticket.Accessories,
                StatusCode = ticket.Status,
                ticket.Priority,
                ticket.InspectionFee,
                ticket.ReceivedAtUtc,
                ticket.ExpectedCompletionAtUtc,
                ticket.ActualCompletionAtUtc,
                ticket.ClosedAtUtc,
                AssignedTechnician = ticket.AssignedTechnicianUser != null ? new
                {
                    ticket.AssignedTechnicianUser.UserId,
                    UserName = ticket.AssignedTechnicianUser.TechnicianProfile != null ? ticket.AssignedTechnicianUser.TechnicianProfile.DisplayName : ticket.AssignedTechnicianUser.UserName,
                    AvatarUrl = ticket.AssignedTechnicianUser.TechnicianProfile != null ? ticket.AssignedTechnicianUser.TechnicianProfile.AvatarUrl : null,
                    Specialty = ticket.AssignedTechnicianUser.TechnicianProfile != null ? ticket.AssignedTechnicianUser.TechnicianProfile.Specialty : null,
                    ExperienceYears = ticket.AssignedTechnicianUser.TechnicianProfile != null ? (int?)ticket.AssignedTechnicianUser.TechnicianProfile.ExperienceYears : null
                } : null,
                PreferredTechnician = ticket.PreferredTechnicianUser != null ? new
                {
                    ticket.PreferredTechnicianUser.UserId,
                    ticket.PreferredTechnicianUser.UserName
                } : null,
                Rating = ticket.TicketRating != null ? new
                {
                    ticket.TicketRating.RatingId,
                    ticket.TicketRating.Score,
                    ticket.TicketRating.Comment,
                    ticket.TicketRating.CreatedAtUtc
                } : null,
                Shipments = shipments.Select(s => new
                {
                    s.ShipmentId,
                    s.CarrierCode,
                    s.WaybillCode,
                    s.Direction,
                    s.StatusCode,
                    s.SenderAddress,
                    s.ReceiverAddress,
                    s.FeeAmount,
                    Events = s.Events.OrderBy(e => e.OccurredAtUtc).Select(e => new
                    {
                        e.EventId,
                        e.ProviderStatus,
                        e.InternalStatus,
                        e.Description,
                        e.LocationText,
                        e.OccurredAtUtc
                    })
                })
            });
        });

        // 4b. Submit Rating for Completed Ticket
        group.MapPost("/tickets/{ticketId:long}/rating", async (long ticketId, SubmitTicketRatingRequest req, HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            if (req.Score < 1 || req.Score > 5)
            {
                return Results.BadRequest(new { message = "Điểm đánh giá phải từ 1 đến 5 sao." });
            }

            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            var ticket = await db.RepairTickets
                .Include(t => t.TicketRating)
                .FirstOrDefaultAsync(t => t.RepairTicketId == ticketId);

            if (ticket == null)
            {
                return Results.NotFound(new { message = "Không tìm thấy phiếu sửa chữa." });
            }

            // CRITICAL OWNERSHIP CHECK: Only ticket owner can rate
            if (ticket.CustomerId != customer!.CustomerId)
            {
                return Results.Forbid();
            }

            // Status check: Only COMPLETED or RETURNED
            if (ticket.Status != "COMPLETED" && ticket.Status != "RETURNED")
            {
                return Results.BadRequest(new { message = "Chỉ có thể đánh giá phiếu sửa chữa khi máy đã hoàn thành hoặc đã trả về." });
            }

            if (!ticket.AssignedTechnicianUserId.HasValue)
            {
                return Results.BadRequest(new { message = "Phiếu sửa chữa chưa được phân công kỹ thuật viên." });
            }

            // Double rating check: Only 1 rating allowed
            if (ticket.TicketRating != null || await db.TicketRatings.AnyAsync(r => r.RepairTicketId == ticketId))
            {
                return Results.BadRequest(new { message = "Phiếu sửa chữa này đã được đánh giá trước đó." });
            }

            var rating = new TicketRating
            {
                RepairTicketId = ticketId,
                CustomerId = customer.CustomerId,
                TechnicianUserId = ticket.AssignedTechnicianUserId.Value,
                Score = req.Score,
                Comment = req.Comment?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            db.TicketRatings.Add(rating);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                success = true,
                ratingId = rating.RatingId,
                message = "Cảm ơn bạn đã gửi đánh giá chất lượng sửa chữa!"
            });
        });

        // 5. Customer Appointments
        group.MapGet("/appointments", async (HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            var appointments = await db.Appointments.AsNoTracking()
                .Where(a => a.CustomerId == customer!.CustomerId)
                .OrderByDescending(a => a.ScheduledAtUtc)
                .Select(a => new
                {
                    a.AppointmentId,
                    a.AppointmentCode,
                    a.ScheduledAtUtc,
                    StatusCode = a.Status,
                    a.Subject,
                    a.CustomerDescription,
                    a.CreatedAtUtc
                })
                .ToListAsync();

            return Results.Ok(appointments);
        });

        // 6. Customer Invoices
        group.MapGet("/invoices", async (HttpContext ctx, ITechCareDbContext db, ICurrentUserService current) =>
        {
            var (customer, error) = await ResolveCurrentCustomerAsync(ctx, db, current);
            if (error != null) return error;

            var invoices = await db.Invoices.AsNoTracking()
                .Include(i => i.RepairTicket)
                .Where(i => i.RepairTicket.CustomerId == customer!.CustomerId)
                .OrderByDescending(i => i.IssuedAtUtc)
                .Select(i => new
                {
                    i.InvoiceId,
                    i.InvoiceNumber,
                    i.InvoiceType,
                    i.RepairTicketId,
                    SubTotal = i.Subtotal,
                    i.TaxAmount,
                    i.DiscountAmount,
                    TotalAmount = i.TotalAmount ?? 0,
                    PaymentStatus = i.Status,
                    i.IssuedAtUtc,
                    i.Note
                })
                .ToListAsync();

            return Results.Ok(invoices);
        });
    }

    private static async Task<(Customer? Customer, IResult? ErrorResult)> ResolveCurrentCustomerAsync(
        HttpContext ctx,
        ITechCareDbContext db,
        ICurrentUserService current)
    {
        long? userId = current.UserId;
        if (!userId.HasValue)
        {
            var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")?.Value;
            if (long.TryParse(sub, out var parsed))
            {
                userId = parsed;
            }
        }

        if (!userId.HasValue)
        {
            return (null, Results.Unauthorized());
        }

        var customer = await db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId.Value);

        if (customer == null)
        {
            return (null, Results.NotFound(new { message = "Không tìm thấy hồ sơ khách hàng liên kết với tài khoản này." }));
        }

        return (customer, null);
    }
}
