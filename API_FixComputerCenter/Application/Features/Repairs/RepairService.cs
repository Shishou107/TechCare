using System.Security.Cryptography;
using System.Text;
using API_FixComputerCenter.Application.Common.DTOs;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Repairs;

public record RepairTicketDto(
    long RepairTicketId,
    string TicketCode,
    int BranchId,
    long CustomerId,
    string CustomerName,
    string CustomerPhone,
    long DeviceId,
    string ModelName,
    string? SerialNumber,
    long? CreatedByUserId,
    long? AssignedTechnicianUserId,
    string? TechnicianName,
    DateTime ReceivedAtUtc,
    DateTime? ExpectedCompletionAtUtc,
    DateTime? ActualCompletionAtUtc,
    string CustomerIssue,
    string? InitialCondition,
    string? Accessories,
    string Status,
    string? TrackingCode = null
);

public class CreateRepairTicketRequest
{
    public long CustomerId { get; set; }
    public long DeviceId { get; set; }
    public string CustomerIssue { get; set; } = string.Empty;
    public string? InitialCondition { get; set; }
    public string? Accessories { get; set; }
    public DateTime? ExpectedCompletionAtUtc { get; set; }
}

public class AssignTechnicianRequest
{
    public long TechnicianUserId { get; set; }
}

public class UpdateRepairStatusRequest
{
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class CreateRepairTicketValidator : AbstractValidator<CreateRepairTicketRequest>
{
    public CreateRepairTicketValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.DeviceId).GreaterThan(0);
        RuleFor(x => x.CustomerIssue).NotEmpty().MinimumLength(5).MaximumLength(2000);
    }
}

public interface IRepairService
{
    Task<PagedResult<RepairTicketDto>> GetTicketsAsync(int page, int pageSize, string? status, CancellationToken ct = default);
    Task<RepairTicketDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<RepairTicketDto> CreateTicketAsync(CreateRepairTicketRequest req, CancellationToken ct = default);
    Task<RepairTicketDto> AssignTechnicianAsync(long ticketId, AssignTechnicianRequest req, CancellationToken ct = default);
    Task<RepairTicketDto> UpdateStatusAsync(long ticketId, UpdateRepairStatusRequest req, CancellationToken ct = default);
    Task<RepairTicketDto?> TrackTicketAsync(string code, string phone, CancellationToken ct = default);
}

public class RepairService : IRepairService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "RECEIVED", "DIAGNOSING", "WAITING_APPROVAL", "WAITING_PARTS",
        "REPAIRING", "QUALITY_CHECK", "READY_FOR_PICKUP", "COMPLETED",
        "RETURNED", "CANCELLED"
    };

    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public RepairService(ITechCareDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<RepairTicketDto>> GetTicketsAsync(int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var query = _db.RepairTickets.AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Device)
            .Include(t => t.AssignedTechnicianUser)
            .AsQueryable();

        if (_currentUser.BranchId.HasValue)
        {
            query = query.Where(t => t.BranchId == _currentUser.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new RepairTicketDto(
                t.RepairTicketId,
                t.TicketCode,
                t.BranchId,
                t.CustomerId,
                t.Customer != null ? t.Customer.FullName : "",
                t.Customer != null ? t.Customer.Phone : "",
                t.DeviceId,
                t.Device != null ? t.Device.ModelName : "",
                t.Device != null ? t.Device.SerialNumber : null,
                t.CreatedByUserId,
                t.AssignedTechnicianUserId,
                t.AssignedTechnicianUser != null ? t.AssignedTechnicianUser.UserName : null,
                t.ReceivedAtUtc,
                t.ExpectedCompletionAtUtc,
                t.ActualCompletionAtUtc,
                t.CustomerIssue,
                t.InitialCondition,
                t.Accessories,
                t.Status,
                null
            ))
            .ToListAsync(ct);

        return PagedResult<RepairTicketDto>.Create(items, page, pageSize, total);
    }

    public async Task<RepairTicketDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var t = await _db.RepairTickets.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Device)
            .Include(x => x.AssignedTechnicianUser)
            .FirstOrDefaultAsync(x => x.RepairTicketId == id, ct)
            ?? throw new NotFoundException(nameof(RepairTicket), id);

        if (_currentUser.BranchId.HasValue && t.BranchId != _currentUser.BranchId.Value)
        {
            throw new ForbiddenException("Không có quyền truy cập phiếu sửa chữa của chi nhánh khác.");
        }

        return new RepairTicketDto(
            t.RepairTicketId,
            t.TicketCode,
            t.BranchId,
            t.CustomerId,
            t.Customer?.FullName ?? "",
            t.Customer?.Phone ?? "",
            t.DeviceId,
            t.Device?.ModelName ?? "",
            t.Device?.SerialNumber,
            t.CreatedByUserId,
            t.AssignedTechnicianUserId,
            t.AssignedTechnicianUser?.UserName,
            t.ReceivedAtUtc,
            t.ExpectedCompletionAtUtc,
            t.ActualCompletionAtUtc,
            t.CustomerIssue,
            t.InitialCondition,
            t.Accessories,
            t.Status,
            null
        );
    }

    public async Task<RepairTicketDto> CreateTicketAsync(CreateRepairTicketRequest req, CancellationToken ct = default)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var customer = await _db.Customers.FindAsync([req.CustomerId], ct)
            ?? throw new NotFoundException(nameof(Customer), req.CustomerId);

        var device = await _db.Devices.FindAsync([req.DeviceId], ct)
            ?? throw new NotFoundException(nameof(Device), req.DeviceId);

        if (device.CustomerId != customer.CustomerId)
        {
            throw new DomainException("Thiết bị này không thuộc về khách hàng đã chọn.");
        }

        var branchId = _currentUser.BranchId ?? 1;
        var createdBy = _currentUser.UserId;
        if (!createdBy.HasValue)
        {
            var fallbackUser = await _db.Users.Select(u => u.UserId).FirstOrDefaultAsync(ct);
            createdBy = fallbackUser != 0 ? fallbackUser : 1;
        }

        // C1: Generate unique ticket code with collision retry
        string ticketCode = string.Empty;
        for (int i = 0; i < 5; i++)
        {
            var candidate = "TC-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Random.Shared.Next(1000, 9999);
            if (!await _db.RepairTickets.AnyAsync(t => t.TicketCode == candidate, ct))
            {
                ticketCode = candidate;
                break;
            }
        }
        if (string.IsNullOrEmpty(ticketCode))
        {
            ticketCode = "TC-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        }

        // B6: Generate TrackingCode and store SHA-256 hash
        var rawTrackingCode = "TRK-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var trackingHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawTrackingCode));

        var ticket = new RepairTicket
        {
            TicketCode = ticketCode,
            TrackingCodeHash = trackingHash,
            BranchId = branchId,
            CustomerId = customer.CustomerId,
            DeviceId = device.DeviceId,
            CreatedByUserId = createdBy,
            ReceivedAtUtc = DateTime.UtcNow,
            ExpectedCompletionAtUtc = req.ExpectedCompletionAtUtc,
            CustomerIssue = req.CustomerIssue.Trim(),
            InitialCondition = req.InitialCondition?.Trim(),
            Accessories = req.Accessories?.Trim(),
            Status = "RECEIVED",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.RepairTickets.Add(ticket);

        var history = new TicketStatusHistory
        {
            RepairTicket = ticket,
            OldStatus = null,
            NewStatus = "RECEIVED",
            ChangedByUserId = createdBy.Value,
            ChangedAtUtc = DateTime.UtcNow,
            Note = "Tiếp nhận thiết bị tại quầy"
        };
        _db.TicketStatusHistories.Add(history);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("CREATE_REPAIR_TICKET", nameof(RepairTicket), ticket.RepairTicketId.ToString(), null, ticket, ct);

        var dto = await GetByIdAsync(ticket.RepairTicketId, ct);
        return dto with { TrackingCode = rawTrackingCode };
    }

    public async Task<RepairTicketDto> AssignTechnicianAsync(long ticketId, AssignTechnicianRequest req, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.RepairTicketId == ticketId, ct)
            ?? throw new NotFoundException(nameof(RepairTicket), ticketId);

        var techUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == req.TechnicianUserId && u.IsActive && u.DeletedAtUtc == null, ct)
            ?? throw new NotFoundException(nameof(User), req.TechnicianUserId);

        var oldTech = ticket.AssignedTechnicianUserId;
        ticket.AssignedTechnicianUserId = techUser.UserId;

        var performerId = _currentUser.UserId;
        if (!performerId.HasValue)
        {
            var fallbackUser = await _db.Users.Select(u => u.UserId).FirstOrDefaultAsync(ct);
            performerId = fallbackUser != 0 ? fallbackUser : 1;
        }

        if (ticket.Status == "RECEIVED")
        {
            ticket.Status = "DIAGNOSING";
            _db.TicketStatusHistories.Add(new TicketStatusHistory
            {
                RepairTicketId = ticket.RepairTicketId,
                OldStatus = "RECEIVED",
                NewStatus = "DIAGNOSING",
                ChangedByUserId = performerId.Value,
                ChangedAtUtc = DateTime.UtcNow,
                Note = $"Phân công kỹ thuật viên: {techUser.UserName}"
            });
        }

        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ASSIGN_TECHNICIAN", nameof(RepairTicket), ticket.RepairTicketId.ToString(), new { OldTech = oldTech }, new { NewTech = techUser.UserId }, ct);

        return await GetByIdAsync(ticket.RepairTicketId, ct);
    }

    public async Task<RepairTicketDto> UpdateStatusAsync(long ticketId, UpdateRepairStatusRequest req, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.RepairTicketId == ticketId, ct)
            ?? throw new NotFoundException(nameof(RepairTicket), ticketId);

        var fromStatus = ticket.Status;
        var toStatus = req.NewStatus.Trim().ToUpperInvariant();

        // B4: Map or validate status against CK_Ticket_Status
        if (toStatus == "DELIVERED")
        {
            toStatus = "RETURNED";
        }

        if (!AllowedStatuses.Contains(toStatus))
        {
            throw new ValidationException($"Trạng thái '{req.NewStatus}' không hợp lệ. Các trạng thái được phép: {string.Join(", ", AllowedStatuses)}.");
        }

        ticket.Status = toStatus;
        if (toStatus == "READY_FOR_PICKUP" || toStatus == "COMPLETED" || toStatus == "RETURNED")
        {
            ticket.ActualCompletionAtUtc ??= DateTime.UtcNow;
        }
        if (toStatus == "RETURNED" || toStatus == "CANCELLED")
        {
            ticket.ClosedAtUtc = DateTime.UtcNow;
        }

        ticket.UpdatedAtUtc = DateTime.UtcNow;

        var performerId = _currentUser.UserId;
        if (!performerId.HasValue)
        {
            var fallbackUser = await _db.Users.Select(u => u.UserId).FirstOrDefaultAsync(ct);
            performerId = fallbackUser != 0 ? fallbackUser : 1;
        }

        _db.TicketStatusHistories.Add(new TicketStatusHistory
        {
            RepairTicketId = ticket.RepairTicketId,
            OldStatus = fromStatus,
            NewStatus = toStatus,
            ChangedByUserId = performerId.Value,
            ChangedAtUtc = DateTime.UtcNow,
            Note = req.Note?.Trim()
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UPDATE_REPAIR_STATUS", nameof(RepairTicket), ticket.RepairTicketId.ToString(), new { From = fromStatus }, new { To = toStatus }, ct);

        return await GetByIdAsync(ticket.RepairTicketId, ct);
    }

    public async Task<RepairTicketDto?> TrackTicketAsync(string code, string phone, CancellationToken ct = default)
    {
        var cleanCode = code.Trim().ToUpperInvariant();
        var cleanPhone = phone.Trim();

        var ticket = await _db.RepairTickets.AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Device)
            .FirstOrDefaultAsync(t => (t.TicketCode == cleanCode || t.Appointment != null && t.Appointment.AppointmentCode == cleanCode)
                && t.Customer != null && t.Customer.Phone == cleanPhone, ct);

        if (ticket == null) return null;

        return new RepairTicketDto(
            ticket.RepairTicketId,
            ticket.TicketCode,
            ticket.BranchId,
            ticket.CustomerId,
            ticket.Customer?.FullName ?? "",
            ticket.Customer?.Phone ?? "",
            ticket.DeviceId,
            ticket.Device?.ModelName ?? "",
            ticket.Device?.SerialNumber,
            null,
            null,
            null,
            ticket.ReceivedAtUtc,
            ticket.ExpectedCompletionAtUtc,
            ticket.ActualCompletionAtUtc,
            ticket.CustomerIssue,
            ticket.InitialCondition,
            ticket.Accessories,
            ticket.Status,
            null
        );
    }
}
