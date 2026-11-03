using API_FixComputerCenter.Domain.Entities;
using API_FixComputerCenter.Domain.Entities.Shipping;
using API_FixComputerCenter.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace API_FixComputerCenter.Application.Common.Interfaces;

public interface ITechCareDbContext
{
    DbSet<Appointment> Appointments { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Branch> Branches { get; }
    DbSet<BranchStock> BranchStocks { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Device> Devices { get; }
    DbSet<DeviceBrand> DeviceBrands { get; }
    DbSet<DiagnosisReport> DiagnosisReports { get; }
    DbSet<DiagnosticRun> DiagnosticRuns { get; }
    DbSet<FileAttachment> FileAttachments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Part> Parts { get; }
    DbSet<PartCategory> PartCategories { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationApproval> QuotationApprovals { get; }
    DbSet<QuotationItem> QuotationItems { get; }
    DbSet<Session> Sessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<RepairPart> RepairParts { get; }
    DbSet<RepairTicket> RepairTickets { get; }
    DbSet<Role> Roles { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketStatusHistory> TicketStatusHistories { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<VwBranchStockStatus> VwBranchStockStatuses { get; }
    DbSet<Warranty> Warranties { get; }
    DbSet<WarrantyClaim> WarrantyClaims { get; }

    // Shipping & AI Entities
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentEvent> ShipmentEvents { get; }
    DbSet<KnowledgeDocument> KnowledgeDocuments { get; }
    DbSet<KnowledgeChunk> KnowledgeChunks { get; }
    DbSet<ChatSession> ChatSessions { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    DatabaseFacade Database { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
