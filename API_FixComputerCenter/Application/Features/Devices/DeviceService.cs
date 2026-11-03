using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Devices;

public record DeviceDto(
    long DeviceId,
    long CustomerId,
    string ModelName,
    string? SerialNumber,
    string? Cpu,
    string? RamDescription,
    string? StorageDescription,
    string? OperatingSystem,
    string? Notes,
    DateTime CreatedAtUtc
);

public record RegisterDeviceRequest(
    long CustomerId,
    string ModelName,
    string? SerialNumber,
    string? Cpu,
    string? RamDescription,
    string? StorageDescription,
    string? OperatingSystem,
    string? Notes
);

public class RegisterDeviceValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.ModelName).NotEmpty().MaximumLength(150);
    }
}

public interface IDeviceService
{
    Task<List<DeviceDto>> GetCustomerDevicesAsync(long customerId, CancellationToken ct = default);
    Task<DeviceDto> RegisterDeviceAsync(RegisterDeviceRequest req, CancellationToken ct = default);
}

public class DeviceService : IDeviceService
{
    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;

    public DeviceService(ITechCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<DeviceDto>> GetCustomerDevicesAsync(long customerId, CancellationToken ct = default)
    {
        return await _db.Devices.AsNoTracking()
            .Where(d => d.CustomerId == customerId)
            .Select(d => new DeviceDto(d.DeviceId, d.CustomerId, d.ModelName, d.SerialNumber, d.Cpu, d.RamDescription, d.StorageDescription, d.OperatingSystem, d.Notes, d.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<DeviceDto> RegisterDeviceAsync(RegisterDeviceRequest req, CancellationToken ct = default)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.CustomerId == req.CustomerId, ct);
        if (!customerExists) throw new NotFoundException(nameof(Customer), req.CustomerId);

        var device = new Device
        {
            CustomerId = req.CustomerId,
            ModelName = req.ModelName.Trim(),
            SerialNumber = req.SerialNumber?.Trim(),
            Cpu = req.Cpu?.Trim(),
            RamDescription = req.RamDescription?.Trim(),
            StorageDescription = req.StorageDescription?.Trim(),
            OperatingSystem = req.OperatingSystem?.Trim(),
            Notes = req.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("REGISTER_DEVICE", nameof(Device), device.DeviceId.ToString(), null, device, ct);

        return new DeviceDto(device.DeviceId, device.CustomerId, device.ModelName, device.SerialNumber, device.Cpu, device.RamDescription, device.StorageDescription, device.OperatingSystem, device.Notes, device.CreatedAtUtc);
    }
}

