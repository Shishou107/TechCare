using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Features.AiChat;
using API_FixComputerCenter.Application.Features.Knowledge;
using API_FixComputerCenter.Domain.Entities.Ai;
using API_FixComputerCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class FakeChatCompletionService : IAiChatCompletionService
{
    public bool HasApiKey => false;
    public string ModelName => "local-fallback";

    public Task<string?> CompleteChatAsync(IEnumerable<AiChatMessage> messages, CancellationToken ct = default)
    {
        // Simulate no API key configured (null triggers extractive / handoff logic)
        return Task.FromResult<string?>(null);
    }
}

public class RagKnowledgeTests
{
    private TechCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TechCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new TechCareDbContext(options);
    }

    private EmbeddingService CreateEmbeddingService()
    {
        var config = new ConfigurationBuilder().Build();
        return new EmbeddingService(new HttpClient(), config, NullLogger<EmbeddingService>.Instance);
    }

    // TEST 3 (RAG/AI): Cosine similarity và xếp hạng top-K
    [Fact]
    public async Task SearchChunks_BatteryInquiry_RanksBatteryChunkFirstWithHighSimilarity()
    {
        using var db = CreateDbContext("Test_TopK_Cosine_" + Guid.NewGuid());
        var embeddingSvc = CreateEmbeddingService();
        var knowledgeSvc = new KnowledgeService(db, embeddingSvc);

        // 1. Seed active document with 3 distinct chunks
        var doc = new KnowledgeDocument
        {
            Title = "Chính sách dịch vụ sửa chữa phần cứng",
            Category = "BẢNG GIÁ",
            RawContent = "Tổng hợp bảng giá dịch vụ.",
            Status = "ACTIVE",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.KnowledgeDocuments.Add(doc);
        await db.SaveChangesAsync();

        var chunkBatteryText = "Thay pin laptop Dell HP Lenovo chính hãng lấy ngay bảo hành 6 tháng pin chai phồng không nhận sạc";
        var chunkHingeText = "Sửa bản lề laptop gãy vỡ vỏ thay khung kim loại hàn khớp xoay màn hình";
        var chunkWinText = "Cài đặt Windows 11 bản quyền Office diệt virus tối ưu hệ điều hành máy tính";

        var vecBattery = await embeddingSvc.GenerateEmbeddingAsync(chunkBatteryText);
        var vecHinge = await embeddingSvc.GenerateEmbeddingAsync(chunkHingeText);
        var vecWin = await embeddingSvc.GenerateEmbeddingAsync(chunkWinText);

        db.KnowledgeChunks.AddRange(
            new KnowledgeChunk
            {
                DocumentId = doc.DocumentId,
                ChunkIndex = 0,
                Content = chunkBatteryText,
                Embedding = embeddingSvc.VectorToBytes(vecBattery),
                EmbeddingModel = embeddingSvc.CurrentModelName,
                EmbeddingDim = vecBattery.Length
            },
            new KnowledgeChunk
            {
                DocumentId = doc.DocumentId,
                ChunkIndex = 1,
                Content = chunkHingeText,
                Embedding = embeddingSvc.VectorToBytes(vecHinge),
                EmbeddingModel = embeddingSvc.CurrentModelName,
                EmbeddingDim = vecHinge.Length
            },
            new KnowledgeChunk
            {
                DocumentId = doc.DocumentId,
                ChunkIndex = 2,
                Content = chunkWinText,
                Embedding = embeddingSvc.VectorToBytes(vecWin),
                EmbeddingModel = embeddingSvc.CurrentModelName,
                EmbeddingDim = vecWin.Length
            }
        );
        await db.SaveChangesAsync();

        // 2. Query for battery issue: "pin sạc không vào"
        var results = await knowledgeSvc.SearchChunksAsync("pin sạc không vào", topK: 3, threshold: 0.3);

        // 3. Assertions: Battery chunk must be ranked first with score above threshold
        Assert.NotEmpty(results);
        var topResult = results[0];
        Assert.Contains("Thay pin", topResult.ChunkContent);
        Assert.True(topResult.Similarity >= 0.35, $"Expected similarity >= 0.35 but got {topResult.Similarity}");

        // Direct query with high overlap achieves > 0.6
        var directResults = await knowledgeSvc.SearchChunksAsync("thay pin laptop lấy ngay", topK: 3, threshold: 0.3);
        Assert.NotEmpty(directResults);
        Assert.True(directResults[0].Similarity > 0.6, $"Expected similarity > 0.6 but got {directResults[0].Similarity}");
    }

    // TEST 4 (RAG/AI): Handoff khi câu hỏi không khớp
    [Fact]
    public async Task CustomerChat_IrrelevantQuestion_TransitionsToHumanWithHandoffMessage()
    {
        using var db = CreateDbContext("Test_Handoff_" + Guid.NewGuid());
        var embeddingSvc = CreateEmbeddingService();
        var knowledgeSvc = new KnowledgeService(db, embeddingSvc);
        var aiChatSvc = new AiChatService(db, knowledgeSvc, new FakeChatCompletionService());

        // 1. Seed document about computer repairs
        var doc = new KnowledgeDocument
        {
            Title = "Quy trình vệ sinh laptop",
            Category = "DỊCH VỤ",
            RawContent = "Vệ sinh tra keo tản nhiệt CPU GPU bảo dưỡng máy tính.",
            Status = "ACTIVE",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.KnowledgeDocuments.Add(doc);
        await db.SaveChangesAsync();

        var vec = await embeddingSvc.GenerateEmbeddingAsync(doc.RawContent);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            DocumentId = doc.DocumentId,
            ChunkIndex = 0,
            Content = doc.RawContent,
            Embedding = embeddingSvc.VectorToBytes(vec),
            EmbeddingModel = embeddingSvc.CurrentModelName,
            EmbeddingDim = vec.Length
        });
        await db.SaveChangesAsync();

        // 2. Customer asks completely out-of-scope question
        var req = new CustomerChatMessageRequest(
            SessionId: null,
            CustomerPhone: "0912345678",
            Message: "tiệm có bán trà sữa trân châu đường đen trưa nay không"
        );

        var response = await aiChatSvc.HandleCustomerMessageAsync(req);

        // 3. Assertions: Handoff triggered, status = HUMAN
        Assert.Equal("HUMAN", response.Status);
        Assert.Contains("Thông tin này mình chưa có sẵn, mình chuyển câu hỏi cho nhân viên hỗ trợ giúp bạn nhé.", response.Reply);

        var sessionGuid = Guid.Parse(response.SessionId);
        var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionGuid);
        Assert.NotNull(session);
        Assert.Equal("HUMAN", session.Status);
    }

    // TEST 5 (RAG/AI): Chunk của tài liệu bị vô hiệu hóa (Status != 'ACTIVE') bị loại bỏ
    [Fact]
    public async Task SearchChunks_DisabledDocumentChunks_NeverAppearInSearchResults()
    {
        using var db = CreateDbContext("Test_Disabled_Doc_" + Guid.NewGuid());
        var embeddingSvc = CreateEmbeddingService();
        var knowledgeSvc = new KnowledgeService(db, embeddingSvc);

        // Document A: INACTIVE / DISABLED
        var disabledDoc = new KnowledgeDocument
        {
            Title = "Tài liệu bảo mật nội bộ ngưng áp dụng",
            Category = "NỘI BỘ",
            RawContent = "Quy định giảm giá đặc biệt 90% cho dịch vụ thay màn hình OLED.",
            Status = "INACTIVE",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.KnowledgeDocuments.Add(disabledDoc);

        // Document B: ACTIVE
        var activeDoc = new KnowledgeDocument
        {
            Title = "Bảng giá thay màn hình tiêu chuẩn",
            Category = "BẢNG GIÁ",
            RawContent = "Bảng giá công khai dịch vụ thay màn hình laptop chính hãng.",
            Status = "ACTIVE",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.KnowledgeDocuments.Add(activeDoc);
        await db.SaveChangesAsync();

        var vecDisabled = await embeddingSvc.GenerateEmbeddingAsync(disabledDoc.RawContent);
        var vecActive = await embeddingSvc.GenerateEmbeddingAsync(activeDoc.RawContent);

        db.KnowledgeChunks.AddRange(
            new KnowledgeChunk
            {
                DocumentId = disabledDoc.DocumentId,
                ChunkIndex = 0,
                Content = disabledDoc.RawContent,
                Embedding = embeddingSvc.VectorToBytes(vecDisabled),
                EmbeddingModel = embeddingSvc.CurrentModelName,
                EmbeddingDim = vecDisabled.Length
            },
            new KnowledgeChunk
            {
                DocumentId = activeDoc.DocumentId,
                ChunkIndex = 0,
                Content = activeDoc.RawContent,
                Embedding = embeddingSvc.VectorToBytes(vecActive),
                EmbeddingModel = embeddingSvc.CurrentModelName,
                EmbeddingDim = vecActive.Length
            }
        );
        await db.SaveChangesAsync();

        // Query matching the disabled document's phrase
        var results = await knowledgeSvc.SearchChunksAsync("giảm giá đặc biệt 90% thay màn hình OLED", topK: 5, threshold: 0.1);

        // None of the returned chunks should come from the disabled document
        Assert.DoesNotContain(results, r => r.DocumentId == disabledDoc.DocumentId);
        Assert.All(results, r => Assert.NotEqual(disabledDoc.DocumentId, r.DocumentId));
    }
}

