using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class DiagnosisReport
{
    public long DiagnosisReportId { get; set; }

    public long RepairTicketId { get; set; }

    public long TechnicianUserId { get; set; }

    public DateTime DiagnosedAtUtc { get; set; }

    public string? Symptoms { get; set; }

    public string Findings { get; set; } = null!;

    public string? SuspectedCauses { get; set; }

    public string? RecommendedActions { get; set; }

    public int? EstimatedMinutes { get; set; }

    public bool IsFinal { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual RepairTicket RepairTicket { get; set; } = null!;

    public virtual User TechnicianUser { get; set; } = null!;
}
