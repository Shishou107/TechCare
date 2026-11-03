using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class RepairTicket
{
    public long RepairTicketId { get; set; }

    public int BranchId { get; set; }

    public long CustomerId { get; set; }

    public long DeviceId { get; set; }

    public long? AppointmentId { get; set; }

    public string TicketCode { get; set; } = null!;

    public byte[]? TrackingCodeHash { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public DateTime? ExpectedCompletionAtUtc { get; set; }

    public DateTime? ActualCompletionAtUtc { get; set; }

    public string Status { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public string CustomerIssue { get; set; } = null!;

    public string? InitialCondition { get; set; }

    public string? Accessories { get; set; }

    public string? IntakeNotes { get; set; }

    public decimal InspectionFee { get; set; }

    public bool IsInspectionFeeWaived { get; set; }

    public long? AssignedTechnicianUserId { get; set; }

    public long? PreferredTechnicianUserId { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Appointment? Appointment { get; set; }

    public virtual User? AssignedTechnicianUser { get; set; }

    public virtual User? PreferredTechnicianUser { get; set; }

    public virtual TicketRating? TicketRating { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual User? CreatedByUser { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Device Device { get; set; } = null!;

    public virtual ICollection<DiagnosisReport> DiagnosisReports { get; set; } = new List<DiagnosisReport>();

    public virtual ICollection<DiagnosticRun> DiagnosticRuns { get; set; } = new List<DiagnosticRun>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

    public virtual ICollection<RepairPart> RepairParts { get; set; } = new List<RepairPart>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<TicketAttachment> TicketAttachments { get; set; } = new List<TicketAttachment>();

    public virtual ICollection<TicketStatusHistory> TicketStatusHistories { get; set; } = new List<TicketStatusHistory>();

    public virtual ICollection<Warranty> Warranties { get; set; } = new List<Warranty>();
}
