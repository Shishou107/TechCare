using System.Text;
using System.Threading.RateLimiting;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Features.Appointments;
using API_FixComputerCenter.Application.Features.Auth;
using API_FixComputerCenter.Application.Features.Customers;
using API_FixComputerCenter.Application.Features.Devices;
using API_FixComputerCenter.Application.Features.Diagnostics;
using API_FixComputerCenter.Application.Features.Inventory;
using API_FixComputerCenter.Application.Features.Quotations;
using API_FixComputerCenter.Application.Features.Repairs;
using API_FixComputerCenter.Application.Features.Shipping;
using API_FixComputerCenter.Infrastructure.Persistence;
using API_FixComputerCenter.Infrastructure.Services;
using API_FixComputerCenter.Security.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace API_FixComputerCenter;

public static class DependencyInjection
{
    public static IServiceCollection AddTechCareBackend(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. HttpContext & Persistence
        services.AddHttpContextAccessor();
        var connString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=TechcareWed;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        services.AddDbContext<TechCareDbContext>(options =>
        {
            options.UseSqlServer(connString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });
        });
        services.AddScoped<ITechCareDbContext>(sp => sp.GetRequiredService<TechCareDbContext>());

        // 2. Application Services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IRepairService, RepairService>();
        services.AddScoped<IDiagnosticService, DiagnosticService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddHttpClient();
        services.Configure<GhnOptions>(configuration.GetSection("Ghn"));
        services.AddScoped<IShippingProvider, GhnShippingProvider>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddHostedService<ShipmentStatusSyncService>();

        // 3. FluentValidation & MemoryCache
        services.AddMemoryCache();
        services.AddValidatorsFromAssemblyContaining<CreateCustomerValidator>();
        services.AddHostedService<SessionCleanupBackgroundService>();

        // 4. Authentication (JWT Bearer)
        var rawKey = configuration["Jwt:Key"];
        var jwtKey = !string.IsNullOrWhiteSpace(rawKey) ? rawKey : (Environment.GetEnvironmentVariable("TECHCARE_JWT_SECRET") ?? Environment.GetEnvironmentVariable("Jwt__Key"));
        if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Cấu hình 'Jwt:Key' không tồn tại! Vui lòng thiết lập biến môi trường 'TECHCARE_JWT_SECRET' hoặc .NET User-Secrets (tối thiểu 32 ký tự).");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // Set to true in Production with TLS
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "TechCareAPI",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "TechCareWeb",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<ITechCareDbContext>();
                    var cache = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                    var principal = context.Principal;
                    if (principal == null)
                    {
                        context.Fail("Unauthorized");
                        return;
                    }

                    var userIdStr = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                    var sidStr = principal.FindFirst("sid")?.Value;
                    var stamp = principal.FindFirst("stamp")?.Value;

                    if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out var userId))
                    {
                        context.Fail("Missing or invalid user id in token");
                        return;
                    }

                    // Cache user stamp and active status for 60 seconds to avoid querying DB on every request
                    var cacheKey = $"user_sec_{userId}";
                    if (!cache.TryGetValue(cacheKey, out (bool IsValid, string? Stamp, DateTime? Lockout) cachedUser))
                    {
                        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                        if (user == null || !user.IsActive || user.DeletedAtUtc != null)
                        {
                            cachedUser = (false, null, null);
                        }
                        else
                        {
                            cachedUser = (true, user.SecurityStamp, user.LockoutEndUtc);
                        }
                        cache.Set(cacheKey, cachedUser, TimeSpan.FromSeconds(60));
                    }

                    if (!cachedUser.IsValid)
                    {
                        context.Fail("Tài khoản không tồn tại, bị vô hiệu hóa hoặc đã xóa.");
                        return;
                    }

                    if (cachedUser.Lockout.HasValue && cachedUser.Lockout.Value > DateTime.UtcNow)
                    {
                        context.Fail("Tài khoản đang bị tạm khóa.");
                        return;
                    }

                    if (!string.IsNullOrEmpty(stamp) && cachedUser.Stamp != stamp)
                    {
                        context.Fail("Phiên bảo mật đã thay đổi (SecurityStamp mismatch). Vui lòng đăng nhập lại.");
                        return;
                    }

                    if (!string.IsNullOrEmpty(sidStr) && Guid.TryParse(sidStr, out var sessionId))
                    {
                        var sessionCacheKey = $"session_active_{sessionId}";
                        if (!cache.TryGetValue(sessionCacheKey, out bool sessionActive))
                        {
                            var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.SessionId == sessionId);
                            sessionActive = session != null && session.RevokedAtUtc == null && session.AbsoluteExpiresAtUtc > DateTime.UtcNow;
                            cache.Set(sessionCacheKey, sessionActive, TimeSpan.FromSeconds(60));
                        }

                        if (!sessionActive)
                        {
                            context.Fail("Phiên đăng nhập (Session) đã bị thu hồi hoặc hết hạn.");
                            return;
                        }
                    }
                }
            };
        });

        // 5. Authorization
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole("ADMIN"));
            options.AddPolicy("RequireTechnician", policy => policy.RequireRole("ADMIN", "TECHNICIAN"));
            options.AddPolicy("RequireInventory", policy => policy.RequireRole("ADMIN", "INVENTORY"));

            // Granular permission policies mapped to SQL Security.Permissions
            var permissions = new[]
            {
                "CUSTOMER.READ", "CUSTOMER.WRITE",
                "DEVICE.READ", "DEVICE.WRITE",
                "APPOINTMENT.MANAGE",
                "REPAIR.READ", "REPAIR.INTAKE", "REPAIR.ASSIGN", "REPAIR.DIAGNOSE", "REPAIR.UPDATE_STATUS",
                "QUOTE.MANAGE", "QUOTE.APPROVE",
                "INVENTORY.READ", "INVENTORY.TRANSACT",
                "BILLING.READ", "BILLING.COLLECT",
                "WARRANTY.MANAGE", "REPORT.READ",
                "USER.MANAGE", "ROLE.MANAGE", "AUDIT.READ",
                "SHIPPING.VIEW", "SHIPPING.CREATE", "SHIPPING.MANAGE",
                "AI.KB.MANAGE", "AI.CHAT.VIEW", "AI.CHAT.REPLY"
            };

            foreach (var perm in permissions)
            {
                options.AddPolicy($"Permission:{perm}", policy => policy.AddRequirements(new PermissionRequirement(perm)));
            }
        });
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

        // 6. Rate Limiting (OWASP recommendation)
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Strict rate limit for login attempts (5 attempts per minute per IP)
            options.AddPolicy("LoginRateLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Refresh token rate limit (30 attempts per 5 minutes per IP)
            options.AddPolicy("RefreshRateLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(5),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Public tracking rate limit (20 queries per minute per IP to prevent harvesting)
            options.AddPolicy("TrackingRateLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Public Shipping fee rate limit (30 queries per min)
            options.AddPolicy("ShippingFeeRateLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Public AI Chat rate limit (20 messages per min)
            options.AddPolicy("ChatRateLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        // 7. CORS
        services.AddCors(options =>
        {
            options.AddPolicy("TechCareCorsPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
