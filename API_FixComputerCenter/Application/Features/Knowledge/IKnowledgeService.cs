using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Features.AiChat;
using API_FixComputerCenter.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Knowledge;

public record CreateDocumentRequest(string Title, string Category, string RawContent);

public record KnowledgeDocumentDto(
    long DocumentId,
    string Title,
    string Category,
    string Status,
    int ChunkCount,
    DateTime CreatedAtUtc
);

public record RelevantChunkDto(
    long ChunkId,
    long DocumentId,
    string DocumentTitle,
    string Category,
    string ChunkContent,
    double Similarity
);

public interface IKnowledgeService
{
    Task<KnowledgeDocumentDto> IngestDocumentAsync(CreateDocumentRequest req, CancellationToken ct = default);
    Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(long documentId, CancellationToken ct = default);
    Task<List<RelevantChunkDto>> SearchChunksAsync(string query, int topK = 3, double threshold = 0.72, CancellationToken ct = default);
    Task<int> ReembedAllAsync(CancellationToken ct = default);
}

public class KnowledgeService : IKnowledgeService
{
    private readonly ITechCareDbContext _db;
    private readonly IEmbeddingService _embedding;

    public KnowledgeService(ITechCareDbContext db, IEmbeddingService embedding)
    {
        _db = db;
        _embedding = embedding;
    }

    public async Task<KnowledgeDocumentDto> IngestDocumentAsync(CreateDocumentRequest req, CancellationToken ct = default)
    {
        var doc = new KnowledgeDocument
        {
            Title = req.Title.Trim(),
            Category = req.Category.Trim(),
            RawContent = req.RawContent.Trim(),
            Status = "ACTIVE",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.KnowledgeDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        // Chunking: ~600 words with 80 words overlap
        var chunks = ChunkText(req.RawContent, targetWords: 600, overlapWords: 80);
        int index = 0;
        foreach (var cText in chunks)
        {
            var vector = await _embedding.GenerateEmbeddingAsync(cText, ct);
            var chunk = new KnowledgeChunk
            {
                DocumentId = doc.DocumentId,
                ChunkIndex = index++,
                Content = cText,
                Embedding = _embedding.VectorToBytes(vector),
                EmbeddingModel = _embedding.CurrentModelName,
                EmbeddingDim = vector.Length,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.KnowledgeChunks.Add(chunk);
        }

        await _db.SaveChangesAsync(ct);

        return new KnowledgeDocumentDto(
            doc.DocumentId,
            doc.Title,
            doc.Category ?? "Chung",
            doc.Status,
            chunks.Count,
            doc.CreatedAtUtc
        );
    }

    public async Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(CancellationToken ct = default)
    {
        var docs = await _db.KnowledgeDocuments
            .AsNoTracking()
            .Include(d => d.Chunks)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

        return docs.Select(d => new KnowledgeDocumentDto(
            d.DocumentId,
            d.Title,
            d.Category ?? "Chung",
            d.Status,
            d.Chunks.Count,
            d.CreatedAtUtc
        )).ToList();
    }

    public async Task<bool> DeleteDocumentAsync(long documentId, CancellationToken ct = default)
    {
        var doc = await _db.KnowledgeDocuments
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);

        if (doc == null) return false;

        _db.KnowledgeChunks.RemoveRange(doc.Chunks);
        _db.KnowledgeDocuments.Remove(doc);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<RelevantChunkDto>> SearchChunksAsync(string query, int topK = 3, double threshold = 0.72, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<RelevantChunkDto>();

        var queryVector = await _embedding.GenerateEmbeddingAsync(query, ct);
        var chunks = await _db.KnowledgeChunks
            .AsNoTracking()
            .Include(c => c.Document)
            .Where(c => c.Document != null && c.Document.Status == "ACTIVE")
            .ToListAsync(ct);

        var scored = new List<RelevantChunkDto>();

        var currentModel = _embedding.CurrentModelName;
        int currentDim = queryVector.Length;

        foreach (var c in chunks)
        {
            if (c.Embedding == null || c.Embedding.Length == 0) continue;

            // Only compare chunks with matching model and dimension
            if (c.EmbeddingDim.HasValue && c.EmbeddingDim.Value != currentDim) continue;
            if (!string.IsNullOrEmpty(c.EmbeddingModel) && c.EmbeddingModel != currentModel) continue;

            var chunkVector = _embedding.BytesToVector(c.Embedding);
            if (chunkVector.Length != currentDim) continue;

            var sim = _embedding.CosineSimilarity(queryVector, chunkVector);

            if (sim >= threshold)
            {
                scored.Add(new RelevantChunkDto(
                    c.ChunkId,
                    c.DocumentId,
                    c.Document?.Title ?? "Tài liệu",
                    c.Document?.Category ?? "Chung",
                    c.Content,
                    sim
                ));
            }
        }

        return scored.OrderByDescending(s => s.Similarity).Take(topK).ToList();
    }

    public async Task<int> ReembedAllAsync(CancellationToken ct = default)
    {
        var activeDocs = await _db.KnowledgeDocuments
            .Include(d => d.Chunks)
            .Where(d => d.Status == "ACTIVE")
            .ToListAsync(ct);

        int updatedCount = 0;
        foreach (var doc in activeDocs)
        {
            // Remove existing chunks
            _db.KnowledgeChunks.RemoveRange(doc.Chunks);

            // Re-chunk and re-embed with current model
            var chunks = ChunkText(doc.RawContent, targetWords: 600, overlapWords: 80);
            int index = 0;
            foreach (var cText in chunks)
            {
                var vector = await _embedding.GenerateEmbeddingAsync(cText, ct);
                var chunk = new KnowledgeChunk
                {
                    DocumentId = doc.DocumentId,
                    ChunkIndex = index++,
                    Content = cText,
                    Embedding = _embedding.VectorToBytes(vector),
                    EmbeddingModel = _embedding.CurrentModelName,
                    EmbeddingDim = vector.Length,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.KnowledgeChunks.Add(chunk);
                updatedCount++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return updatedCount;
    }

    private static List<string> ChunkText(string text, int targetWords = 600, int overlapWords = 80)
    {
        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        if (words.Length <= targetWords)
        {
            chunks.Add(string.Join(" ", words));
            return chunks;
        }

        int step = targetWords - overlapWords;
        for (int i = 0; i < words.Length; i += step)
        {
            var take = Math.Min(targetWords, words.Length - i);
            var slice = words.Skip(i).Take(take);
            chunks.Add(string.Join(" ", slice));
            if (i + targetWords >= words.Length) break;
        }

        return chunks;
    }
}
