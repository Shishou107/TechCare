using System;
using API_FixComputerCenter.Domain.Entities;

namespace API_FixComputerCenter.Domain.Entities.Ai;

public class ChatMessage
{
    public long MessageId { get; set; }
    public Guid SessionId { get; set; }
    public string SenderType { get; set; } = "CUSTOMER"; // "CUSTOMER", "BOT", "SUPPORT", "SYSTEM"
    public long? SenderUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? SourcesJson { get; set; } // JSON list of cited RAG sources for bot messages
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ChatSession Session { get; set; } = null!;
    public virtual User? SenderUser { get; set; }
}

