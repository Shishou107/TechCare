using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Branch
{
    public int BranchId { get; set; }

    public string BranchCode { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? AddressLine { get; set; }

    public string? Ward { get; set; }

    public string? District { get; set; }

    public string? Province { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BranchStock> BranchStocks { get; set; } = new List<BranchStock>();

    public virtual ICollection<RepairPart> RepairParts { get; set; } = new List<RepairPart>();

    public virtual ICollection<RepairTicket> RepairTickets { get; set; } = new List<RepairTicket>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
