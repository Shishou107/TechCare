using System;
using System.Collections.Generic;
using API_FixComputerCenter.Domain.Entities;

namespace API_FixComputerCenter.Domain.Entities.Ai;

public class ChatSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public long? CustomerId { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string Status { get; set; } = "BOT"; // "BOT", "HUMAN", "CLOSED"
    public long? AssignedSupportId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public virtual Customer? Customer { get; set; }
    public virtual User? AssignedSupportUser { get; set; }
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

