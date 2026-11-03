using API_FixComputerCenter.Application.Common.DTOs;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Customers;

public record CustomerDto(
    long CustomerId,
    string CustomerCode,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? AddressLine,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc
);

public class CreateCustomerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? Notes { get; set; }
}

public class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^0\d{9,10}$").WithMessage("Số điện thoại không hợp lệ");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest req, CancellationToken ct = default);
}

public class CustomerService : ICustomerService
{
    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;

    public CustomerService(ITechCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => c.FullName.Contains(s) || c.Phone.Contains(s) || (c.Email != null && c.Email.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto(c.CustomerId, c.CustomerCode, c.FullName, c.Phone, c.Email, c.AddressLine, c.Notes, c.IsActive, c.CreatedAtUtc))
            .ToListAsync(ct);

        return PagedResult<CustomerDto>.Create(items, page, pageSize, total);
    }

    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        return new CustomerDto(c.CustomerId, c.CustomerCode, c.FullName, c.Phone, c.Email, c.AddressLine, c.Notes, c.IsActive, c.CreatedAtUtc);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest req, CancellationToken ct = default)
    {
        var code = "KH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var entity = new Customer
        {
            CustomerCode = code,
            FullName = req.FullName.Trim(),
            Phone = req.PhoneNumber.Trim(),
            Email = req.Email?.Trim(),
            AddressLine = req.AddressLine?.Trim(),
            Notes = req.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CREATE_CUSTOMER", nameof(Customer), entity.CustomerId.ToString(), null, entity, ct);

        return new CustomerDto(entity.CustomerId, entity.CustomerCode, entity.FullName, entity.Phone, entity.Email, entity.AddressLine, entity.Notes, entity.IsActive, entity.CreatedAtUtc);
    }
}
