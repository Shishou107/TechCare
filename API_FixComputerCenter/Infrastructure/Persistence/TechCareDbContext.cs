using System;
using System.Collections.Generic;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Infrastructure.Persistence;

public partial class TechCareDbContext : DbContext
{
    public TechCareDbContext(DbContextOptions<TechCareDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchStock> BranchStocks { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Device> Devices { get; set; }

    public virtual DbSet<DeviceBrand> DeviceBrands { get; set; }

    public virtual DbSet<DiagnosisReport> DiagnosisReports { get; set; }

    public virtual DbSet<DiagnosticRun> DiagnosticRuns { get; set; }

    public virtual DbSet<FileAttachment> FileAttachments { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Part> Parts { get; set; }

    public virtual DbSet<PartCategory> PartCategories { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Quotation> Quotations { get; set; }

    public virtual DbSet<QuotationApproval> QuotationApprovals { get; set; }

    public virtual DbSet<QuotationItem> QuotationItems { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<RepairPart> RepairParts { get; set; }

    public virtual DbSet<RepairTicket> RepairTickets { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<StockTransaction> StockTransactions { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TicketAttachment> TicketAttachments { get; set; }

    public virtual DbSet<TicketStatusHistory> TicketStatusHistories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<VwBranchStockStatus> VwBranchStockStatuses { get; set; }

    public virtual DbSet<Warranty> Warranties { get; set; }

    public virtual DbSet<WarrantyClaim> WarrantyClaims { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC24F094E45");

            entity.ToTable("Appointments", "CRM");

            entity.HasIndex(e => new { e.BranchId, e.ScheduledAtUtc, e.Status }, "IX_Appointments_Schedule");

            entity.HasIndex(e => e.AppointmentCode, "UQ__Appointm__F67FE26FAD135E8F").IsUnique();

            entity.Property(e => e.AppointmentCode).HasMaxLength(30);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerDescription).HasMaxLength(1500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.ScheduledAtUtc).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("BOOKED");
            entity.Property(e => e.Subject).HasMaxLength(200);

            entity.HasOne(d => d.Branch).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_App_Branch");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_App_User");

            entity.HasOne(d => d.Customer).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_App_Customer");

            entity.HasOne(d => d.Device).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.DeviceId)
                .HasConstraintName("FK_App_Device");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBD6E57A5B9");

            entity.ToTable("AuditLogs", "Audit");

            entity.HasIndex(e => new { e.EntityName, e.EntityId, e.OccurredAtUtc }, "IX_Audit_EntityDate").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.UserId, e.OccurredAtUtc }, "IX_Audit_UserDate").IsDescending(false, true);

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.EntityName).HasMaxLength(128);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.OccurredAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasOne(d => d.Branch).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_Audit_Branch");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Audit_User");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("PK__Branches__A1682FC57E0D79E7");

            entity.ToTable("Branches", "Core");

            entity.HasIndex(e => e.BranchCode, "UQ__Branches__1C61B88863EF1BDD").IsUnique();

            entity.Property(e => e.AddressLine).HasMaxLength(250);
            entity.Property(e => e.BranchCode).HasMaxLength(20);
            entity.Property(e => e.BranchName).HasMaxLength(150);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Province).HasMaxLength(100);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(0);
            entity.Property(e => e.Ward).HasMaxLength(100);
        });

        modelBuilder.Entity<BranchStock>(entity =>
        {
            entity.HasKey(e => new { e.BranchId, e.PartId });

            entity.ToTable("BranchStock", "Inventory");

            entity.Property(e => e.QuantityOnHand).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchStocks)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BStock_Branch");

            entity.HasOne(d => d.Part).WithMany(p => p.BranchStocks)
                .HasForeignKey(d => d.PartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BStock_Part");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64D87DA4D2C6");

            entity.ToTable("Customers", "CRM");

            entity.HasIndex(e => e.FullName, "IX_Customers_Name");

            entity.HasIndex(e => e.Phone, "IX_Customers_Phone");

            entity.HasIndex(e => e.CustomerCode, "UQ__Customer__0667852107EA7364").IsUnique();

            entity.Property(e => e.AddressLine).HasMaxLength(250);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerCode).HasMaxLength(30);
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Province).HasMaxLength(100);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(0);
            entity.Property(e => e.Ward).HasMaxLength(100);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Customers)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_Customers_CreatedBy");
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.DeviceId).HasName("PK__Devices__49E12311543B381B");

            entity.ToTable("Devices", "CRM");

            entity.HasIndex(e => e.CustomerId, "IX_Devices_Customer");

            entity.HasIndex(e => e.SerialNumber, "IX_Devices_Serial").HasFilter("([SerialNumber] IS NOT NULL)");

            entity.Property(e => e.AssetTag).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Cpu).HasMaxLength(150);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ModelName).HasMaxLength(150);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.OperatingSystem).HasMaxLength(150);
            entity.Property(e => e.RamDescription).HasMaxLength(100);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.StorageDescription).HasMaxLength(150);

            entity.HasOne(d => d.Customer).WithMany(p => p.Devices)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Devices_Customer");

            entity.HasOne(d => d.DeviceBrand).WithMany(p => p.Devices)
                .HasForeignKey(d => d.DeviceBrandId)
                .HasConstraintName("FK_Devices_Brand");
        });

        modelBuilder.Entity<DeviceBrand>(entity =>
        {
            entity.HasKey(e => e.DeviceBrandId).HasName("PK__DeviceBr__5BB24B3FC1868CE1");

            entity.ToTable("DeviceBrands", "CRM");

            entity.HasIndex(e => e.BrandName, "UQ__DeviceBr__2206CE9B5CCE6DAF").IsUnique();

            entity.Property(e => e.BrandName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<DiagnosisReport>(entity =>
        {
            entity.HasKey(e => e.DiagnosisReportId).HasName("PK__Diagnosi__5ADAB085F7C9BA1A");

            entity.ToTable("DiagnosisReports", "Repair");

            entity.HasIndex(e => new { e.RepairTicketId, e.DiagnosedAtUtc }, "IX_Diagnosis_Ticket_Date").IsDescending(false, true);

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiagnosedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Findings).HasMaxLength(3000);
            entity.Property(e => e.RecommendedActions).HasMaxLength(2000);
            entity.Property(e => e.SuspectedCauses).HasMaxLength(2000);
            entity.Property(e => e.Symptoms).HasMaxLength(2000);

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.DiagnosisReports)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnosis_Ticket");

            entity.HasOne(d => d.TechnicianUser).WithMany(p => p.DiagnosisReports)
                .HasForeignKey(d => d.TechnicianUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diagnosis_Tech");
        });

        modelBuilder.Entity<DiagnosticRun>(entity =>
        {
            entity.HasKey(e => e.DiagnosticRunId).HasName("PK__Diagnost__CAB9D5CE64D9E801");

            entity.ToTable("DiagnosticRuns", "Repair");

            entity.Property(e => e.AgentVersion).HasMaxLength(50);
            entity.Property(e => e.CollectedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ConsentRecordedAtUtc).HasPrecision(0);

            entity.HasOne(d => d.CollectedByUser).WithMany(p => p.DiagnosticRuns)
                .HasForeignKey(d => d.CollectedByUserId)
                .HasConstraintName("FK_DiagRun_User");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.DiagnosticRuns)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiagRun_Ticket");
        });

        modelBuilder.Entity<FileAttachment>(entity =>
        {
            entity.HasKey(e => e.FileId).HasName("PK__FileAtta__6F0F98BF5062511B");

            entity.ToTable("FileAttachments", "Core");

            entity.Property(e => e.ContentType).HasMaxLength(150);
            entity.Property(e => e.OriginalFileName).HasMaxLength(255);
            entity.Property(e => e.Sha256Hash).HasMaxLength(32);
            entity.Property(e => e.StorageKey).HasMaxLength(500);
            entity.Property(e => e.StorageProvider).HasMaxLength(30);
            entity.Property(e => e.UploadedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.FileAttachments)
                .HasForeignKey(d => d.UploadedByUserId)
                .HasConstraintName("FK_File_User");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoices__D796AAB584C11BF1");

            entity.ToTable("Invoices", "Billing");

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Invoices__D776E981EA649C27").IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceNumber).HasMaxLength(40);
            entity.Property(e => e.InvoiceType)
                .HasMaxLength(15)
                .HasDefaultValue("SALE");
            entity.Property(e => e.IssuedAtUtc).HasPrecision(0);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount)
                .HasComputedColumnSql("(([Subtotal]-[DiscountAmount])+[TaxAmount])", true)
                .HasColumnType("decimal(20, 2)");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_Invoice_User");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoice_Ticket");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12B22A4DDC");

            entity.ToTable("Notifications", "Comms");

            entity.HasIndex(e => new { e.Status, e.ScheduledAtUtc }, "IX_Notifications_Queue");

            entity.Property(e => e.Channel).HasMaxLength(15);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.ProviderMessageId).HasMaxLength(150);
            entity.Property(e => e.ScheduledAtUtc).HasPrecision(0);
            entity.Property(e => e.SentAtUtc).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("QUEUED");
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.TemplateCode).HasMaxLength(80);

            entity.HasOne(d => d.Customer).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Notification_Customer");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notification_User");
        });

        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasKey(e => e.PartId).HasName("PK__Parts__7C3F0D50373CF517");

            entity.ToTable("Parts", "Inventory");

            entity.HasIndex(e => e.PartName, "IX_Parts_Name");

            entity.HasIndex(e => e.PartCode, "UQ__Parts__6525D39DCD01C9AA").IsUnique();

            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DefaultCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DefaultSalePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PartCode).HasMaxLength(50);
            entity.Property(e => e.PartName).HasMaxLength(180);
            entity.Property(e => e.ReorderLevel).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Specification).HasMaxLength(500);
            entity.Property(e => e.Unit)
                .HasMaxLength(30)
                .HasDefaultValue("piece");

            entity.HasOne(d => d.PartCategory).WithMany(p => p.Parts)
                .HasForeignKey(d => d.PartCategoryId)
                .HasConstraintName("FK_Part_Category");
        });

        modelBuilder.Entity<PartCategory>(entity =>
        {
            entity.HasKey(e => e.PartCategoryId).HasName("PK__PartCate__BF6B7EC98DDDEBAC");

            entity.ToTable("PartCategories", "Inventory");

            entity.HasIndex(e => e.CategoryName, "UQ__PartCate__8517B2E073ECBE8E").IsUnique();

            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK_PartCategory_Parent");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38CA0D2BE7");

            entity.ToTable("Payments", "Billing");

            entity.HasIndex(e => new { e.InvoiceId, e.PaidAtUtc }, "IX_Payments_InvoiceDate").IsDescending(false, true);

            entity.HasIndex(e => e.PaymentCode, "UQ__Payments__106D3BA86BB6954D").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PaidAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PaymentCode).HasMaxLength(40);
            entity.Property(e => e.PaymentMethod).HasMaxLength(20);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .HasDefaultValue("COMPLETED");
            entity.Property(e => e.TransactionReference).HasMaxLength(100);

            entity.HasOne(d => d.Invoice).WithMany(p => p.Payments)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_Invoice");

            entity.HasOne(d => d.ReceivedByUser).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ReceivedByUserId)
                .HasConstraintName("FK_Payment_User");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("PK__Permissi__EFA6FB2FF233183C");

            entity.ToTable("Permissions", "Security");

            entity.HasIndex(e => e.PermissionCode, "UQ__Permissi__91FE57504B90BBB2").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.ModuleCode).HasMaxLength(50);
            entity.Property(e => e.PermissionCode).HasMaxLength(100);
            entity.Property(e => e.PermissionName).HasMaxLength(150);
        });

        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.HasKey(e => e.QuotationId).HasName("PK__Quotatio__E1975293CAE3EB37");

            entity.ToTable("Quotations", "Repair");

            entity.HasIndex(e => new { e.RepairTicketId, e.VersionNumber }, "UQ_Quote_TicketVersion").IsUnique();

            entity.HasIndex(e => e.QuotationCode, "UQ__Quotatio__5BC2F94DCFC4C823").IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerNote).HasMaxLength(1000);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IssuedAtUtc).HasPrecision(0);
            entity.Property(e => e.QuotationCode).HasMaxLength(30);
            entity.Property(e => e.QuotationType)
                .HasMaxLength(15)
                .HasDefaultValue("INITIAL");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount)
                .HasComputedColumnSql("(([Subtotal]-[DiscountAmount])+[TaxAmount])", true)
                .HasColumnType("decimal(20, 2)");
            entity.Property(e => e.ValidUntilUtc).HasPrecision(0);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Quotations)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_Quote_User");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.Quotations)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Quote_Ticket");
        });

        modelBuilder.Entity<QuotationApproval>(entity =>
        {
            entity.HasKey(e => e.QuotationApprovalId).HasName("PK__Quotatio__86B86872325A4F0E");

            entity.ToTable("QuotationApprovals", "Repair");

            entity.Property(e => e.CustomerNote).HasMaxLength(1000);
            entity.Property(e => e.Decision).HasMaxLength(20);
            entity.Property(e => e.DecisionAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DecisionChannel).HasMaxLength(20);

            entity.HasOne(d => d.Quotation).WithMany(p => p.QuotationApprovals)
                .HasForeignKey(d => d.QuotationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QApproval_Quote");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.QuotationApprovals)
                .HasForeignKey(d => d.RecordedByUserId)
                .HasConstraintName("FK_QApproval_User");
        });

        modelBuilder.Entity<QuotationItem>(entity =>
        {
            entity.HasKey(e => e.QuotationItemId).HasName("PK__Quotatio__4F97450E472E1D77");

            entity.ToTable("QuotationItems", "Repair");

            entity.HasIndex(e => e.QuotationId, "IX_QItems_Quote");

            entity.Property(e => e.CustomerDecision)
                .HasMaxLength(15)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ItemType).HasMaxLength(15);
            entity.Property(e => e.LineTotal)
                .HasComputedColumnSql("(CONVERT([decimal](18,2),[Quantity]*[UnitPrice])-[DiscountAmount])", true)
                .HasColumnType("decimal(19, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Quotation).WithMany(p => p.QuotationItems)
                .HasForeignKey(d => d.QuotationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QItem_Quote");

            entity.HasOne<Part>().WithMany()
                .HasForeignKey(d => d.PartId)
                .HasConstraintName("FK_QItem_Part");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            entity.ToTable("Sessions", "Security");

            entity.HasIndex(e => e.UserId, "IX_Sessions_User_Active").HasFilter("[RevokedAtUtc] IS NULL");

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LastUsedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.AbsoluteExpiresAtUtc).HasPrecision(0);
            entity.Property(e => e.RevokedAtUtc).HasPrecision(0);
            entity.Property(e => e.RevokeReason).HasMaxLength(50);
            entity.Property(e => e.CreatedByIp).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.DeviceLabel).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.User).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sessions_User");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId).HasName("PK__RefreshT__F5845E39673BE895");

            entity.ToTable("RefreshTokens", "Security");

            entity.HasIndex(e => new { e.UserId, e.ExpiresAtUtc }, "IX_RefreshTokens_User_Expiry");
            entity.HasIndex(e => new { e.SessionId, e.ExpiresAtUtc }, "IX_RefreshTokens_Session");
            entity.HasIndex(e => e.TokenHash, "UX_RefreshTokens_Hash").IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedByIp).HasMaxLength(45);
            entity.Property(e => e.LastUsedByIp).HasMaxLength(45);
            entity.Property(e => e.ExpiresAtUtc).HasPrecision(0);
            entity.Property(e => e.RotatedAtUtc).HasPrecision(0);
            entity.Property(e => e.RevokedAtUtc).HasPrecision(0);
            entity.Property(e => e.ReuseDetectedAtUtc).HasPrecision(0);
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(32);
            entity.Property(e => e.RevokedByIp).HasMaxLength(45);
            entity.Property(e => e.TokenHash).HasMaxLength(32);

            entity.HasOne(d => d.Session).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefreshTokens_Session");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RefreshTokens_User");
        });

        modelBuilder.Entity<RepairPart>(entity =>
        {
            entity.HasKey(e => e.RepairPartId).HasName("PK__RepairPa__1C1C4176A22D59C2");

            entity.ToTable("RepairParts", "Repair");

            entity.HasIndex(e => e.RepairTicketId, "IX_RParts_Ticket");

            entity.Property(e => e.InstalledAtUtc).HasPrecision(0);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitSalePrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.RepairParts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RPart_Branch");

            entity.HasOne(d => d.InstalledByUser).WithMany(p => p.RepairParts)
                .HasForeignKey(d => d.InstalledByUserId)
                .HasConstraintName("FK_RPart_User");

            entity.HasOne(d => d.Part).WithMany(p => p.RepairParts)
                .HasForeignKey(d => d.PartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RPart_Part");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.RepairParts)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RPart_Ticket");

            entity.HasOne(d => d.StockTransaction).WithMany(p => p.RepairParts)
                .HasForeignKey(d => d.StockTransactionId)
                .HasConstraintName("FK_RPart_ST");
        });

        modelBuilder.Entity<RepairTicket>(entity =>
        {
            entity.HasKey(e => e.RepairTicketId).HasName("PK__RepairTi__D8F944EAB52E9489");

            entity.ToTable("RepairTickets", "Repair");

            entity.HasIndex(e => new { e.BranchId, e.Status, e.ReceivedAtUtc }, "IX_Ticket_Branch_Status_Date").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.CustomerId, e.ReceivedAtUtc }, "IX_Ticket_Customer_Date").IsDescending(false, true);

            entity.HasIndex(e => new { e.DeviceId, e.ReceivedAtUtc }, "IX_Ticket_Device_Date").IsDescending(false, true);

            entity.HasIndex(e => e.TicketCode, "UQ__RepairTi__598CF7A31B1C6286").IsUnique();

            entity.HasIndex(e => e.TrackingCodeHash, "UX_RepairTickets_TrackingCodeHash")
                .IsUnique()
                .HasFilter("([TrackingCodeHash] IS NOT NULL)");

            entity.Property(e => e.Accessories).HasMaxLength(1000);
            entity.Property(e => e.ActualCompletionAtUtc).HasPrecision(0);
            entity.Property(e => e.ClosedAtUtc).HasPrecision(0);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerIssue).HasMaxLength(2000);
            entity.Property(e => e.ExpectedCompletionAtUtc).HasPrecision(0);
            entity.Property(e => e.InitialCondition).HasMaxLength(2000);
            entity.Property(e => e.InspectionFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IntakeNotes).HasMaxLength(1500);
            entity.Property(e => e.Priority)
                .HasMaxLength(15)
                .HasDefaultValue("NORMAL");
            entity.Property(e => e.ReceivedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("RECEIVED");
            entity.Property(e => e.TicketCode).HasMaxLength(30);
            entity.Property(e => e.TrackingCodeHash).HasMaxLength(32);
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(0);

            entity.HasOne(d => d.Appointment).WithMany(p => p.RepairTickets)
                .HasForeignKey(d => d.AppointmentId)
                .HasConstraintName("FK_Ticket_Appointment");

            entity.HasOne(d => d.AssignedTechnicianUser).WithMany(p => p.RepairTicketAssignedTechnicianUsers)
                .HasForeignKey(d => d.AssignedTechnicianUserId)
                .HasConstraintName("FK_Ticket_Tech");

            entity.HasOne(d => d.Branch).WithMany(p => p.RepairTickets)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Ticket_Branch");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.RepairTicketCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_Ticket_Creator");

            entity.HasOne(d => d.Customer).WithMany(p => p.RepairTickets)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Ticket_Customer");

            entity.HasOne(d => d.Device).WithMany(p => p.RepairTickets)
                .HasForeignKey(d => d.DeviceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Ticket_Device");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1AF1569A06");

            entity.ToTable("Roles", "Security");

            entity.HasIndex(e => e.RoleCode, "UQ__Roles__D62CB59C1DA259E5").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.RoleCode).HasMaxLength(50);
            entity.Property(e => e.RoleName).HasMaxLength(100);

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_RP_Permission"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_RP_Role"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId");
                        j.ToTable("RolePermissions", "Security");
                    });
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.StockTransactionId).HasName("PK__StockTra__C6E6C5CA51D8B00F");

            entity.ToTable("StockTransactions", "Inventory");

            entity.HasIndex(e => new { e.BranchId, e.PartId, e.PerformedAtUtc }, "IX_ST_BranchPartDate").IsDescending(false, false, true);

            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.PerformedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReferenceCode).HasMaxLength(50);
            entity.Property(e => e.TransactionType).HasMaxLength(20);
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Branch).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ST_Branch");

            entity.HasOne(d => d.Part).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.PartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ST_Part");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.PerformedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ST_User");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.RepairTicketId)
                .HasConstraintName("FK_ST_Ticket");

            entity.HasOne(d => d.Supplier).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_ST_Supplier");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B4B6A3E6C0");

            entity.ToTable("Suppliers", "Inventory");

            entity.HasIndex(e => e.SupplierCode, "UQ__Supplier__44BE981BB06CB9A1").IsUnique();

            entity.Property(e => e.AddressLine).HasMaxLength(250);
            entity.Property(e => e.ContactName).HasMaxLength(150);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.SupplierCode).HasMaxLength(30);
            entity.Property(e => e.SupplierName).HasMaxLength(180);
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.HasKey(e => new { e.RepairTicketId, e.FileId });

            entity.ToTable("TicketAttachments", "Repair");

            entity.Property(e => e.AttachmentCategory).HasMaxLength(20);
            entity.Property(e => e.Caption).HasMaxLength(300);

            entity.HasOne(d => d.File).WithMany(p => p.TicketAttachments)
                .HasForeignKey(d => d.FileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TA_File");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.TicketAttachments)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TA_Ticket");
        });

        modelBuilder.Entity<TicketStatusHistory>(entity =>
        {
            entity.HasKey(e => e.TicketStatusHistoryId).HasName("PK__TicketSt__6BBC8864FD244834");

            entity.ToTable("TicketStatusHistory", "Repair");

            entity.HasIndex(e => new { e.RepairTicketId, e.ChangedAtUtc }, "IX_TSH_Ticket_Date").IsDescending(false, true);

            entity.Property(e => e.ChangedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NewStatus).HasMaxLength(30);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.OldStatus).HasMaxLength(30);

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.TicketStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .HasConstraintName("FK_TSH_User");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.TicketStatusHistories)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TSH_Ticket");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C978DD7CE");

            entity.ToTable("Users", "Security");

            entity.HasIndex(e => e.NormalizedEmail, "UX_Users_Email")
                .IsUnique()
                .HasFilter("([NormalizedEmail] IS NOT NULL)");

            entity.HasIndex(e => e.NormalizedUserName, "UX_Users_UserName").IsUnique();

            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.DeletedAtUtc).HasPrecision(0);
            entity.Property(e => e.LastLoginAtUtc).HasPrecision(0);
            entity.Property(e => e.LockoutEnabled).HasDefaultValue(true);
            entity.Property(e => e.LockoutEndUtc).HasPrecision(0);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(254);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SecurityStamp).HasMaxLength(100);
            entity.Property(e => e.UpdatedAtUtc).HasPrecision(0);
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.Branch).WithMany(p => p.Users)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_Users_Branch");

            entity.HasOne(d => d.DeletedByUser).WithMany()
                .HasForeignKey(d => d.DeletedByUserId)
                .HasConstraintName("FK_Users_DeletedBy");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.ToTable("UserRoles", "Security");

            entity.Property(e => e.AssignedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.UserRoleAssignedByUsers)
                .HasForeignKey(d => d.AssignedByUserId)
                .HasConstraintName("FK_UserRoles_AssignedBy");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Role");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_User");
        });

        modelBuilder.Entity<VwBranchStockStatus>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_BranchStockStatus", "Inventory");

            entity.Property(e => e.BranchCode).HasMaxLength(20);
            entity.Property(e => e.BranchName).HasMaxLength(150);
            entity.Property(e => e.PartCode).HasMaxLength(50);
            entity.Property(e => e.PartName).HasMaxLength(180);
            entity.Property(e => e.QuantityOnHand).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ReorderLevel).HasColumnType("decimal(18, 3)");
        });

        modelBuilder.Entity<Warranty>(entity =>
        {
            entity.HasKey(e => e.WarrantyId).HasName("PK__Warranti__2ED31813E22234F4");

            entity.ToTable("Warranties", "Warranty");

            entity.HasIndex(e => new { e.EndAtUtc, e.Status }, "IX_Warranty_EndDate");

            entity.HasIndex(e => e.RepairTicketId, "IX_Warranty_Ticket");

            entity.HasIndex(e => e.WarrantyCode, "UQ__Warranti__551FF6B23579F742").IsUnique();

            entity.Property(e => e.CoverageDescription).HasMaxLength(1000);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EndAtUtc).HasPrecision(0);
            entity.Property(e => e.StartAtUtc).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.Terms).HasMaxLength(2000);
            entity.Property(e => e.WarrantyCode).HasMaxLength(40);

            entity.HasOne(d => d.RepairPart).WithMany(p => p.Warranties)
                .HasForeignKey(d => d.RepairPartId)
                .HasConstraintName("FK_Warranty_RepairPart");

            entity.HasOne(d => d.RepairTicket).WithMany(p => p.Warranties)
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warranty_Ticket");
        });

        modelBuilder.Entity<WarrantyClaim>(entity =>
        {
            entity.HasKey(e => e.WarrantyClaimId).HasName("PK__Warranty__F216752BD004A4D1");

            entity.ToTable("WarrantyClaims", "Warranty");

            entity.HasIndex(e => new { e.WarrantyId, e.ReceivedAtUtc }, "IX_Claims_WarrantyDate").IsDescending(false, true);

            entity.HasIndex(e => e.ClaimCode, "UQ__Warranty__17537BFC5DFFBB12").IsUnique();

            entity.Property(e => e.ClaimCode).HasMaxLength(40);
            entity.Property(e => e.CustomerDescription).HasMaxLength(2000);
            entity.Property(e => e.InspectionResult).HasMaxLength(2000);
            entity.Property(e => e.ReceivedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ResolutionNote).HasMaxLength(1500);
            entity.Property(e => e.ResolvedAtUtc).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("RECEIVED");

            entity.HasOne(d => d.HandledByUser).WithMany(p => p.WarrantyClaims)
                .HasForeignKey(d => d.HandledByUserId)
                .HasConstraintName("FK_Claim_User");

            entity.HasOne(d => d.Warranty).WithMany(p => p.WarrantyClaims)
                .HasForeignKey(d => d.WarrantyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Claim_Warranty");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
