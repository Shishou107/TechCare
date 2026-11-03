using API_FixComputerCenter.Application.Common.DTOs;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Appointments;

public record AppointmentDto(
    long AppointmentId,
    string AppointmentCode,
    int BranchId,
    long CustomerId,
    string CustomerName,
    string CustomerPhone,
    long? DeviceId,
    string? ModelName,
    DateTime ScheduledAtUtc,
    string Subject,
    string? CustomerDescription,
    string Status,
    DateTime CreatedAtUtc
);

public class BookAppointmentRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? ModelName { get; set; }
    public string? SerialNumber { get; set; }
    public string? Cpu { get; set; }
    public string? Ram { get; set; }
    public string? Storage { get; set; }
    public string? OperatingSystem { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public DateTime? ScheduledAtUtc { get; set; }
}

public class BookAppointmentValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CustomerPhone).NotEmpty().Matches(@"^0\d{9,10}$").WithMessage("Số điện thoại không đúng định dạng (0xxxxxxxxx)");
        RuleFor(x => x.IssueDescription).NotEmpty().MinimumLength(5).MaximumLength(1500);
    }
}

public interface IAppointmentService
{
    Task<PagedResult<AppointmentDto>> GetAppointmentsAsync(int page, int pageSize, string? status, CancellationToken ct = default);
    Task<AppointmentDto> BookAppointmentAsync(BookAppointmentRequest req, CancellationToken ct = default);
    Task<AppointmentDto?> TrackAppointmentAsync(string keyword, CancellationToken ct = default);
}

public class AppointmentService : IAppointmentService
{
    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public AppointmentService(ITechCareDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AppointmentDto>> GetAppointmentsAsync(int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var query = _db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Device).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.ScheduledAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AppointmentDto(
                a.AppointmentId,
                a.AppointmentCode,
                a.BranchId,
                a.CustomerId,
                a.Customer != null ? a.Customer.FullName : "",
                a.Customer != null ? a.Customer.Phone : "",
                a.DeviceId,
                a.Device != null ? a.Device.ModelName : "",
                a.ScheduledAtUtc,
                a.Subject,
                a.CustomerDescription,
                a.Status,
                a.CreatedAtUtc
            ))
            .ToListAsync(ct);

        return PagedResult<AppointmentDto>.Create(items, page, pageSize, total);
    }

    public async Task<AppointmentDto> BookAppointmentAsync(BookAppointmentRequest req, CancellationToken ct = default)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == req.CustomerPhone.Trim(), ct);
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = "KH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                FullName = req.CustomerName.Trim(),
                Phone = req.CustomerPhone.Trim(),
                Email = req.CustomerEmail?.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
        }

        long? deviceId = null;
        string modelName = req.ModelName?.Trim() ?? "Laptop";
        if (!string.IsNullOrWhiteSpace(req.ModelName) || !string.IsNullOrWhiteSpace(req.Cpu))
        {
            var device = new Device
            {
                CustomerId = customer.CustomerId,
                ModelName = modelName,
                SerialNumber = req.SerialNumber?.Trim(),
                Cpu = req.Cpu?.Trim(),
                RamDescription = req.Ram?.Trim(),
                StorageDescription = req.Storage?.Trim(),
                OperatingSystem = req.OperatingSystem?.Trim(),
                Notes = req.IssueDescription.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync(ct);
            deviceId = device.DeviceId;
        }

        var branchId = _currentUser.BranchId ?? 1;

        // C1: Generate unique appointment code with collision retry
        string apptCode = string.Empty;
        for (int i = 0; i < 5; i++)
        {
            var candidate = "APPT-" + Random.Shared.Next(100000, 999999);
            if (!await _db.Appointments.AnyAsync(a => a.AppointmentCode == candidate, ct))
            {
                apptCode = candidate;
                break;
            }
        }
        if (string.IsNullOrEmpty(apptCode))
        {
            apptCode = "APPT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        }

        var subject = req.IssueDescription.Length > 50 ? req.IssueDescription[..50] : req.IssueDescription;

        var appointment = new Appointment
        {
            BranchId = branchId,
            CustomerId = customer.CustomerId,
            DeviceId = deviceId,
            AppointmentCode = apptCode,
            ScheduledAtUtc = req.ScheduledAtUtc ?? DateTime.UtcNow.AddHours(2),
            Subject = subject,
            CustomerDescription = req.IssueDescription.Trim(),
            Status = "BOOKED",
            CreatedByUserId = _currentUser.UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("BOOK_APPOINTMENT", nameof(Appointment), appointment.AppointmentId.ToString(), null, appointment, ct);

        return new AppointmentDto(
            appointment.AppointmentId,
            appointment.AppointmentCode,
            appointment.BranchId,
            customer.CustomerId,
            customer.FullName,
            customer.Phone,
            deviceId,
            modelName,
            appointment.ScheduledAtUtc,
            appointment.Subject,
            appointment.CustomerDescription,
            appointment.Status,
            appointment.CreatedAtUtc
        );
    }

    public async Task<AppointmentDto?> TrackAppointmentAsync(string keyword, CancellationToken ct = default)
    {
        var clean = keyword.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(clean)) return null;

        var appt = await _db.Appointments.AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Device)
            .Where(a => a.AppointmentCode == clean || (a.Customer != null && a.Customer.Phone == keyword.Trim()))
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (appt == null) return null;

        return new AppointmentDto(
            appt.AppointmentId,
            appt.AppointmentCode,
            appt.BranchId,
            appt.CustomerId,
            appt.Customer?.FullName ?? "",
            appt.Customer?.Phone ?? "",
            appt.DeviceId,
            appt.Device?.ModelName ?? "Laptop",
            appt.ScheduledAtUtc,
            appt.Subject,
            appt.CustomerDescription,
            appt.Status,
            appt.CreatedAtUtc
        );
    }
}
