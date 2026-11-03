using System;
using System.Collections.Generic;
using System.Security.Claims;
using API_FixComputerCenter.Application.Features.AiChat;
using API_FixComputerCenter.Application.Features.Knowledge;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace API_FixComputerCenter.Endpoints;

public record StaffReplyRequest(string Message);

public static class AiChatEndpoints
{
    public static void MapAiChatEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. PUBLIC CHAT ENDPOINTS (For Customer Floating Widget)
        var publicChatGroup = app.MapGroup("/api/v1/chat").WithTags("AiChatPublic");

        publicChatGroup.MapPost("/message", async (CustomerChatMessageRequest req, IAiChatService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.Message))
            {
                return Results.BadRequest(new { message = "Nội dung tin nhắn không được để trống." });
            }
            var res = await svc.HandleCustomerMessageAsync(req);
            return Results.Ok(res);
        }).RequireRateLimiting("ChatRateLimit");

        publicChatGroup.MapGet("/session/{id}/messages", async (string id, IAiChatService svc) =>
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return Results.Ok(new List<ChatMessageDto>());
            }
            var msgs = await svc.GetSessionMessagesAsync(guid);
            return Results.Ok(msgs);
        });

        // 2. ADMIN KNOWLEDGE BASE ENDPOINTS
        var kbGroup = app.MapGroup("/api/v1/admin/knowledge").WithTags("KnowledgeBase");

        kbGroup.MapPost("/documents", async (CreateDocumentRequest req, IKnowledgeService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.RawContent))
            {
                return Results.BadRequest(new { message = "Tiêu đề và nội dung tài liệu không được để trống." });
            }
            var doc = await svc.IngestDocumentAsync(req);
            return Results.Created($"/api/v1/admin/knowledge/documents/{doc.DocumentId}", doc);
        }).RequireAuthorization("Permission:AI.KB.MANAGE");

        kbGroup.MapGet("/documents", async (IKnowledgeService svc) =>
        {
            var list = await svc.GetDocumentsAsync();
            return Results.Ok(list);
        }).RequireAuthorization("Permission:AI.KB.MANAGE");

        kbGroup.MapDelete("/documents/{id:long}", async (long id, IKnowledgeService svc) =>
        {
            var deleted = await svc.DeleteDocumentAsync(id);
            if (!deleted) return Results.NotFound(new { message = "Không tìm thấy tài liệu cần xóa." });
            return Results.Ok(new { message = "Xóa tài liệu thành công." });
        }).RequireAuthorization("Permission:AI.KB.MANAGE");

        // 3. SUPPORT STAFF HUMAN CHAT INBOX
        var supportGroup = app.MapGroup("/api/v1/support/chat").WithTags("SupportChat");

        supportGroup.MapGet("/sessions", async (string? status, IAiChatService svc) =>
        {
            var list = await svc.GetSupportSessionsAsync(status);
            return Results.Ok(list);
        }).RequireAuthorization("Permission:AI.CHAT.VIEW");

        supportGroup.MapGet("/sessions/{id}/messages", async (string id, IAiChatService svc) =>
        {
            if (!Guid.TryParse(id, out var guid)) return Results.BadRequest(new { message = "Invalid session id." });
            var msgs = await svc.GetSessionMessagesAsync(guid);
            return Results.Ok(msgs);
        }).RequireAuthorization("Permission:AI.CHAT.VIEW");

        supportGroup.MapPost("/sessions/{id}/claim", async (string id, HttpContext ctx, IAiChatService svc) =>
        {
            if (!Guid.TryParse(id, out var guid)) return Results.BadRequest(new { message = "Invalid session id." });

            long userId = 1;
            var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(claim, out var uid)) userId = uid;

            var ok = await svc.ClaimSessionAsync(guid, userId);
            if (!ok) return Results.NotFound(new { message = "Không tìm thấy phiên chat." });
            return Results.Ok(new { message = "Đã tiếp nhận phiên chat thành công." });
        }).RequireAuthorization("Permission:AI.CHAT.REPLY");

        supportGroup.MapPost("/sessions/{id}/reply", async (string id, StaffReplyRequest req, HttpContext ctx, IAiChatService svc) =>
        {
            if (!Guid.TryParse(id, out var guid)) return Results.BadRequest(new { message = "Invalid session id." });
            if (string.IsNullOrWhiteSpace(req.Message))
            {
                return Results.BadRequest(new { message = "Nội dung phản hồi không được để trống." });
            }

            long userId = 1;
            var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(claim, out var uid)) userId = uid;

            var msg = await svc.StaffReplyAsync(guid, userId, req.Message);
            if (msg == null) return Results.NotFound(new { message = "Không tìm thấy phiên chat." });
            return Results.Ok(msg);
        }).RequireAuthorization("Permission:AI.CHAT.REPLY");

        supportGroup.MapPost("/sessions/{id}/close", async (string id, HttpContext ctx, IAiChatService svc) =>
        {
            if (!Guid.TryParse(id, out var guid)) return Results.BadRequest(new { message = "Invalid session id." });

            long userId = 1;
            var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(claim, out var uid)) userId = uid;

            var ok = await svc.CloseSessionAsync(guid, userId);
            if (!ok) return Results.NotFound(new { message = "Không tìm thấy phiên chat." });
            return Results.Ok(new { message = "Đã kết thúc phiên chat." });
        }).RequireAuthorization("Permission:AI.CHAT.REPLY");
    }
}
