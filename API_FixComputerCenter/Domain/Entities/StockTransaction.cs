using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class StockTransaction
{
    public long StockTransactionId { get; set; }

    public int BranchId { get; set; }

    public long PartId { get; set; }

    public long? SupplierId { get; set; }

    public long? RepairTicketId { get; set; }

    public string TransactionType { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public string? ReferenceCode { get; set; }

    public string? Note { get; set; }

    public long PerformedByUserId { get; set; }

    public DateTime PerformedAtUtc { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Part Part { get; set; } = null!;

    public virtual User PerformedByUser { get; set; } = null!;

    public virtual ICollection<RepairPart> RepairParts { get; set; } = new List<RepairPart>();

    public virtual RepairTicket? RepairTicket { get; set; }

    public virtual Supplier? Supplier { get; set; }
}
