using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class User
{
    public long UserId { get; set; }

    public int? BranchId { get; set; }

    public string UserName { get; set; } = null!;

    public string NormalizedUserName { get; set; } = null!;

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? PhoneNumber { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public bool IsEmailConfirmed { get; set; }

    public bool IsPhoneConfirmed { get; set; }

    public int AccessFailedCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public bool LockoutEnabled { get; set; }

    public bool IsActive { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public long? DeletedByUserId { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? DeletedByUser { get; set; }

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual Branch? Branch { get; set; }

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual ICollection<DiagnosisReport> DiagnosisReports { get; set; } = new List<DiagnosisReport>();

    public virtual ICollection<DiagnosticRun> DiagnosticRuns { get; set; } = new List<DiagnosticRun>();

    public virtual ICollection<FileAttachment> FileAttachments { get; set; } = new List<FileAttachment>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<QuotationApproval> QuotationApprovals { get; set; } = new List<QuotationApproval>();

    public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<RepairPart> RepairParts { get; set; } = new List<RepairPart>();

    public virtual ICollection<RepairTicket> RepairTicketAssignedTechnicianUsers { get; set; } = new List<RepairTicket>();

    public virtual ICollection<RepairTicket> RepairTicketCreatedByUsers { get; set; } = new List<RepairTicket>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<TicketStatusHistory> TicketStatusHistories { get; set; } = new List<TicketStatusHistory>();

    public virtual ICollection<UserRole> UserRoleAssignedByUsers { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRole> UserRoleUsers { get; set; } = new List<UserRole>();

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();

    public virtual TechnicianProfile? TechnicianProfile { get; set; }
}
