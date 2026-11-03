using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class PartCategory
{
    public int PartCategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public int? ParentCategoryId { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<PartCategory> InverseParentCategory { get; set; } = new List<PartCategory>();

    public virtual PartCategory? ParentCategory { get; set; }

    public virtual ICollection<Part> Parts { get; set; } = new List<Part>();
}
