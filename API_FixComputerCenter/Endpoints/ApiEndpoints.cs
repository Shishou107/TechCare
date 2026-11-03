using API_FixComputerCenter.Application.Features.Shipping;
using System.Security.Claims;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Common.Validators;
using API_FixComputerCenter.Application.Features.Appointments;
using API_FixComputerCenter.Application.Features.Auth;
using API_FixComputerCenter.Application.Features.Customers;
using API_FixComputerCenter.Application.Features.Devices;
using API_FixComputerCenter.Application.Features.Diagnostics;
using API_FixComputerCenter.Application.Features.Inventory;
using API_FixComputerCenter.Application.Features.Quotations;
using API_FixComputerCenter.Application.Features.Repairs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using API_FixComputerCenter.Domain.Entities;

namespace API_FixComputerCenter.Endpoints;

public record GuestRatingRequest(string Phone, int Score, string? Comment);

public static class ApiEndpoints
{
    private const string RefreshTokenCookieName = "tc_refresh_token";

    public static IEndpointRouteBuilder MapTechCareEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. HEALTH CHECK (Sanitized, no ex.Message exposure)
        app.MapGet("/api/v1/health/database", async (Microsoft.Extensions.Configuration.IConfiguration config) =>
        {
            var connString = config.GetConnectionString("DefaultConnection");
            try
            {
                using var conn = new Microsoft.Data.SqlClient.SqlConnection(connString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'";
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return Results.Ok(new
                {
                    status = "HEALTHY",
                    totalTables = count,
                    checkedAtUtc = DateTime.UtcNow
                });
            }
            catch
            {
                return Results.Problem(detail: "Không thể kết nối đến cơ sở dữ liệu. Vui lòng kiểm tra dịch vụ SQL Server.", statusCode: 500, title: "Database Unhealthy");
            }
        })
        .WithName("GetDatabaseHealth")
        .WithTags("Health");

        // 2. AUTHENTICATION & SESSION MANAGEMENT
        var authGroup = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        authGroup.MapPost("/login", async (LoginRequest req, HttpContext ctx, IAuthService auth) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();
            var result = await auth.LoginAsync(req, ip, ua);

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(ctx, result.RefreshToken);
            }

            // A3: Omit refresh token from body for browser clients so JavaScript cannot access it via XSS
            var isBrowserClient = !string.Equals(ctx.Request.Headers["X-Client-Type"], "mobile", StringComparison.OrdinalIgnoreCase);
            var responseBody = isBrowserClient ? result with { RefreshToken = null } : result;

            return Results.Ok(responseBody);
        }).RequireRateLimiting("LoginRateLimit").WithValidation<LoginRequest>();

        authGroup.MapPost("/register", async (RegisterCustomerRequest req, IAuthService auth) =>
        {
            var res = await auth.RegisterCustomerAsync(req);
            return Results.Ok(res);
        }).RequireRateLimiting("LoginRateLimit");

        authGroup.MapPost("/verify-email", async (VerifyEmailRequest req, HttpContext ctx, IAuthService auth) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();
            var result = await auth.VerifyEmailAsync(req, ip, ua);

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(ctx, result.RefreshToken);
            }

            var isBrowserClient = !string.Equals(ctx.Request.Headers["X-Client-Type"], "mobile", StringComparison.OrdinalIgnoreCase);
            var responseBody = isBrowserClient ? result with { RefreshToken = null } : result;

            return Results.Ok(responseBody);
        }).RequireRateLimiting("LoginRateLimit");

        authGroup.MapPost("/resend-email-code", async (ResendVerificationEmailRequest req, IAuthService auth) =>
        {
            await auth.ResendVerificationEmailAsync(req.Email);
            return Results.Ok(new { success = true, message = "Đã gửi lại mã xác thực vào email của bạn." });
        }).RequireRateLimiting("LoginRateLimit");

        authGroup.MapPost("/forgot-password", async (ForgotPasswordRequest req, IAuthService auth) =>
        {
            await auth.ForgotPasswordAsync(req.Email);
            return Results.Ok(new { success = true, message = "Nếu email tồn tại trên hệ thống, mã xác thực đặt lại mật khẩu đã được gửi đến hộp thư của bạn." });
        }).RequireRateLimiting("LoginRateLimit");

        authGroup.MapPost("/reset-password", async (ResetPasswordRequest req, IAuthService auth) =>
        {
            await auth.ResetPasswordAsync(req);
            return Results.Ok(new { success = true, message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập bằng mật khẩu mới." });
        }).RequireRateLimiting("LoginRateLimit");

        authGroup.MapPost("/refresh", async (RefreshTokenRequest? req, HttpContext ctx, IAuthService auth) =>
        {
            // Read from cookie first, fallback to request body for non-browser clients
            var token = ctx.Request.Cookies[RefreshTokenCookieName] ?? req?.RefreshToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();
            var result = await auth.RefreshTokenAsync(token, ip, ua);

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(ctx, result.RefreshToken);
            }

            // A3: Omit refresh token from body for browser clients
            var isBrowserClient = !string.Equals(ctx.Request.Headers["X-Client-Type"], "mobile", StringComparison.OrdinalIgnoreCase);
            var responseBody = isBrowserClient ? result with { RefreshToken = null } : result;

            return Results.Ok(responseBody);
        }).RequireRateLimiting("RefreshRateLimit");

        authGroup.MapPost("/logout", async (HttpContext ctx, IAuthService auth) =>
        {
            var token = ctx.Request.Cookies[RefreshTokenCookieName];
            var sidClaim = ctx.User.FindFirst("sid")?.Value;
            Guid? sessionId = Guid.TryParse(sidClaim, out var sid) ? sid : null;

            await auth.LogoutAsync(token, sessionId);
            ctx.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/v1/auth" });
            return Results.NoContent();
        }).RequireAuthorization();

        authGroup.MapPost("/logout-all", async (HttpContext ctx, IAuthService auth, ICurrentUserService current) =>
        {
            if (current.UserId.HasValue)
            {
                await auth.LogoutAllAsync(current.UserId.Value);
            }
            ctx.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/v1/auth" });
            return Results.NoContent();
        }).RequireAuthorization();

        authGroup.MapPost("/change-password", async (ChangePasswordRequest req, IAuthService auth, ICurrentUserService current) =>
        {
            if (!current.UserId.HasValue) return Results.Unauthorized();
            await auth.ChangePasswordAsync(current.UserId.Value, req);
            return Results.Ok(new { message = "Đổi mật khẩu thành công. Các phiên đăng nhập khác đã được thu hồi." });
        }).RequireAuthorization();

        authGroup.MapGet("/sessions", async (HttpContext ctx, IAuthService auth, ICurrentUserService current) =>
        {
            if (!current.UserId.HasValue) return Results.Unauthorized();
            var sidClaim = ctx.User.FindFirst("sid")?.Value;
            Guid? sessionId = Guid.TryParse(sidClaim, out var sid) ? sid : null;

            var sessions = await auth.GetUserSessionsAsync(current.UserId.Value, sessionId);
            return Results.Ok(sessions);
        }).RequireAuthorization();

        authGroup.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, IAuthService auth, ICurrentUserService current) =>
        {
            if (!current.UserId.HasValue) return Results.Unauthorized();
            await auth.RevokeSessionAsync(current.UserId.Value, sessionId);
            return Results.NoContent();
        }).RequireAuthorization();

        authGroup.MapPost("/admin/users/{userId:long}/revoke-sessions", async (long userId, IAuthService auth) =>
        {
            await auth.AdminRevokeUserSessionsAsync(userId);
            return Results.Ok(new { message = $"Đã thu hồi toàn bộ phiên đăng nhập của người dùng {userId}." });
        }).RequireAuthorization("Permission:USER.MANAGE");

        authGroup.MapGet("/me", async (IAuthService auth) =>
        {
            var profile = await auth.GetCurrentUserProfileAsync();
            return Results.Ok(profile);
        }).RequireAuthorization();

        // 3. CUSTOMERS (Protected - Granular permissions)
        var custGroup = app.MapGroup("/api/v1/customers").WithTags("Customers");
        custGroup.MapGet("/", async (int? page, int? pageSize, string? search, ICustomerService svc) =>
        {
            var result = await svc.GetCustomersAsync(page ?? 1, pageSize ?? 20, search);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:CUSTOMER.READ");

        custGroup.MapGet("/{id:long}", async (long id, ICustomerService svc) =>
        {
            var result = await svc.GetByIdAsync(id);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:CUSTOMER.READ");

        custGroup.MapPost("/", async (CreateCustomerRequest req, ICustomerService svc) =>
        {
            var result = await svc.CreateAsync(req);
            return Results.Created($"/api/v1/customers/{result.CustomerId}", result);
        }).RequireAuthorization("Permission:CUSTOMER.WRITE").WithValidation<CreateCustomerRequest>();

        // 4. DEVICES (Protected - Granular permissions)
        var devGroup = app.MapGroup("/api/v1/devices").WithTags("Devices");
        devGroup.MapGet("/customer/{customerId:long}", async (long customerId, IDeviceService svc) =>
        {
            var result = await svc.GetCustomerDevicesAsync(customerId);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:DEVICE.READ");

        devGroup.MapPost("/", async (RegisterDeviceRequest req, IDeviceService svc) =>
        {
            var result = await svc.RegisterDeviceAsync(req);
            return Results.Created($"/api/v1/devices/{result.DeviceId}", result);
        }).RequireAuthorization("Permission:DEVICE.WRITE");

        // 5. APPOINTMENTS
        var apptGroup = app.MapGroup("/api/v1/appointments").WithTags("Appointments");

        // Public booking for customers on website
        apptGroup.MapPost("/book", async (BookAppointmentRequest req, IAppointmentService svc) =>
        {
            var result = await svc.BookAppointmentAsync(req);
            return Results.Ok(new
            {
                success = true,
                appointmentCode = result.AppointmentCode,
                message = "Đặt lịch sửa chữa thành công!",
                data = result
            });
        }).WithValidation<BookAppointmentRequest>();

        apptGroup.MapPost("/submit", async (BookAppointmentRequest req, IAppointmentService svc) =>
        {
            var result = await svc.BookAppointmentAsync(req);
            return Results.Ok(new
            {
                success = true,
                appointmentCode = result.AppointmentCode,
                message = "Đặt lịch sửa chữa thành công!",
                data = result
            });
        }).WithValidation<BookAppointmentRequest>();

        // A6: Public tracking - requires both Code and Phone with masking, includes real courier shipment
        apptGroup.MapGet("/track", async (string? code, string? phone, string? keyword, IAppointmentService apptSvc, IRepairService repairSvc, IShippingService shippingSvc) =>
        {
            var lookupCode = code;
            var lookupPhone = phone;

            if (string.IsNullOrWhiteSpace(lookupCode) && !string.IsNullOrWhiteSpace(keyword) && keyword.Contains('|'))
            {
                var parts = keyword.Split('|', 2);
                lookupCode = parts[0].Trim();
                lookupPhone = parts[1].Trim();
            }

            if (string.IsNullOrWhiteSpace(lookupCode) || string.IsNullOrWhiteSpace(lookupPhone))
            {
                return Results.BadRequest(new { message = "Vui lòng cung cấp cả Mã tra cứu (Mã lịch hẹn/Mã phiếu) và Số điện thoại đăng ký." });
            }

            var appt = await apptSvc.TrackAppointmentAsync(lookupCode, lookupPhone);
            if (appt != null)
            {
                var shipment = await shippingSvc.GetShipmentTrackingAsync(appt.AppointmentId, null);
                return Results.Ok(new { found = true, type = "APPOINTMENT", data = appt, shipment });
            }

            var ticket = await repairSvc.TrackTicketAsync(lookupCode, lookupPhone);
            if (ticket != null)
            {
                var shipment = await shippingSvc.GetShipmentTrackingAsync(null, ticket.RepairTicketId);
                return Results.Ok(new { found = true, type = "REPAIR_TICKET", data = ticket, shipment });
            }

            return Results.Ok(new { found = false, message = "Không tìm thấy thông tin lịch hẹn hoặc phiếu sửa chữa phù hợp với mã và số điện thoại đã cung cấp." });
        }).RequireRateLimiting("TrackingRateLimit");

        // Administrative query (Protected)
        apptGroup.MapGet("/", async (int? page, int? pageSize, string? status, IAppointmentService svc) =>
        {
            var result = await svc.GetAppointmentsAsync(page ?? 1, pageSize ?? 20, status);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:APPOINTMENT.MANAGE");

        // 6. REPAIR TICKETS (Protected - Granular permissions)
        var repairGroup = app.MapGroup("/api/v1/repairs").WithTags("Repairs");
        repairGroup.MapGet("/", async (int? page, int? pageSize, string? status, IRepairService svc) =>
        {
            var result = await svc.GetTicketsAsync(page ?? 1, pageSize ?? 20, status);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:REPAIR.READ");

        repairGroup.MapGet("/{id:long}", async (long id, IRepairService svc) =>
        {
            var result = await svc.GetByIdAsync(id);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:REPAIR.READ");

        repairGroup.MapPost("/", async (CreateRepairTicketRequest req, IRepairService svc) =>
        {
            var result = await svc.CreateTicketAsync(req);
            return Results.Created($"/api/v1/repairs/{result.RepairTicketId}", result);
        }).RequireAuthorization("Permission:REPAIR.INTAKE").WithValidation<CreateRepairTicketRequest>();

        repairGroup.MapPost("/{id:long}/assign", async (long id, AssignTechnicianRequest req, IRepairService svc) =>
        {
            var result = await svc.AssignTechnicianAsync(id, req);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:REPAIR.ASSIGN");

        repairGroup.MapPost("/{id:long}/status", async (long id, UpdateRepairStatusRequest req, IRepairService svc) =>
        {
            var result = await svc.UpdateStatusAsync(id, req);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:REPAIR.UPDATE_STATUS").WithValidation<UpdateRepairStatusRequest>();

        // 7. DIAGNOSTICS & QUICK-SYNC
        var diagGroup = app.MapGroup("/api/v1/diagnostics").WithTags("Diagnostics");

        diagGroup.MapPost("/quick-sync/{sessionCode}", (string sessionCode, DiagnosticScanPayload payload, IDiagnosticService svc) =>
        {
            svc.SaveQuickSyncPayload(sessionCode, payload);
            return Results.Ok(new { success = true, sessionCode, message = "Cấu hình đã được đồng bộ!" });
        });

        diagGroup.MapGet("/quick-sync/{sessionCode}", (string sessionCode, IDiagnosticService svc) =>
        {
            var payload = svc.GetQuickSyncPayload(sessionCode);
            if (payload != null)
            {
                return Results.Ok(new { synced = true, data = payload });
            }
            return Results.Ok(new { synced = false, message = "Đang chờ khách chạy script quét cấu hình..." });
        });

        diagGroup.MapGet("/download-script/{sessionCode}", (string sessionCode, HttpContext ctx, IDiagnosticService svc) =>
        {
            var host = ctx.Request.Scheme + "://" + ctx.Request.Host.Value;
            var bytes = svc.GenerateScanScriptBytes(sessionCode, host);
            return Results.File(bytes, "application/bat", $"TechCare-Scan-{sessionCode}.bat");
        });

        diagGroup.MapPost("/runs", async (SaveDiagnosticRunRequest req, IDiagnosticService svc) =>
        {
            var runId = await svc.SaveDiagnosticRunAsync(req);
            return Results.Ok(new { success = true, diagnosticRunId = runId });
        }).RequireAuthorization("Permission:REPAIR.DIAGNOSE");

        // 8. QUOTATIONS (Protected - Granular permissions)
        var quoteGroup = app.MapGroup("/api/v1/quotations").WithTags("Quotations");
                repairGroup.MapPost("/{ticketId:long}/rating", async (long ticketId, GuestRatingRequest req, ITechCareDbContext db) =>
        {
            if (req.Score < 1 || req.Score > 5)
                return Results.BadRequest(new { message = "Điểm đánh giá phải từ 1 đến 5 sao." });

            if (string.IsNullOrWhiteSpace(req.Phone))
                return Results.BadRequest(new { message = "Vui lòng cung cấp số điện thoại chủ phiếu." });

            var cleanPhone = req.Phone.Trim();
            var ticket = await db.RepairTickets
                .Include(t => t.Customer)
                .Include(t => t.TicketRating)
                .FirstOrDefaultAsync(t => t.RepairTicketId == ticketId);

            if (ticket == null || ticket.Customer == null)
                return Results.NotFound(new { message = "Không tìm thấy phiếu sửa chữa." });

            if (ticket.Customer.Phone != cleanPhone)
                return Results.Forbid();

            if (ticket.Status != "COMPLETED" && ticket.Status != "RETURNED")
                return Results.BadRequest(new { message = "Chỉ có thể đánh giá phiếu sửa chữa khi máy đã hoàn thành hoặc đã trả về." });

            if (!ticket.AssignedTechnicianUserId.HasValue)
                return Results.BadRequest(new { message = "Phiếu sửa chữa chưa được phân công kỹ thuật viên." });

            if (ticket.TicketRating != null || await db.TicketRatings.AnyAsync(r => r.RepairTicketId == ticketId))
                return Results.BadRequest(new { message = "Phiếu sửa chữa này đã được đánh giá trước đó." });

            var rating = new TicketRating
            {
                RepairTicketId = ticketId,
                CustomerId = ticket.CustomerId,
                TechnicianUserId = ticket.AssignedTechnicianUserId.Value,
                Score = req.Score,
                Comment = req.Comment?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            db.TicketRatings.Add(rating);
            await db.SaveChangesAsync();

            return Results.Ok(new { success = true, ratingId = rating.RatingId, message = "Cảm ơn bạn đã gửi đánh giá!" });
        });

        quoteGroup.MapGet("/{id:long}", async (long id, IQuotationService svc) =>
        {
            var result = await svc.GetByIdAsync(id);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:REPAIR.READ");

        quoteGroup.MapPost("/repairs/{ticketId:long}", async (long ticketId, CreateQuotationRequest req, IQuotationService svc) =>
        {
            var result = await svc.CreateQuotationAsync(req with { RepairTicketId = ticketId });
            return Results.Created($"/api/v1/quotations/{result.QuotationId}", result);
        }).RequireAuthorization("Permission:QUOTE.MANAGE").WithValidation<CreateQuotationRequest>();

        quoteGroup.MapPost("/{id:long}/decision", async (long id, QuotationApprovalRequest req, IQuotationService svc) =>
        {
            var result = await svc.ApproveOrRejectAsync(id, req);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:QUOTE.APPROVE");

        // 9. INVENTORY (Protected - Granular permissions)
        var invGroup = app.MapGroup("/api/v1/inventory").WithTags("Inventory");
        invGroup.MapGet("/stock/{branchId:int}", async (int branchId, IInventoryService svc) =>
        {
            var result = await svc.GetBranchPartsAsync(branchId);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:INVENTORY.READ");

        invGroup.MapPost("/issue", async (IssuePartRequest req, IInventoryService svc) =>
        {
            await svc.IssuePartToTicketAsync(req);
            return Results.Ok(new { success = true, message = "Xuất linh kiện thành công!" });
        }).RequireAuthorization("Permission:INVENTORY.TRANSACT").WithValidation<IssuePartRequest>();

        // Map Shipping & Webhook Endpoints
        app.MapShippingEndpoints();
        app.MapAiChatEndpoints();
        app.MapCustomerEndpoints();
        app.MapTechnicianEndpoints();

        app.MapGet("/api/v1/integrations/status", (Microsoft.Extensions.Configuration.IConfiguration config) =>
        {
            var ghnToken = config["TECHCARE_GHN_TOKEN"] ?? config["Shipping:Ghn:Token"];
            var aiKey = config["TECHCARE_AI_API_KEY"] ?? config["Ai:ApiKey"];
            var smtpHost = config["TECHCARE_SMTP_HOST"] ?? config["Email:Smtp:Host"];

            return Results.Ok(new
            {
                shipping = new
                {
                    provider = "Giao Hàng Nhanh (GHN)",
                    isConfigured = !string.IsNullOrWhiteSpace(ghnToken)
                },
                ai = new
                {
                    provider = "Meta / OpenAI Compatible",
                    isConfigured = !string.IsNullOrWhiteSpace(aiKey)
                },
                email = new
                {
                    provider = "Email (SMTP)",
                    isConfigured = !string.IsNullOrWhiteSpace(smtpHost),
                    mode = !string.IsNullOrWhiteSpace(smtpHost) ? "SMTP_SERVER" : "LOG_ONLY_DEV"
                }
            });
        }).RequireAuthorization("Permission:USER.MANAGE");
        return app;
    }

    private static void SetRefreshTokenCookie(HttpContext ctx, string refreshToken)
    {
        ctx.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        });
    }
}
