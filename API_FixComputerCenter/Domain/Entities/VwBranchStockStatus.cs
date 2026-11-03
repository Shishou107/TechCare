using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class VwBranchStockStatus
{
    public int BranchId { get; set; }

    public string BranchCode { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public long PartId { get; set; }

    public string PartCode { get; set; } = null!;

    public string PartName { get; set; } = null!;

    public decimal QuantityOnHand { get; set; }

    public decimal ReorderLevel { get; set; }

    public bool? IsLowStock { get; set; }
}
