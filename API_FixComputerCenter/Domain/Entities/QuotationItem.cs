using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class QuotationItem
{
    public long QuotationItemId { get; set; }

    public long QuotationId { get; set; }

    public string ItemType { get; set; } = null!;

    public long? PartId { get; set; }

    public string Description { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal? LineTotal { get; set; }

    public string CustomerDecision { get; set; } = null!;

    public virtual Quotation Quotation { get; set; } = null!;
}
