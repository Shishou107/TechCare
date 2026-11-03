using API_FixComputerCenter.Domain.Entities.Shipping;
using API_FixComputerCenter.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Infrastructure.Persistence;

public partial class TechCareDbContext
{
    public virtual DbSet<Shipment> Shipments { get; set; } = null!;
    public virtual DbSet<ShipmentEvent> ShipmentEvents { get; set; } = null!;
    public virtual DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; } = null!;
    public virtual DbSet<KnowledgeChunk> KnowledgeChunks { get; set; } = null!;
    public virtual DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public virtual DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // 1. Shipping Schema
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("Shipments", "Shipping");
            entity.HasKey(e => e.ShipmentId);

            entity.Property(e => e.CarrierCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.WaybillCode).HasMaxLength(60).IsRequired();
            entity.Property(e => e.Direction).HasMaxLength(10).IsRequired();

            entity.Property(e => e.SenderName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.SenderPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SenderAddress).HasMaxLength(500).IsRequired();
            entity.Property(e => e.SenderProvinceCode).HasMaxLength(20);
            entity.Property(e => e.SenderDistrictCode).HasMaxLength(20);
            entity.Property(e => e.SenderWardCode).HasMaxLength(30);

            entity.Property(e => e.ReceiverName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ReceiverPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReceiverAddress).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ReceiverProvinceCode).HasMaxLength(20);
            entity.Property(e => e.ReceiverDistrictCode).HasMaxLength(20);
            entity.Property(e => e.ReceiverWardCode).HasMaxLength(30);

            entity.Property(e => e.CodAmount).HasColumnType("decimal(18,0)").HasDefaultValue(0);
            entity.Property(e => e.InsuredValue).HasColumnType("decimal(18,0)").HasDefaultValue(0);
            entity.Property(e => e.FeeAmount).HasColumnType("decimal(18,0)");
            entity.Property(e => e.FeePayer).HasMaxLength(10);
            entity.Property(e => e.StatusCode).HasMaxLength(30).HasDefaultValue("CREATED").IsRequired();

            entity.HasIndex(e => new { e.CarrierCode, e.WaybillCode }, "UX_Shipments_Carrier_Waybill").IsUnique();
            entity.HasIndex(e => e.AppointmentId, "IX_Shipments_Appointment");
            entity.HasIndex(e => e.RepairTicketId, "IX_Shipments_Ticket");

            entity.HasOne(d => d.Appointment)
                .WithMany()
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.RepairTicket)
                .WithMany()
                .HasForeignKey(d => d.RepairTicketId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShipmentEvent>(entity =>
        {
            entity.ToTable("ShipmentEvents", "Shipping");
            entity.HasKey(e => e.EventId);

            entity.Property(e => e.ProviderStatus).HasMaxLength(60).IsRequired();
            entity.Property(e => e.InternalStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.LocationText).HasMaxLength(300);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9,6)");

            // Unique index for deduplication of webhook retries
            entity.HasIndex(e => new { e.ShipmentId, e.ProviderStatus, e.OccurredAtUtc }, "UX_ShipmentEvents_Dedup").IsUnique();
            entity.HasIndex(e => new { e.ShipmentId, e.OccurredAtUtc }, "IX_ShipmentEvents_Shipment_Time");

            entity.HasOne(d => d.Shipment)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 2. AI Schema (Knowledge Documents, Chunks, Chat Sessions, Chat Messages)
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("KnowledgeDocuments", "Ai");
            entity.HasKey(e => e.DocumentId);

            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.SourceType).HasMaxLength(20).HasDefaultValue("TEXT").IsRequired();
            entity.Property(e => e.OriginalFileName).HasMaxLength(260);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.ToTable("KnowledgeChunks", "Ai");
            entity.HasKey(e => e.ChunkId);

            entity.Property(e => e.EmbeddingModel).HasMaxLength(100);
            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex }, "UQ_KnowledgeChunks_Doc_Index").IsUnique();

            entity.HasOne(d => d.Document)
                .WithMany(p => p.Chunks)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("ChatSessions", "Ai");
            entity.HasKey(e => e.SessionId);

            entity.Property(e => e.GuestName).HasMaxLength(150);
            entity.Property(e => e.GuestPhone).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("BOT").IsRequired();

            entity.HasIndex(e => new { e.Status, e.LastMessageAtUtc }, "IX_ChatSessions_Status_LastMsg");

            entity.HasOne(d => d.Customer)
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.AssignedSupportUser)
                .WithMany()
                .HasForeignKey(d => d.AssignedSupportId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages", "Ai");
            entity.HasKey(e => e.MessageId);

            entity.Property(e => e.SenderType).HasMaxLength(20).IsRequired();

            entity.HasIndex(e => new { e.SessionId, e.CreatedAtUtc }, "IX_ChatMessages_Session_Time");

            entity.HasOne(d => d.Session)
                .WithMany(p => p.Messages)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.SenderUser)
                .WithMany()
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
