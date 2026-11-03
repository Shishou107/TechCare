using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long? CustomerId { get; set; }

    public long? UserId { get; set; }

    public string Channel { get; set; } = null!;

    public string? TemplateCode { get; set; }

    public string? Subject { get; set; }

    public string MessageBody { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? ScheduledAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual User? User { get; set; }
}
