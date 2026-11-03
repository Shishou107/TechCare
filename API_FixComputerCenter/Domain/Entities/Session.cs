using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class Session
{
    public Guid SessionId { get; set; }

    public long UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastUsedAtUtc { get; set; }

    public DateTime AbsoluteExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokeReason { get; set; }

    public string? CreatedByIp { get; set; }

    public string? UserAgent { get; set; }

    public string? DeviceLabel { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

