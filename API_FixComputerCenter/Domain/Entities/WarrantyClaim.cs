using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class WarrantyClaim
{
    public long WarrantyClaimId { get; set; }

    public long WarrantyId { get; set; }

    public string ClaimCode { get; set; } = null!;

    public string CustomerDescription { get; set; } = null!;

    public string? InspectionResult { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ReceivedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public long? HandledByUserId { get; set; }

    public string? ResolutionNote { get; set; }

    public virtual User? HandledByUser { get; set; }

    public virtual Warranty Warranty { get; set; } = null!;
}
