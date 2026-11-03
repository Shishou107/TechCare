using System;

namespace API_FixComputerCenter.Domain.Entities;

public class TicketRating
{
    public long RatingId { get; set; }
    public long RepairTicketId { get; set; }
    public long CustomerId { get; set; }
    public long TechnicianUserId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public virtual RepairTicket RepairTicket { get; set; } = null!;
    public virtual Customer Customer { get; set; } = null!;
    public virtual User TechnicianUser { get; set; } = null!;
}

