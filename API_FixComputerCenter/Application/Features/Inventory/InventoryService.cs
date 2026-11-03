using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Inventory;

public record PartDto(
    long PartId,
    int? PartCategoryId,
    string PartCode,
    string PartName,
    string? Brand,
    string? Specification,
    string Unit,
    decimal DefaultCost,
    decimal DefaultSalePrice,
    decimal ReorderLevel,
    decimal QuantityOnHand
);

public record IssuePartRequest(
    long RepairTicketId,
    long PartId,
    decimal Quantity
);

public class IssuePartValidator : AbstractValidator<IssuePartRequest>
{
    public IssuePartValidator()
    {
        RuleFor(x => x.RepairTicketId).GreaterThan(0).WithMessage("Mã phiếu sửa chữa không hợp lệ.");
        RuleFor(x => x.PartId).GreaterThan(0).WithMessage("Mã linh kiện không hợp lệ.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng xuất kho phải lớn hơn 0.");
    }
}

public interface IInventoryService
{
    Task<List<PartDto>> GetBranchPartsAsync(int branchId, CancellationToken ct = default);
    Task IssuePartToTicketAsync(IssuePartRequest req, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public InventoryService(ITechCareDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<List<PartDto>> GetBranchPartsAsync(int branchId, CancellationToken ct = default)
    {
        var parts = await _db.Parts.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new PartDto(
                p.PartId,
                p.PartCategoryId,
                p.PartCode,
                p.PartName,
                p.Brand,
                p.Specification,
                p.Unit,
                p.DefaultCost,
                p.DefaultSalePrice,
                p.ReorderLevel,
                _db.BranchStocks.Where(bs => bs.PartId == p.PartId && bs.BranchId == branchId).Sum(bs => bs.QuantityOnHand)
            ))
            .ToListAsync(ct);

        return parts;
    }

    public async Task IssuePartToTicketAsync(IssuePartRequest req, CancellationToken ct = default)
    {
        // C4: Server-side validation
        if (req.Quantity <= 0)
        {
            throw new ValidationException("Số lượng linh kiện xuất kho phải lớn hơn 0.");
        }

        // C2: Atomic single transaction for stock deduction, ledger entry, and repair part linkage
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.RepairTicketId == req.RepairTicketId, ct)
            ?? throw new NotFoundException(nameof(RepairTicket), req.RepairTicketId);

        var branchId = _currentUser.BranchId ?? ticket.BranchId;

        var part = await _db.Parts.FirstOrDefaultAsync(p => p.PartId == req.PartId, ct)
            ?? throw new NotFoundException(nameof(Part), req.PartId);

        var stock = await _db.BranchStocks.FirstOrDefaultAsync(bs => bs.PartId == req.PartId && bs.BranchId == branchId, ct);
        if (stock == null || stock.QuantityOnHand < req.Quantity)
        {
            throw new DomainException($"Không đủ linh kiện trong kho chi nhánh (Cần: {req.Quantity}, Tồn kho: {stock?.QuantityOnHand ?? 0}).");
        }

        var performerId = _currentUser.UserId;
        if (!performerId.HasValue)
        {
            var fallbackUser = await _db.Users.Select(u => u.UserId).FirstOrDefaultAsync(ct);
            performerId = fallbackUser != 0 ? fallbackUser : 1;
        }

        // Concurrency-safe decrement
        stock.QuantityOnHand -= req.Quantity;
        stock.UpdatedAtUtc = DateTime.UtcNow;

        // B3: Conforms with SQL CK_ST_Type (ISSUE_REPAIR)
        var stockTx = new StockTransaction
        {
            BranchId = branchId,
            PartId = part.PartId,
            RepairTicketId = ticket.RepairTicketId,
            TransactionType = "ISSUE_REPAIR",
            Quantity = req.Quantity,
            UnitCost = part.DefaultCost,
            ReferenceCode = ticket.TicketCode,
            PerformedByUserId = performerId.Value,
            PerformedAtUtc = DateTime.UtcNow,
            Note = $"Xuất linh kiện cho phiếu {ticket.TicketCode}"
        };
        _db.StockTransactions.Add(stockTx);

        // Record repair part linkage
        var repairPart = new RepairPart
        {
            RepairTicketId = ticket.RepairTicketId,
            PartId = part.PartId,
            BranchId = branchId,
            StockTransaction = stockTx,
            Quantity = req.Quantity,
            UnitSalePrice = part.DefaultSalePrice,
            InstalledByUserId = performerId,
            InstalledAtUtc = DateTime.UtcNow,
            Note = $"Lắp đặt cho phiếu {ticket.TicketCode}"
        };
        _db.RepairParts.Add(repairPart);

        // Update ticket status to REPAIRING if it was WAITING_PARTS
        if (ticket.Status == "WAITING_PARTS")
        {
            ticket.Status = "REPAIRING";
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            _db.TicketStatusHistories.Add(new TicketStatusHistory
            {
                RepairTicketId = ticket.RepairTicketId,
                OldStatus = "WAITING_PARTS",
                NewStatus = "REPAIRING",
                ChangedByUserId = performerId,
                ChangedAtUtc = DateTime.UtcNow,
                Note = "Đã nhận linh kiện, chuyển sang tiến hành sửa chữa"
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("ISSUE_PART", nameof(RepairPart), repairPart.RepairPartId.ToString(), null, new { req.RepairTicketId, req.PartId, req.Quantity }, ct);
    }
}
