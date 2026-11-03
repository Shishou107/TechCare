using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class BranchStock
{
    public int BranchId { get; set; }

    public long PartId { get; set; }

    public decimal QuantityOnHand { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Branch Branch { get; set; } = null!;

    public virtual Part Part { get; set; } = null!;
}
