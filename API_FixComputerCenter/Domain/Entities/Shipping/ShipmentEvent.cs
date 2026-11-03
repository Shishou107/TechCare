using System;

namespace API_FixComputerCenter.Domain.Entities.Shipping;

public class ShipmentEvent
{
    public long EventId { get; set; }
    public long ShipmentId { get; set; }
    public string ProviderStatus { get; set; } = string.Empty; // e.g. "transporting", "delivering"
    public string InternalStatus { get; set; } = string.Empty; // e.g. "IN_TRANSIT", "OUT_FOR_DELIVERY"
    public string? Description { get; set; }
    public string? LocationText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public string? RawPayloadJson { get; set; }

    public virtual Shipment Shipment { get; set; } = null!;
}
