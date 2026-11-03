using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class QuotationApproval
{
    public long QuotationApprovalId { get; set; }

    public long QuotationId { get; set; }

    public string Decision { get; set; } = null!;

    public DateTime DecisionAtUtc { get; set; }

    public string DecisionChannel { get; set; } = null!;

    public string? CustomerNote { get; set; }

    public long? RecordedByUserId { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;

    public virtual User? RecordedByUser { get; set; }
}
