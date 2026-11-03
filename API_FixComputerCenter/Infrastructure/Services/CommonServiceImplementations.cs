using System.Security.Claims;
using System.Text.Json;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public long? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return long.TryParse(sub, out var id) ? id : null;
        }
    }

    public int? BranchId
    {
        get
        {
            var branch = User?.FindFirstValue("branch_id");
            return int.TryParse(branch, out var bId) ? bId : null;
        }
    }

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name) ?? User?.FindFirstValue("unique_name");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<string> Permissions => User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

public class AuditService : IAuditService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
    {
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITechCareDbContext>();
            var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
            var httpContext = _httpContextAccessor.HttpContext;

            var log = new AuditLog
            {
                BranchId = currentUser.BranchId,
                UserId = currentUser.UserId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
                CorrelationId = httpContext?.TraceIdentifier,
                OccurredAtUtc = DateTime.UtcNow
            };

            db.AuditLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Logging failure should never crash the main business operation
        }
    }
}
