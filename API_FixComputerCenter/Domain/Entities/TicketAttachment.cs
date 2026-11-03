using System;
using System.Collections.Generic;

namespace API_FixComputerCenter.Domain.Entities;

public partial class TicketAttachment
{
    public long RepairTicketId { get; set; }

    public long FileId { get; set; }

    public string AttachmentCategory { get; set; } = null!;

    public string? Caption { get; set; }

    public virtual FileAttachment File { get; set; } = null!;

    public virtual RepairTicket RepairTicket { get; set; } = null!;
}
