using System;

namespace API_FixComputerCenter.Domain.Entities;

public class EmailToken
{
    public long TokenId { get; set; }
    public long UserId { get; set; }
    public string Email { get; set; } = null!;
    public byte[] TokenHash { get; set; } = null!;
    public string Purpose { get; set; } = null!; // 'EMAIL_CONFIRMATION', 'RESET_PASSWORD'
    public DateTime ExpiresAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
