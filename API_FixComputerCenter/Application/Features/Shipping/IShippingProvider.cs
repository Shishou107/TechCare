using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace API_FixComputerCenter.Application.Features.Shipping;

public class ShippingFeePreviewRequest
{
    public int? FromDistrictId { get; set; }
    public string? FromWardCode { get; set; }
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; } = string.Empty;
    public int WeightGram { get; set; } = 2500; // Default laptop weight
    public decimal InsuredValue { get; set; } = 0;
}

public class ShippingFeePreviewResult
{
    public decimal FeeAmount { get; set; }
    public DateTime? ExpectedDeliveryTime { get; set; }
    public string Note { get; set; } = "Phí cước tham khảo tính từ GHN Express";
    public string CarrierCode { get; set; } = "GHN";
}

public class CreateShipmentRequest
{
    public long? AppointmentId { get; set; }
    public long? RepairTicketId { get; set; }
    public string Direction { get; set; } = "INBOUND"; // INBOUND or OUTBOUND
    public string ClientOrderCode { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public string? SenderProvinceCode { get; set; }
    public string? SenderDistrictCode { get; set; }
    public string? SenderWardCode { get; set; }

    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ReceiverAddress { get; set; } = string.Empty;
    public string? ReceiverProvinceCode { get; set; }
    public string? ReceiverDistrictCode { get; set; }
    public string? ReceiverWardCode { get; set; }

    public decimal CodAmount { get; set; } = 0;
    public decimal InsuredValue { get; set; } = 0;
    public int WeightGram { get; set; } = 2500;
    public string FeePayer { get; set; } = "SENDER"; // SENDER or RECEIVER
    public string? Note { get; set; }
}

public class CreateShipmentResult
{
    public bool Success { get; set; }
    public string CarrierCode { get; set; } = "GHN";
    public string WaybillCode { get; set; } = string.Empty;
    public string? InitialStatus { get; set; } = "CREATED";
    public decimal? FeeAmount { get; set; }
    public DateTime? ExpectedDeliveryAtUtc { get; set; }
    public string? RawResponse { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CancelShipmentResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class GhnLocationItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Code { get; set; }
}

public interface IShippingProvider
{
    string CarrierCode { get; }
    Task<ShippingFeePreviewResult> PreviewFeeAsync(ShippingFeePreviewRequest request, CancellationToken ct = default);
    Task<CreateShipmentResult> CreateOrderAsync(CreateShipmentRequest request, CancellationToken ct = default);
    Task<CancelShipmentResult> CancelOrderAsync(string waybillCode, CancellationToken ct = default);
    Task<string> GetPrintUrlAsync(string waybillCode, CancellationToken ct = default);
    string MapProviderStatus(string providerStatus);
    Task<IReadOnlyList<GhnLocationItem>> GetProvincesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GhnLocationItem>> GetDistrictsAsync(int provinceId, CancellationToken ct = default);
    Task<IReadOnlyList<GhnLocationItem>> GetWardsAsync(int districtId, CancellationToken ct = default);
}
