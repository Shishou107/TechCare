using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class RepairPart
{
    public long RepairPartId { get; set; }

    public long RepairTicketId { get; set; }

    public long PartId { get; set; }

    public int BranchId { get; set; }

    public long? StockTransactionId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitSalePrice { get; set; }

    public DateTime? InstalledAtUtc { get; set; }

    public long? InstalledByUserId { get; set; }

    public string? Note { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual User? InstalledByUser { get; set; }

    public virtual Part Part { get; set; } = null!;

    public virtual RepairTicket RepairTicket { get; set; } = null!;

    public virtual StockTransaction? StockTransaction { get; set; }

    public virtual ICollection<Warranty> Warranties { get; set; } = new List<Warranty>();
}
