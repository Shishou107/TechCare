using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Part
{
    public long PartId { get; set; }

    public int? PartCategoryId { get; set; }

    public string PartCode { get; set; } = null!;

    public string PartName { get; set; } = null!;

    public string? Brand { get; set; }

    public string? Specification { get; set; }

    public string Unit { get; set; } = null!;

    public decimal DefaultCost { get; set; }

    public decimal DefaultSalePrice { get; set; }

    public decimal ReorderLevel { get; set; }

    public bool IsSerialized { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<BranchStock> BranchStocks { get; set; } = new List<BranchStock>();

    public virtual PartCategory? PartCategory { get; set; }

    public virtual ICollection<RepairPart> RepairParts { get; set; } = new List<RepairPart>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
}
