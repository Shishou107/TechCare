using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Features.Knowledge;
using API_FixComputerCenter.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.AiChat;

public record CustomerChatMessageRequest(
    string? SessionId,
    string? CustomerPhone,
    string Message
);

public record ChatCitationDto(
    string Title,
    string Snippet
);

public record ChatResponseDto(
    string SessionId,
    string Status, // "BOT" | "HUMAN"
    string Reply,
    List<ChatCitationDto> Sources
);

public record ChatMessageDto(
    long MessageId,
    string SessionId,
    string SenderType, // "CUSTOMER" | "BOT" | "SUPPORT"
    string Content,
    List<ChatCitationDto>? Sources,
    DateTime CreatedAtUtc
);

public record ChatSessionDto(
    string SessionId,
    string? GuestPhone,
    string Status, // "BOT", "HUMAN", "CLOSED"
    long? AssignedSupportId,
    string? LastMessage,
    DateTime StartedAtUtc,
    DateTime? ClosedAtUtc
);

public interface IAiChatService
{
    Task<ChatResponseDto> HandleCustomerMessageAsync(CustomerChatMessageRequest req, CancellationToken ct = default);
    Task<List<ChatSessionDto>> GetSupportSessionsAsync(string? status, CancellationToken ct = default);
    Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, CancellationToken ct = default);
    Task<bool> ClaimSessionAsync(Guid sessionId, long staffUserId, CancellationToken ct = default);
    Task<ChatMessageDto?> StaffReplyAsync(Guid sessionId, long staffUserId, string message, CancellationToken ct = default);
    Task<bool> CloseSessionAsync(Guid sessionId, long staffUserId, CancellationToken ct = default);
}

public class AiChatService : IAiChatService
{
    private readonly ITechCareDbContext _db;
    private readonly IKnowledgeService _knowledge;

    public AiChatService(ITechCareDbContext db, IKnowledgeService knowledge)
    {
        _db = db;
        _knowledge = knowledge;
    }

    public async Task<ChatResponseDto> HandleCustomerMessageAsync(CustomerChatMessageRequest req, CancellationToken ct = default)
    {
        Guid sessGuid;
        ChatSession? session = null;

        if (!string.IsNullOrWhiteSpace(req.SessionId) && Guid.TryParse(req.SessionId, out sessGuid))
        {
            session = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.SessionId == sessGuid, ct);
        }

        if (session == null)
        {
            sessGuid = Guid.NewGuid();
            session = new ChatSession
            {
                SessionId = sessGuid,
                GuestPhone = req.CustomerPhone?.Trim(),
                Status = "BOT",
                StartedAtUtc = DateTime.UtcNow,
                LastMessageAtUtc = DateTime.UtcNow
            };
            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }

        // 2. Save customer message
        var customerMsg = new ChatMessage
        {
            SessionId = session.SessionId,
            SenderType = "CUSTOMER",
            Content = req.Message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ChatMessages.Add(customerMsg);
        session.LastMessageAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 3. If session is already assigned to HUMAN
        if (session.Status == "HUMAN")
        {
            var holdReply = "Cuộc trò chuyện của quý khách đang được chuyên viên tư vấn tiếp nhận. Chuyên viên sẽ trả lời trực tiếp trong ít phút ạ.";
            return new ChatResponseDto(session.SessionId.ToString(), "HUMAN", holdReply, new List<ChatCitationDto>());
        }

        // 4. Check if customer requests a human or expresses dissatisfaction
        var lowerMsg = req.Message.ToLowerInvariant();
        bool requestsHuman = lowerMsg.Contains("gặp người") || lowerMsg.Contains("nhân viên") ||
                             lowerMsg.Contains("tổng đài") || lowerMsg.Contains("khiếu nại") ||
                             lowerMsg.Contains("tư vấn viên") || lowerMsg.Contains("chăm sóc khách hàng");

        if (requestsHuman)
        {
            session.Status = "HUMAN";
            await _db.SaveChangesAsync(ct);

            var handoffMsg = "Em đã chuyển tiếp cuộc hội thoại cho chuyên viên TechCare. Anh/Chị vui lòng chờ trong giây lát, chuyên viên sẽ phản hồi trực tiếp qua khung chat này ạ.";
            var aiHandoffMsg = new ChatMessage
            {
                SessionId = session.SessionId,
                SenderType = "BOT",
                Content = handoffMsg,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ChatMessages.Add(aiHandoffMsg);
            await _db.SaveChangesAsync(ct);

            return new ChatResponseDto(session.SessionId.ToString(), "HUMAN", handoffMsg, new List<ChatCitationDto>());
        }

        // 5. Search knowledge base chunks (threshold: 0.72)
        var relevantChunks = await _knowledge.SearchChunksAsync(req.Message, topK: 3, threshold: 0.72, ct);

        // If no confident chunks found, transition to HUMAN
        if (relevantChunks.Count == 0)
        {
            session.Status = "HUMAN";
            await _db.SaveChangesAsync(ct);

            var fallbackHandoff = "Em chưa tìm thấy thông tin phù hợp trong cơ sở dữ liệu để giải đáp chính xác yêu cầu này. Em đã kết nối câu hỏi này đến nhân viên kỹ thuật TechCare để tư vấn chi tiết cho Anh/Chị ngay ạ.";
            var aiFallbackMsg = new ChatMessage
            {
                SessionId = session.SessionId,
                SenderType = "BOT",
                Content = fallbackHandoff,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ChatMessages.Add(aiFallbackMsg);
            await _db.SaveChangesAsync(ct);

            return new ChatResponseDto(session.SessionId.ToString(), "HUMAN", fallbackHandoff, new List<ChatCitationDto>());
        }

        // 6. Confident RAG Answer Generation
        var topDoc = relevantChunks[0];
        var answer = $"Theo tài liệu của TechCare ({topDoc.DocumentTitle}): {topDoc.ChunkContent}";
        if (relevantChunks.Count > 1)
        {
            answer += $"\n\nBổ sung thêm: {relevantChunks[1].ChunkContent}";
        }

        var citations = relevantChunks.Select(c => new ChatCitationDto(
            c.DocumentTitle,
            c.ChunkContent.Length > 120 ? c.ChunkContent[..120] + "..." : c.ChunkContent
        )).ToList();

        var aiMsg = new ChatMessage
        {
            SessionId = session.SessionId,
            SenderType = "BOT",
            Content = answer,
            SourcesJson = JsonSerializer.Serialize(citations),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ChatMessages.Add(aiMsg);
        await _db.SaveChangesAsync(ct);

        return new ChatResponseDto(session.SessionId.ToString(), "BOT", answer, citations);
    }

    public async Task<List<ChatSessionDto>> GetSupportSessionsAsync(string? status, CancellationToken ct = default)
    {
        var query = _db.ChatSessions
            .AsNoTracking()
            .Include(s => s.Messages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status.ToUpper() != "ALL")
        {
            query = query.Where(s => s.Status == status.ToUpper());
        }

        var list = await query
            .OrderByDescending(s => s.LastMessageAtUtc)
            .ToListAsync(ct);

        return list.Select(s => new ChatSessionDto(
            s.SessionId.ToString(),
            s.GuestPhone,
            s.Status,
            s.AssignedSupportId,
            s.Messages.OrderByDescending(m => m.CreatedAtUtc).FirstOrDefault()?.Content,
            s.StartedAtUtc,
            s.ClosedAtUtc
        )).ToList();
    }

    public async Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, CancellationToken ct = default)
    {
        var msgs = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        return msgs.Select(m =>
        {
            List<ChatCitationDto>? citations = null;
            if (!string.IsNullOrEmpty(m.SourcesJson))
            {
                try { citations = JsonSerializer.Deserialize<List<ChatCitationDto>>(m.SourcesJson); } catch {}
            }
            return new ChatMessageDto(m.MessageId, m.SessionId.ToString(), m.SenderType, m.Content, citations, m.CreatedAtUtc);
        }).ToList();
    }

    public async Task<bool> ClaimSessionAsync(Guid sessionId, long staffUserId, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session == null) return false;

        session.Status = "HUMAN";
        session.AssignedSupportId = staffUserId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ChatMessageDto?> StaffReplyAsync(Guid sessionId, long staffUserId, string message, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session == null) return null;

        session.Status = "HUMAN";
        session.AssignedSupportId = staffUserId;
        session.LastMessageAtUtc = DateTime.UtcNow;

        var msg = new ChatMessage
        {
            SessionId = sessionId,
            SenderType = "SUPPORT",
            SenderUserId = staffUserId,
            Content = message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync(ct);

        return new ChatMessageDto(msg.MessageId, msg.SessionId.ToString(), msg.SenderType, msg.Content, null, msg.CreatedAtUtc);
    }

    public async Task<bool> CloseSessionAsync(Guid sessionId, long staffUserId, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session == null) return false;

        session.Status = "CLOSED";
        session.ClosedAtUtc = DateTime.UtcNow;
        session.AssignedSupportId = staffUserId;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
