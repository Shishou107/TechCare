namespace API_FixComputerCenter.Application.Common.Interfaces;

public interface ICurrentUserService
{
    long? UserId { get; }
    int? BranchId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool HasPermission(string permission);
}

public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? entityId, object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default);
}

