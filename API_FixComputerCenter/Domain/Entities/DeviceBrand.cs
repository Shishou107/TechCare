using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class DeviceBrand
{
    public int DeviceBrandId { get; set; }

    public string BrandName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
}
