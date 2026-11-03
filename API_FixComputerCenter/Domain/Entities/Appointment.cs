using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Appointment
{
    public long AppointmentId { get; set; }

    public int BranchId { get; set; }

    public long CustomerId { get; set; }

    public long? DeviceId { get; set; }

    public string AppointmentCode { get; set; } = null!;

    public DateTime ScheduledAtUtc { get; set; }

    public string Subject { get; set; } = null!;

    public string? CustomerDescription { get; set; }

    public string Status { get; set; } = null!;

    public long? CreatedByUserId { get; set; }

    public long? PreferredTechnicianUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Branch Branch { get; set; } = null!;

    public virtual User? CreatedByUser { get; set; }

    public virtual User? PreferredTechnicianUser { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Device? Device { get; set; }

    public virtual ICollection<RepairTicket> RepairTickets { get; set; } = new List<RepairTicket>();
}
