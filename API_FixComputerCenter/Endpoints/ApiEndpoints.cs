using System.Security.Claims;
using API_FixComputerCenter.Application.Common.Interfaces;
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

namespace API_FixComputerCenter.Endpoints;

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

            return Results.Ok(result);
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

            return Results.Ok(result);
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
        }).RequireAuthorization("RequireAdmin");

        authGroup.MapGet("/me", async (IAuthService auth) =>
        {
            var profile = await auth.GetCurrentUserProfileAsync();
            return Results.Ok(profile);
        }).RequireAuthorization();

        // 3. CUSTOMERS (Protected - Requires Auth)
        var custGroup = app.MapGroup("/api/v1/customers").WithTags("Customers").RequireAuthorization();
        custGroup.MapGet("/", async (int? page, int? pageSize, string? search, ICustomerService svc) =>
        {
            var result = await svc.GetCustomersAsync(page ?? 1, pageSize ?? 20, search);
            return Results.Ok(result);
        });

        custGroup.MapGet("/{id:long}", async (long id, ICustomerService svc) =>
        {
            var result = await svc.GetByIdAsync(id);
            return Results.Ok(result);
        });

        custGroup.MapPost("/", async (CreateCustomerRequest req, ICustomerService svc) =>
        {
            var result = await svc.CreateAsync(req);
            return Results.Created($"/api/v1/customers/{result.CustomerId}", result);
        });

        // 4. DEVICES (Protected - Requires Auth)
        var devGroup = app.MapGroup("/api/v1/devices").WithTags("Devices").RequireAuthorization();
        devGroup.MapGet("/customer/{customerId:long}", async (long customerId, IDeviceService svc) =>
        {
            var result = await svc.GetCustomerDevicesAsync(customerId);
            return Results.Ok(result);
        });

        devGroup.MapPost("/", async (RegisterDeviceRequest req, IDeviceService svc) =>
        {
            var result = await svc.RegisterDeviceAsync(req);
            return Results.Created($"/api/v1/devices/{result.DeviceId}", result);
        });

        // 5. APPOINTMENTS
        var apptGroup = app.MapGroup("/api/v1/appointments").WithTags("Appointments");

        // Public booking for customers on website
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
        });

        // Public tracking: safe query for customer's own appointment/ticket without leaking all records
        apptGroup.MapGet("/track", async (string? keyword, IAppointmentService apptSvc, IRepairService repairSvc) =>
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Results.BadRequest(new { message = "Vui lòng cung cấp mã lịch hẹn, mã phiếu hoặc số điện thoại." });
            }

            var appt = await apptSvc.TrackAppointmentAsync(keyword);
            if (appt != null)
            {
                return Results.Ok(new { found = true, type = "APPOINTMENT", data = appt });
            }

            return Results.Ok(new { found = false, message = "Không tìm thấy thông tin lịch hẹn phù hợp." });
        });

        // Administrative query (Protected)
        apptGroup.MapGet("/", async (int? page, int? pageSize, string? status, IAppointmentService svc) =>
        {
            var result = await svc.GetAppointmentsAsync(page ?? 1, pageSize ?? 20, status);
            return Results.Ok(result);
        }).RequireAuthorization();

        // 6. REPAIR TICKETS (Protected - Requires Auth)
        var repairGroup = app.MapGroup("/api/v1/repairs").WithTags("Repairs").RequireAuthorization();
        repairGroup.MapGet("/", async (int? page, int? pageSize, string? status, IRepairService svc) =>
        {
            var result = await svc.GetTicketsAsync(page ?? 1, pageSize ?? 20, status);
            return Results.Ok(result);
        });

        repairGroup.MapGet("/{id:long}", async (long id, IRepairService svc) =>
        {
            var result = await svc.GetByIdAsync(id);
            return Results.Ok(result);
        });

        repairGroup.MapPost("/", async (CreateRepairTicketRequest req, IRepairService svc) =>
        {
            var result = await svc.CreateTicketAsync(req);
            return Results.Created($"/api/v1/repairs/{result.RepairTicketId}", result);
        });

        repairGroup.MapPost("/{id:long}/assign", async (long id, AssignTechnicianRequest req, IRepairService svc) =>
        {
            var result = await svc.AssignTechnicianAsync(id, req);
            return Results.Ok(result);
        });

        repairGroup.MapPost("/{id:long}/status", async (long id, UpdateRepairStatusRequest req, IRepairService svc) =>
        {
            var result = await svc.UpdateStatusAsync(id, req);
            return Results.Ok(result);
        });

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
        }).RequireAuthorization();

        // 8. QUOTATIONS (Protected - Requires Auth)
        var quoteGroup = app.MapGroup("/api/v1/quotations").WithTags("Quotations").RequireAuthorization();
        quoteGroup.MapGet("/{id:long}", async (long id, IQuotationService svc) =>
        {
            var result = await svc.GetByIdAsync(id);
            return Results.Ok(result);
        });

        quoteGroup.MapPost("/repairs/{ticketId:long}", async (long ticketId, CreateQuotationRequest req, IQuotationService svc) =>
        {
            var result = await svc.CreateQuotationAsync(req with { RepairTicketId = ticketId });
            return Results.Created($"/api/v1/quotations/{result.QuotationId}", result);
        });

        quoteGroup.MapPost("/{id:long}/decision", async (long id, QuotationApprovalRequest req, IQuotationService svc) =>
        {
            var result = await svc.ApproveOrRejectAsync(id, req);
            return Results.Ok(result);
        });

        // 9. INVENTORY (Protected - Requires Auth)
        var invGroup = app.MapGroup("/api/v1/inventory").WithTags("Inventory").RequireAuthorization();
        invGroup.MapGet("/stock/{branchId:int}", async (int branchId, IInventoryService svc) =>
        {
            var result = await svc.GetBranchPartsAsync(branchId);
            return Results.Ok(result);
        });

        invGroup.MapPost("/issue", async (IssuePartRequest req, IInventoryService svc) =>
        {
            await svc.IssuePartToTicketAsync(req);
            return Results.Ok(new { success = true, message = "Xuất linh kiện thành công!" });
        });

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
