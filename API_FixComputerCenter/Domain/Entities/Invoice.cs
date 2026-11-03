using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Invoice
{
    public long InvoiceId { get; set; }

    public long RepairTicketId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public string InvoiceType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? IssuedAtUtc { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? Note { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual RepairTicket RepairTicket { get; set; } = null!;
}
