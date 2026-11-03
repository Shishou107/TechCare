using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class FileAttachment
{
    public long FileId { get; set; }

    public string StorageProvider { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public byte[]? Sha256Hash { get; set; }

    public long? UploadedByUserId { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<TicketAttachment> TicketAttachments { get; set; } = new List<TicketAttachment>();

    public virtual User? UploadedByUser { get; set; }
}
