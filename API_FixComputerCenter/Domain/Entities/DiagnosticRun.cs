using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class DiagnosticRun
{
    public long DiagnosticRunId { get; set; }

    public long RepairTicketId { get; set; }

    public long? CollectedByUserId { get; set; }

    public string? AgentVersion { get; set; }

    public DateTime ConsentRecordedAtUtc { get; set; }

    public DateTime CollectedAtUtc { get; set; }

    public string? DeviceSummaryJson { get; set; }

    public long? ReportFileId { get; set; }

    public virtual User? CollectedByUser { get; set; }

    public virtual RepairTicket RepairTicket { get; set; } = null!;
}
