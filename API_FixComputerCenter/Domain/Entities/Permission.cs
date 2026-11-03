using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Permission
{
    public int PermissionId { get; set; }

    public string PermissionCode { get; set; } = null!;

    public string PermissionName { get; set; } = null!;

    public string ModuleCode { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
