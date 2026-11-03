using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Features.Shipping;
using API_FixComputerCenter.Domain.Entities.Shipping;
using API_FixComputerCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class FakeShippingProvider : IShippingProvider
{
    public string CarrierCode => "GHN";
    public int CallCount { get; private set; }

    public Task<ShippingFeePreviewResult> PreviewFeeAsync(ShippingFeePreviewRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new ShippingFeePreviewResult { FeeAmount = 35000 });
    }

    public Task<CreateShipmentResult> CreateOrderAsync(CreateShipmentRequest request, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(new CreateShipmentResult
        {
            Success = true,
            CarrierCode = "GHN",
            WaybillCode = "GHN-MOCK-" + Guid.NewGuid().ToString("N")[..8],
            InitialStatus = "CREATED",
            FeeAmount = 35000,
            ExpectedDeliveryAtUtc = DateTime.UtcNow.AddDays(2)
        });
    }

    public Task<CancelShipmentResult> CancelOrderAsync(string waybillCode, CancellationToken ct = default)
    {
        return Task.FromResult(new CancelShipmentResult { Success = true });
    }

    public Task<string> GetPrintUrlAsync(string waybillCode, CancellationToken ct = default)
    {
        return Task.FromResult($"https://print.ghn.vn/order/{waybillCode}");
    }

    public string MapProviderStatus(string providerStatus)
    {
        return providerStatus.ToLowerInvariant() switch
        {
            "delivering" => "IN_TRANSIT",
            "delivered" => "DELIVERED",
            _ => "IN_TRANSIT"
        };
    }

    public Task<IReadOnlyList<GhnLocationItem>> GetProvincesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<GhnLocationItem>>(new List<GhnLocationItem>());
    }

    public Task<IReadOnlyList<GhnLocationItem>> GetDistrictsAsync(int provinceId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<GhnLocationItem>>(new List<GhnLocationItem>());
    }

    public Task<IReadOnlyList<GhnLocationItem>> GetWardsAsync(int districtId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<GhnLocationItem>>(new List<GhnLocationItem>());
    }
}

public class FakeHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new HttpClient();
}

public class ShippingTests
{
    private TechCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TechCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new TechCareDbContext(options);
    }

    // TEST 1 (Shipping): Webhook deduplication
    [Fact]
    public async Task Webhook_DuplicateSubmission_DoesNotCreateDuplicateEvents()
    {
        using var db = CreateDbContext("Test_Webhook_Deduplication_" + Guid.NewGuid());
        var provider = new FakeShippingProvider();
        var config = new ConfigurationBuilder().Build();
        var service = new ShippingService(db, provider, NullLogger<ShippingService>.Instance, config, new FakeHttpClientFactory());

        // 1. Seed shipment
        var shipment = new Shipment
        {
            CarrierCode = "GHN",
            WaybillCode = "GHN-DUP-TEST-001",
            Direction = "INBOUND",
            AppointmentId = 101,
            StatusCode = "CREATED",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        var fixedTime = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
        var webhookPayload = new GhnWebhookPayload
        {
            OrderCode = "GHN-DUP-TEST-001",
            Status = "delivering",
            Warehouse = "Kho Hà Nội Hub",
            Description = "Bưu tá đang giao hàng",
            Time = fixedTime,
            TotalFee = 35000
        };

        // 2. First webhook call
        var firstResult = await service.HandleGhnWebhookAsync(webhookPayload, null);
        Assert.True(firstResult);

        // 3. Second identical webhook call (retry from carrier)
        var secondResult = await service.HandleGhnWebhookAsync(webhookPayload, null);
        Assert.True(secondResult);

        // 4. Assert: Exactly 1 event recorded
        var events = await db.ShipmentEvents
            .Where(e => e.ShipmentId == shipment.ShipmentId)
            .ToListAsync();

        Assert.Single(events);
        Assert.Equal("delivering", events[0].ProviderStatus);
        Assert.Equal("IN_TRANSIT", events[0].InternalStatus);
    }

    // TEST 2 (Shipping): Order creation idempotency
    [Fact]
    public async Task CreateOrder_CalledTwiceForSameAppointment_ReturnsExistingOrderWithoutCallingCarrierAgain()
    {
        using var db = CreateDbContext("Test_Order_Idempotency_" + Guid.NewGuid());
        var provider = new FakeShippingProvider();
        var config = new ConfigurationBuilder().Build();
        var service = new ShippingService(db, provider, NullLogger<ShippingService>.Instance, config, new FakeHttpClientFactory());

        var request = new CreateShipmentRequest
        {
            AppointmentId = 555,
            Direction = "INBOUND",
            SenderName = "Nguyễn Văn Test",
            SenderPhone = "0987654321",
            SenderAddress = "123 Cầu Giấy, Hà Nội",
            SenderProvinceCode = "01",
            SenderDistrictCode = "005",
            SenderWardCode = "001",
            ReceiverName = "TechCare Center",
            ReceiverPhone = "19001234",
            ReceiverAddress = "Hoàn Kiếm, Hà Nội",
            ReceiverProvinceCode = "01",
            ReceiverDistrictCode = "001",
            ReceiverWardCode = "001",
            WeightGram = 1500
        };

        // First call: Should invoke carrier provider
        var res1 = await service.CreateOrderAsync(request, createdByUserId: 1);
        Assert.True(res1.Success);
        Assert.Equal(1, provider.CallCount);
        var originalWaybill = res1.WaybillCode;
        Assert.False(string.IsNullOrEmpty(originalWaybill));

        // Second call with same AppointmentId: Should hit idempotency check
        var res2 = await service.CreateOrderAsync(request, createdByUserId: 1);
        Assert.True(res2.Success);
        Assert.Equal(1, provider.CallCount); // Provider was NOT called a second time!
        Assert.Equal(originalWaybill, res2.WaybillCode);
    }
}
