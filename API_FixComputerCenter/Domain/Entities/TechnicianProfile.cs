using System;

namespace API_FixComputerCenter.Domain.Entities;

public class TechnicianProfile
{
    public long ProfileId { get; set; }
    public long UserId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string Specialty { get; set; } = null!;
    public string? Bio { get; set; }
    public int ExperienceYears { get; set; }
    public bool IsPublic { get; set; }
    public bool IsAcceptingRequests { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}

