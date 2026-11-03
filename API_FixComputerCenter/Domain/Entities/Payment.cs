using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Payment
{
    public long PaymentId { get; set; }

    public long InvoiceId { get; set; }

    public string PaymentCode { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public string? TransactionReference { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public long? ReceivedByUserId { get; set; }

    public string? Note { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual User? ReceivedByUser { get; set; }
}
