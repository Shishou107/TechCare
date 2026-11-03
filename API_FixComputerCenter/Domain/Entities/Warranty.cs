using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Warranty
{
    public long WarrantyId { get; set; }

    public long RepairTicketId { get; set; }

    public long? RepairPartId { get; set; }

    public string WarrantyCode { get; set; } = null!;

    public string CoverageDescription { get; set; } = null!;

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public string? Terms { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual RepairPart? RepairPart { get; set; }

    public virtual RepairTicket RepairTicket { get; set; } = null!;

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();
}
