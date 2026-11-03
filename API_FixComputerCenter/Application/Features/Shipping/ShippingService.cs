using API_FixComputerCenter.Infrastructure.Shipping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Application.Features.Shipping;

public class GhnWebhookPayload
{
    public string? OrderCode { get; set; }
    public string? ClientOrderCode { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
    public string? Warehouse { get; set; }
    public decimal? TotalFee { get; set; }
    public DateTime? Time { get; set; }
    public string? RawJson { get; set; }
}

public class ShipmentTrackingDto
{
    public long ShipmentId { get; set; }
    public string CarrierCode { get; set; } = "GHN";
    public string WaybillCode { get; set; } = string.Empty;
    public string Direction { get; set; } = "INBOUND";
    public string StatusCode { get; set; } = "CREATED";
    public string StatusName { get; set; } = "Đã tạo vận đơn";
    public decimal? FeeAmount { get; set; }
    public DateTime? ExpectedDeliveryAtUtc { get; set; }
    public decimal? SenderLatitude { get; set; }
    public decimal? SenderLongitude { get; set; }
    public decimal? ReceiverLatitude { get; set; }
    public decimal? ReceiverLongitude { get; set; }
    public List<ShipmentEventDto> Events { get; set; } = new();
}

public class ShipmentEventDto
{
    public string ProviderStatus { get; set; } = string.Empty;
    public string InternalStatus { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LocationText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public interface IShippingService
{
    Task<ShippingFeePreviewResult> PreviewFeeAsync(ShippingFeePreviewRequest request, CancellationToken ct = default);
    Task<CreateShipmentResult> CreateOrderAsync(CreateShipmentRequest request, long createdByUserId, CancellationToken ct = default);
    Task<CancelShipmentResult> CancelOrderAsync(long shipmentId, long userId, CancellationToken ct = default);
    Task<string> GetPrintUrlAsync(long shipmentId, CancellationToken ct = default);
    Task<bool> HandleGhnWebhookAsync(GhnWebhookPayload payload, string? secretHeader, CancellationToken ct = default);
    Task<ShipmentTrackingDto?> GetShipmentTrackingAsync(long? appointmentId, long? repairTicketId, CancellationToken ct = default);
}

public class ShippingService : IShippingService
{
    private readonly ITechCareDbContext _db;
    private readonly IShippingProvider _provider;
    private readonly ILogger<ShippingService> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public ShippingService(
        ITechCareDbContext db,
        IShippingProvider provider,
        ILogger<ShippingService> logger,
        IConfiguration config,
        IHttpClientFactory httpFactory)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
        _config = config;
        _http = httpFactory.CreateClient();
    }

    public Task<ShippingFeePreviewResult> PreviewFeeAsync(ShippingFeePreviewRequest request, CancellationToken ct = default)
    {
        return _provider.PreviewFeeAsync(request, ct);
    }

    public async Task<CreateShipmentResult> CreateOrderAsync(CreateShipmentRequest request, long createdByUserId, CancellationToken ct = default)
    {
        // 1. Idempotency Check: if a shipment already exists for this appointment or repair ticket, return it immediately
        var existing = await _db.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                (request.AppointmentId.HasValue && s.AppointmentId == request.AppointmentId) ||
                (request.RepairTicketId.HasValue && s.RepairTicketId == request.RepairTicketId),
                ct);

        if (existing != null)
        {
            _logger.LogInformation("Idempotent hit: Shipment already exists with Waybill {WaybillCode}", existing.WaybillCode);
            return new CreateShipmentResult
            {
                Success = true,
                CarrierCode = existing.CarrierCode,
                WaybillCode = existing.WaybillCode,
                InitialStatus = existing.StatusCode,
                FeeAmount = existing.FeeAmount,
                ExpectedDeliveryAtUtc = existing.ExpectedDeliveryAtUtc
            };
        }

        // 2. Call Carrier Provider
        var providerResult = await _provider.CreateOrderAsync(request, ct);
        if (!providerResult.Success)
        {
            return providerResult;
        }

        // Determine branch and customer coordinates (R2: no hardcoded fallback, log warning if missing)
        var branchLat = _config.GetValue<decimal?>("Shipping:BranchLatitude");
        var branchLng = _config.GetValue<decimal?>("Shipping:BranchLongitude");
        if (!branchLat.HasValue || !branchLng.HasValue)
        {
            _logger.LogWarning("Shipping:BranchLatitude hoặc Shipping:BranchLongitude chưa được cấu hình. Tọa độ chi nhánh sẽ là NULL.");
        }
        bool isInbound = string.Equals(request.Direction, "INBOUND", StringComparison.OrdinalIgnoreCase);
        string customerAddress = isInbound ? request.SenderAddress : request.ReceiverAddress;
        var customerCoords = await VnLocationCoordsLookup.GeocodeAddressAsync(_http, customerAddress, ct);
        decimal? senderLat = isInbound ? customerCoords.Lat : branchLat;
        decimal? senderLng = isInbound ? customerCoords.Lng : branchLng;
        decimal? receiverLat = isInbound ? branchLat : customerCoords.Lat;
        decimal? receiverLng = isInbound ? branchLng : customerCoords.Lng;

        // 3. Persist to Shipping.Shipments
        var shipment = new Shipment
        {
            CarrierCode = providerResult.CarrierCode,
            WaybillCode = providerResult.WaybillCode,
            Direction = request.Direction,
            AppointmentId = request.AppointmentId,
            RepairTicketId = request.RepairTicketId,
            SenderName = request.SenderName,
            SenderPhone = request.SenderPhone,
            SenderAddress = request.SenderAddress,
            SenderProvinceCode = request.SenderProvinceCode,
            SenderDistrictCode = request.SenderDistrictCode,
            SenderWardCode = request.SenderWardCode,
            ReceiverName = request.ReceiverName,
            ReceiverPhone = request.ReceiverPhone,
            ReceiverAddress = request.ReceiverAddress,
            ReceiverProvinceCode = request.ReceiverProvinceCode,
            ReceiverDistrictCode = request.ReceiverDistrictCode,
            ReceiverWardCode = request.ReceiverWardCode,
            CodAmount = request.CodAmount,
            InsuredValue = request.InsuredValue,
            WeightGram = request.WeightGram,
            FeeAmount = providerResult.FeeAmount,
            FeePayer = request.FeePayer,
            StatusCode = providerResult.InitialStatus ?? "CREATED",
            ExpectedDeliveryAtUtc = providerResult.ExpectedDeliveryAtUtc,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RawCreateResponseJson = providerResult.RawResponse,
            SenderLatitude = senderLat,
            SenderLongitude = senderLng,
            ReceiverLatitude = receiverLat,
            ReceiverLongitude = receiverLng
        };

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync(ct);

        // 4. Record initial shipment event
        var initEvent = new ShipmentEvent
        {
            ShipmentId = shipment.ShipmentId,
            ProviderStatus = "ready_to_pick",
            InternalStatus = "CREATED",
            Description = "Vận đơn điện tử đã được tạo thành công",
            LocationText = request.SenderAddress,
            Latitude = senderLat ?? VnLocationCoordsLookup.Lookup(request.SenderAddress).Lat,
            Longitude = senderLng ?? VnLocationCoordsLookup.Lookup(request.SenderAddress).Lng,
            OccurredAtUtc = DateTime.UtcNow,
            ReceivedAtUtc = DateTime.UtcNow
        };
        _db.ShipmentEvents.Add(initEvent);
        await _db.SaveChangesAsync(ct);

        return providerResult;
    }

    public async Task<CancelShipmentResult> CancelOrderAsync(long shipmentId, long userId, CancellationToken ct = default)
    {
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct);
        if (shipment == null)
        {
            return new CancelShipmentResult { Success = false, Message = "Không tìm thấy vận đơn" };
        }

        // Rule: Only allow cancellation if internal status is CREATED or PICKING
        if (shipment.StatusCode != "CREATED" && shipment.StatusCode != "PICKING")
        {
            return new CancelShipmentResult
            {
                Success = false,
                Message = $"Không thể hủy vận đơn ở trạng thái '{shipment.StatusCode}'. Chỉ có thể hủy khi bưu tá chưa lấy hàng."
            };
        }

        var providerRes = await _provider.CancelOrderAsync(shipment.WaybillCode, ct);
        if (providerRes.Success)
        {
            shipment.StatusCode = "CANCELLED";
            shipment.UpdatedAtUtc = DateTime.UtcNow;

            var cancelEvent = new ShipmentEvent
            {
                ShipmentId = shipment.ShipmentId,
                ProviderStatus = "cancel",
                InternalStatus = "CANCELLED",
                Description = "Đã hủy vận đơn theo yêu cầu",
                OccurredAtUtc = DateTime.UtcNow,
                ReceivedAtUtc = DateTime.UtcNow
            };
            _db.ShipmentEvents.Add(cancelEvent);
            await _db.SaveChangesAsync(ct);
        }

        return providerRes;
    }

    public async Task<string> GetPrintUrlAsync(long shipmentId, CancellationToken ct = default)
    {
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.ShipmentId == shipmentId, ct);
        if (shipment == null) throw new InvalidOperationException("Vận đơn không tồn tại.");
        return await _provider.GetPrintUrlAsync(shipment.WaybillCode, ct);
    }

    public async Task<bool> HandleGhnWebhookAsync(GhnWebhookPayload payload, string? secretHeader, CancellationToken ct = default)
    {
        // 1. Verify webhook secret
        var expectedSecret = Environment.GetEnvironmentVariable("TECHCARE_GHN_WEBHOOK_SECRET");
        if (!string.IsNullOrWhiteSpace(expectedSecret) && secretHeader != expectedSecret)
        {
            _logger.LogWarning("Webhook rejected: secret mismatch.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.OrderCode))
        {
            _logger.LogWarning("Webhook rejected: OrderCode is empty.");
            return false;
        }

        // 2. Find shipment
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.WaybillCode == payload.OrderCode, ct);
        if (shipment == null && !string.IsNullOrWhiteSpace(payload.ClientOrderCode))
        {
            // Fallback match by appointment code / client order code
            var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentCode == payload.ClientOrderCode, ct);
            if (appt != null)
            {
                shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.AppointmentId == appt.AppointmentId, ct);
            }
        }

        if (shipment == null)
        {
            _logger.LogWarning("Webhook received for unknown waybill: {OrderCode}", payload.OrderCode);
            return true; // Return 200 OK to stop GHN retrying endlessly for untracked order
        }

        string rawStatus = payload.Status ?? "transporting";
        string internalStatus = _provider.MapProviderStatus(rawStatus);
        var occurredAt = payload.Time ?? DateTime.UtcNow;

        // 3. Insert shipment event with deduplication
        // Unique index on (ShipmentId, ProviderStatus, OccurredAtUtc) prevents duplicate logging on retry
        bool alreadyExists = await _db.ShipmentEvents.AnyAsync(e =>
            e.ShipmentId == shipment.ShipmentId &&
            e.ProviderStatus == rawStatus &&
            e.OccurredAtUtc == occurredAt, ct);

        if (!alreadyExists)
        {
            var eventCoords = VnLocationCoordsLookup.Lookup(payload.Warehouse ?? payload.Description);
            decimal? lat = eventCoords.Lat;
            decimal? lng = eventCoords.Lng;

            var evt = new ShipmentEvent
            {
                ShipmentId = shipment.ShipmentId,
                ProviderStatus = rawStatus,
                InternalStatus = internalStatus,
                Description = payload.Description ?? GetStatusDescription(internalStatus),
                LocationText = payload.Warehouse,
                Latitude = lat,
                Longitude = lng,
                OccurredAtUtc = occurredAt,
                ReceivedAtUtc = DateTime.UtcNow,
                RawPayloadJson = payload.RawJson
            };

            try
            {
                _db.ShipmentEvents.Add(evt);
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogInformation(ex, "Duplicate event skipped via unique index.");
            }
        }

        // 4. Update shipment status and fee
        shipment.StatusCode = internalStatus;
        if (payload.TotalFee.HasValue && payload.TotalFee > 0)
        {
            shipment.FeeAmount = payload.TotalFee.Value;
        }
        shipment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<ShipmentTrackingDto?> GetShipmentTrackingAsync(long? appointmentId, long? repairTicketId, CancellationToken ct = default)
    {
        if (!appointmentId.HasValue && !repairTicketId.HasValue) return null;

        var shipment = await _db.Shipments
            .AsNoTracking()
            .Include(s => s.Events.OrderBy(e => e.OccurredAtUtc))
            .FirstOrDefaultAsync(s =>
                (appointmentId.HasValue && s.AppointmentId == appointmentId) ||
                (repairTicketId.HasValue && s.RepairTicketId == repairTicketId),
                ct);

        if (shipment == null) return null;

        return new ShipmentTrackingDto
        {
            ShipmentId = shipment.ShipmentId,
            CarrierCode = shipment.CarrierCode,
            WaybillCode = shipment.WaybillCode,
            Direction = shipment.Direction,
            StatusCode = shipment.StatusCode,
            StatusName = GetStatusDescription(shipment.StatusCode),
            FeeAmount = shipment.FeeAmount,
            ExpectedDeliveryAtUtc = shipment.ExpectedDeliveryAtUtc,
            SenderLatitude = shipment.SenderLatitude,
            SenderLongitude = shipment.SenderLongitude,
            ReceiverLatitude = shipment.ReceiverLatitude,
            ReceiverLongitude = shipment.ReceiverLongitude,
            Events = shipment.Events.Select(e => new ShipmentEventDto
            {
                ProviderStatus = e.ProviderStatus,
                InternalStatus = e.InternalStatus,
                Description = e.Description,
                LocationText = e.LocationText,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                OccurredAtUtc = e.OccurredAtUtc
            }).ToList()
        };
    }

    private static string GetStatusDescription(string statusCode) => statusCode switch
    {
        "CREATED" => "Đã tạo vận đơn điện tử",
        "PICKING" => "Bưu tá đang đến lấy máy",
        "PICKED" => "Bưu tá đã lấy máy về kho",
        "IN_TRANSIT" => "Đang trung chuyển qua bưu cục",
        "OUT_FOR_DELIVERY" => "Đang giao tới địa chỉ nhận",
        "DELIVERED" => "Đã giao hàng thành công",
        "DELIVERY_FAILED" => "Giao hàng chưa thành công",
        "RETURNING" => "Đang chuyển hoàn",
        "RETURNED" => "Đã chuyển hoàn thành công",
        "CANCELLED" => "Đã hủy vận đơn",
        "EXCEPTION" => "Sự cố vận chuyển bất thường",
        _ => "Đang xử lý vận chuyển"
    };
}

