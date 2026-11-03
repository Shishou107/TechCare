using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Quotation
{
    public long QuotationId { get; set; }

    public long RepairTicketId { get; set; }

    public string QuotationCode { get; set; } = null!;

    public int VersionNumber { get; set; }

    public string QuotationType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? CustomerNote { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<QuotationApproval> QuotationApprovals { get; set; } = new List<QuotationApproval>();

    public virtual ICollection<QuotationItem> QuotationItems { get; set; } = new List<QuotationItem>();

    public virtual RepairTicket RepairTicket { get; set; } = null!;
}
