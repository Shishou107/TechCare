using System;

namespace API_FixComputerCenter.Domain.Entities.Ai;

public class KnowledgeChunk
{
    public long ChunkId { get; set; }
    public long DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public byte[]? Embedding { get; set; } // float32[] little-endian
    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDim { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual KnowledgeDocument Document { get; set; } = null!;
}
