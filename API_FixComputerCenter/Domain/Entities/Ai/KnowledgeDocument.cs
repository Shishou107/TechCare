using System;
using System.Collections.Generic;
using API_FixComputerCenter.Domain.Entities;

namespace API_FixComputerCenter.Domain.Entities.Ai;

public class KnowledgeDocument
{
    public long DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string SourceType { get; set; } = "TEXT"; // "TEXT", "FILE", "URL"
    public string? OriginalFileName { get; set; }
    public string RawContent { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE"; // "DRAFT", "ACTIVE", "DISABLED"
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}

