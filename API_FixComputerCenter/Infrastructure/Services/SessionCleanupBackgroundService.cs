using API_FixComputerCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Infrastructure.Services;

public class SessionCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public SessionCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<SessionCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionCleanupBackgroundService đã khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện dọn dẹp phiên và token hết hạn.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task PerformCleanupAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ITechCareDbContext>();
        var threshold = DateTime.UtcNow.AddDays(-30);

        // 1. Clean expired/revoked refresh tokens older than 30 days (Batch 1000)
        var deletedTokens = await db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < threshold || (t.RevokedAtUtc != null && t.RevokedAtUtc < threshold))
            .Take(1000)
            .ExecuteDeleteAsync(ct);

        // 2. Clean revoked/expired sessions older than 30 days
        var deletedSessions = await db.Sessions
            .Where(s => s.AbsoluteExpiresAtUtc < threshold || (s.RevokedAtUtc != null && s.RevokedAtUtc < threshold))
            .Take(1000)
            .ExecuteDeleteAsync(ct);

        if (deletedTokens > 0 || deletedSessions > 0)
        {
            _logger.LogInformation("Dọn dẹp hoàn tất: Đã xóa {TokenCount} refresh token và {SessionCount} session quá hạn.", deletedTokens, deletedSessions);
        }
    }
}

