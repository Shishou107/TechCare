using System;

namespace API_FixComputerCenter.Domain.Entities;

public class OtpCode
{
    public long OtpId { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public byte[] CodeHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
