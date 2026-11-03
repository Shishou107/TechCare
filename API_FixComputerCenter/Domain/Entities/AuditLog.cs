using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public long? UserId { get; set; }

    public int? BranchId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string? EntityId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? CorrelationId { get; set; }

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual User? User { get; set; }
}
