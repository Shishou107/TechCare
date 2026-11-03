using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Quotations;

public record QuotationDto(
    long QuotationId,
    long RepairTicketId,
    string QuotationCode,
    int VersionNumber,
    string QuotationType,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal? TotalAmount,
    string Status,
    DateTime? ValidUntilUtc,
    List<QuotationItemDto> Items
);

public record QuotationItemDto(
    long QuotationItemId,
    string ItemType,
    long? PartId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? LineTotal,
    string CustomerDecision
);

public record CreateQuotationItemRequest(
    string ItemType,
    long? PartId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? DiscountAmount
);

public record CreateQuotationRequest(
    long RepairTicketId,
    string? QuotationType,
    decimal DiscountAmount,
    decimal TaxAmount,
    DateTime? ValidUntilUtc,
    string? CustomerNote,
    List<CreateQuotationItemRequest> Items
);

public record QuotationApprovalRequest(
    string Decision, // APPROVED or DECLINED
    string? DecisionChannel,
    string? CustomerNote
);

public interface IQuotationService
{
    Task<QuotationDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<QuotationDto> CreateQuotationAsync(CreateQuotationRequest req, CancellationToken ct = default);
    Task<QuotationDto> ApproveOrRejectAsync(long quotationId, QuotationApprovalRequest req, CancellationToken ct = default);
}

public class QuotationService : IQuotationService
{
    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public QuotationService(ITechCareDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<QuotationDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var q = await _db.Quotations.AsNoTracking()
            .Include(x => x.QuotationItems)
            .FirstOrDefaultAsync(x => x.QuotationId == id, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        return new QuotationDto(
            q.QuotationId,
            q.RepairTicketId,
            q.QuotationCode,
            q.VersionNumber,
            q.QuotationType,
            q.Subtotal,
            q.DiscountAmount,
            q.TaxAmount,
            q.TotalAmount,
            q.Status,
            q.ValidUntilUtc,
            q.QuotationItems.Select(i => new QuotationItemDto(
                i.QuotationItemId,
                i.ItemType,
                i.PartId,
                i.Description,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                i.CustomerDecision
            )).ToList()
        );
    }

    public async Task<QuotationDto> CreateQuotationAsync(CreateQuotationRequest req, CancellationToken ct = default)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var tx = await _db.Database.BeginTransactionAsync(ct);

                var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.RepairTicketId == req.RepairTicketId, ct)
                    ?? throw new NotFoundException(nameof(RepairTicket), req.RepairTicketId);

                decimal subtotal = 0;
                var items = new List<QuotationItem>();

                foreach (var itm in req.Items)
                {
                    var qty = Math.Max(1, itm.Quantity);
                    var itemDiscount = itm.DiscountAmount ?? 0;
                    var lineTotal = (itm.UnitPrice * qty) - itemDiscount;
                    subtotal += lineTotal;

                    items.Add(new QuotationItem
                    {
                        ItemType = itm.ItemType.ToUpperInvariant(),
                        PartId = itm.PartId,
                        Description = itm.Description.Trim(),
                        Quantity = qty,
                        UnitPrice = itm.UnitPrice,
                        DiscountAmount = itemDiscount,
                        CustomerDecision = "PENDING"
                    });
                }

                // Compute VersionNumber = max + 1
                var maxVersion = await _db.Quotations
                    .Where(x => x.RepairTicketId == ticket.RepairTicketId)
                    .MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
                var nextVersion = maxVersion + 1;

                // Generate unique quotation code with collision retry
                string qCode = string.Empty;
                for (int codeAttempt = 0; codeAttempt < 5; codeAttempt++)
                {
                    var candidate = "BG-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Random.Shared.Next(1000, 9999);
                    if (!await _db.Quotations.AnyAsync(q => q.QuotationCode == candidate, ct))
                    {
                        qCode = candidate;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(qCode))
                {
                    qCode = "BG-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
                }

                var creatorId = _currentUser.UserId;
                if (!creatorId.HasValue)
                {
                    var fallbackUser = await _db.Users.Select(u => u.UserId).FirstOrDefaultAsync(ct);
                    creatorId = fallbackUser != 0 ? fallbackUser : 1;
                }

                var quotation = new Quotation
                {
                    RepairTicketId = ticket.RepairTicketId,
                    QuotationCode = qCode,
                    VersionNumber = nextVersion,
                    QuotationType = req.QuotationType ?? "INITIAL",
                    Status = "SENT",
                    IssuedAtUtc = DateTime.UtcNow,
                    ValidUntilUtc = req.ValidUntilUtc ?? DateTime.UtcNow.AddDays(7),
                    Subtotal = subtotal,
                    DiscountAmount = req.DiscountAmount,
                    TaxAmount = req.TaxAmount,
                    CustomerNote = req.CustomerNote?.Trim(),
                    CreatedByUserId = creatorId,
                    CreatedAtUtc = DateTime.UtcNow,
                    QuotationItems = items
                };

                _db.Quotations.Add(quotation);

                var oldTicketStatus = ticket.Status;
                ticket.Status = "WAITING_APPROVAL";
                ticket.UpdatedAtUtc = DateTime.UtcNow;

                _db.TicketStatusHistories.Add(new TicketStatusHistory
                {
                    RepairTicketId = ticket.RepairTicketId,
                    OldStatus = oldTicketStatus,
                    NewStatus = "WAITING_APPROVAL",
                    ChangedByUserId = creatorId,
                    ChangedAtUtc = DateTime.UtcNow,
                    Note = $"Đã xuất báo giá {qCode} phiên bản {nextVersion}"
                });

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                await _audit.LogAsync("CREATE_QUOTATION", nameof(Quotation), quotation.QuotationId.ToString(), null, quotation, ct);

                return await GetByIdAsync(quotation.QuotationId, ct);
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // Concurrency retry on UQ_Quote_TicketVersion or unique quotation code collision
                _db.ChangeTracker.Clear();
                await Task.Delay(50 * attempt, ct);
            }
        }

        throw new DomainException("Không thể tạo báo giá do xung đột phiên bản đồng thời. Vui lòng thử lại.");
    }

    public async Task<QuotationDto> ApproveOrRejectAsync(long quotationId, QuotationApprovalRequest req, CancellationToken ct = default)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var q = await _db.Quotations.Include(x => x.RepairTicket).FirstOrDefaultAsync(x => x.QuotationId == quotationId, ct)
            ?? throw new NotFoundException(nameof(Quotation), quotationId);

        if (q.Status == "APPROVED")
        {
            throw new DomainException("Báo giá này đã được duyệt trước đó, không thể chỉnh sửa.");
        }

        var decision = req.Decision.Trim().ToUpperInvariant();
        // B2: SQL CK_Quote_Status & CK_QApproval_Decision accept APPROVED or DECLINED
        bool isApproved = decision == "APPROVED" || decision == "ACCEPT";
        var finalDecision = isApproved ? "APPROVED" : "DECLINED";

        q.Status = finalDecision;

        var recorderId = _currentUser.UserId;
        if (!recorderId.HasValue)
        {
            var fallbackUser = await _db.Users.Select(u => u.UserId).FirstOrDefaultAsync(ct);
            recorderId = fallbackUser != 0 ? fallbackUser : 1;
        }

        var approval = new QuotationApproval
        {
            QuotationId = q.QuotationId,
            Decision = finalDecision,
            DecisionChannel = req.DecisionChannel ?? "PORTAL",
            DecisionAtUtc = DateTime.UtcNow,
            CustomerNote = req.CustomerNote?.Trim(),
            RecordedByUserId = recorderId
        };
        _db.QuotationApprovals.Add(approval);

        if (q.RepairTicket != null)
        {
            var oldStatus = q.RepairTicket.Status;
            // B1: Map to valid CK_Ticket_Status values
            var newStatus = isApproved ? "WAITING_PARTS" : "CANCELLED";
            q.RepairTicket.Status = newStatus;
            q.RepairTicket.UpdatedAtUtc = DateTime.UtcNow;

            _db.TicketStatusHistories.Add(new TicketStatusHistory
            {
                RepairTicketId = q.RepairTicket.RepairTicketId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedByUserId = recorderId,
                ChangedAtUtc = DateTime.UtcNow,
                Note = isApproved ? "Khách hàng duyệt báo giá, chờ linh kiện" : "Khách hàng từ chối báo giá"
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("QUOTATION_DECISION", nameof(Quotation), q.QuotationId.ToString(), null, approval, ct);

        return await GetByIdAsync(q.QuotationId, ct);
    }
}
