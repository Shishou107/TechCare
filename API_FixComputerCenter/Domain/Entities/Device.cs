using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Device
{
    public long DeviceId { get; set; }

    public long CustomerId { get; set; }

    public int? DeviceBrandId { get; set; }

    public string ModelName { get; set; } = null!;

    public string? SerialNumber { get; set; }

    public string? AssetTag { get; set; }

    public string? Cpu { get; set; }

    public string? RamDescription { get; set; }

    public string? StorageDescription { get; set; }

    public string? OperatingSystem { get; set; }

    public string? Color { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual DeviceBrand? DeviceBrand { get; set; }

    public virtual ICollection<RepairTicket> RepairTickets { get; set; } = new List<RepairTicket>();
}
