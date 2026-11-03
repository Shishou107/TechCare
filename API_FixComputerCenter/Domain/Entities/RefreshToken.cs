using System;

namespace API_FixComputerCenter.Domain.Entities;

public partial class RefreshToken
{
    public long RefreshTokenId { get; set; }

    public Guid SessionId { get; set; }

    public long UserId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RotatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? ReuseDetectedAtUtc { get; set; }

    public byte[]? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public string? LastUsedByIp { get; set; }

    public string? RevokedByIp { get; set; }

    public virtual Session Session { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
