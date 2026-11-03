using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class TicketStatusHistory
{
    public long TicketStatusHistoryId { get; set; }

    public long RepairTicketId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public DateTime ChangedAtUtc { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Note { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual RepairTicket RepairTicket { get; set; } = null!;
}
