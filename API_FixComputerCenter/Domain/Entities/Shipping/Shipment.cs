using System;
using System.Collections.Generic;
using API_FixComputerCenter.Domain.Entities;

namespace API_FixComputerCenter.Domain.Entities.Shipping;

public class Shipment
{
    public long ShipmentId { get; set; }
    public string CarrierCode { get; set; } = "GHN"; // e.g. "GHN", "GHTK"
    public string WaybillCode { get; set; } = string.Empty; // GHN order_code
    public string Direction { get; set; } = "INBOUND"; // INBOUND (customer to shop) or OUTBOUND (shop to customer)
    
    public long? AppointmentId { get; set; }
    public long? RepairTicketId { get; set; }

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
    public int? WeightGram { get; set; } = 2500;
    public decimal? FeeAmount { get; set; }
    public string? FeePayer { get; set; } // "SENDER" or "RECEIVER"
    
    public string StatusCode { get; set; } = "CREATED";
    // Statuses: CREATED, PICKING, PICKED, IN_TRANSIT, OUT_FOR_DELIVERY, DELIVERED, DELIVERY_FAILED, RETURNING, RETURNED, CANCELLED, EXCEPTION
    
    public DateTime? ExpectedDeliveryAtUtc { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? RawCreateResponseJson { get; set; }

    public virtual Appointment? Appointment { get; set; }
    public virtual RepairTicket? RepairTicket { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<ShipmentEvent> Events { get; set; } = new List<ShipmentEvent>();
}
