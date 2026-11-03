using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class UserRole
{
    public long UserId { get; set; }

    public int RoleId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public long? AssignedByUserId { get; set; }

    public virtual User? AssignedByUser { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
