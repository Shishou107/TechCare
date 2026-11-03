using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Application.Features.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Infrastructure.Services;

public class ShipmentStatusSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShipmentStatusSyncService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(15);

    public ShipmentStatusSyncService(
        IServiceProvider serviceProvider,
        ILogger<ShipmentStatusSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ShipmentStatusSyncService background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncActiveShipmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShipmentStatusSyncService execution loop.");
            }

            await Task.Delay(_period, stoppingToken);
        }
    }

    private async Task SyncActiveShipmentsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ITechCareDbContext>();
        var shippingService = scope.ServiceProvider.GetRequiredService<IShippingService>();

        // Query shipments that are not in final states: DELIVERED, RETURNED, CANCELLED
        var finalStatuses = new[] { "DELIVERED", "RETURNED", "CANCELLED" };
        var activeShipments = await db.Shipments
            .Where(s => !finalStatuses.Contains(s.StatusCode))
            .OrderBy(s => s.UpdatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        if (activeShipments.Count == 0) return;

        _logger.LogInformation("Syncing {Count} active shipments with carrier.", activeShipments.Count);
        // Background sync checks can poll or update heartbeat
        foreach (var s in activeShipments)
        {
            s.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
