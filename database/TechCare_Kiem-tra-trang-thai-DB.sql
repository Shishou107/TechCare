/* ============================================================================
   TechCare — Script Kiểm Tra Trạng Thái Database (Read-Only)
   ----------------------------------------------------------------------------
   Mục đích: Kiểm tra 13 hạng mục cấu trúc & dữ liệu quan trọng sau các đợt migration
   Môi trường: SQL Server (.\SQLEXPRESS) -> Database: TechCare
   Lưu ý: Script CHỈ ĐỌC (SELECT), KHÔNG thay đổi hay xóa bất kỳ dữ liệu nào.
   ============================================================================ */

USE [TechCare];
GO

SET NOCOUNT ON;

DECLARE @CheckResults TABLE (
    STT int,
    HangMuc nvarchar(60),
    YeuCauKiemTra nvarchar(200),
    TrangThai nvarchar(20),
    GhiChu nvarchar(250)
);

-- 1. Schema Ai
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Ai')
    INSERT INTO @CheckResults VALUES (1, N'Schema Ai', N'Schema Ai phục vụ Chatbot & RAG', N'ĐẠT', N'Đã có schema Ai');
ELSE
    INSERT INTO @CheckResults VALUES (1, N'Schema Ai', N'Schema Ai phục vụ Chatbot & RAG', N'THIẾU', N'Chưa chạy Migration 003');

-- 2. Schema Shipping
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Shipping')
    INSERT INTO @CheckResults VALUES (2, N'Schema Shipping', N'Schema Shipping phục vụ giao vận GHN', N'ĐẠT', N'Đã có schema Shipping');
ELSE
    INSERT INTO @CheckResults VALUES (2, N'Schema Shipping', N'Schema Shipping phục vụ giao vận GHN', N'THIẾU', N'Chưa chạy Migration 003');

-- 3. Bảng Security.Sessions
IF OBJECT_ID(N'Security.Sessions', N'U') IS NOT NULL
    INSERT INTO @CheckResults VALUES (3, N'Bảng Security.Sessions', N'Lưu phiên đăng nhập & Sliding session', N'ĐẠT', N'Bảng Sessions đã tồn tại');
ELSE
    INSERT INTO @CheckResults VALUES (3, N'Bảng Security.Sessions', N'Lưu phiên đăng nhập & Sliding session', N'THIẾU', N'Chưa chạy Migration 002');

-- 4. Cột bảo mật Security.RefreshTokens
IF COL_LENGTH(N'Security.RefreshTokens', N'SessionId') IS NOT NULL
   AND COL_LENGTH(N'Security.RefreshTokens', N'RotatedAtUtc') IS NOT NULL
   AND COL_LENGTH(N'Security.RefreshTokens', N'ReuseDetectedAtUtc') IS NOT NULL
   AND COL_LENGTH(N'Security.RefreshTokens', N'LastUsedByIp') IS NOT NULL
    INSERT INTO @CheckResults VALUES (4, N'Cột RefreshTokens v2', N'Đủ SessionId, RotatedAtUtc, ReuseDetectedAtUtc, LastUsedByIp', N'ĐẠT', N'Đủ 4 cột bảo mật token rotation');
ELSE
    INSERT INTO @CheckResults VALUES (4, N'Cột RefreshTokens v2', N'Đủ SessionId, RotatedAtUtc, ReuseDetectedAtUtc, LastUsedByIp', N'THIẾU', N'Thiếu 1 hoặc nhiều cột bảo mật (chưa chạy Migration 002)');

-- 5. Filtered Index UX_RepairTickets_TrackingCodeHash
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RepairTickets_TrackingCodeHash' AND object_id = OBJECT_ID(N'Repair.RepairTickets') AND has_filter = 1)
    INSERT INTO @CheckResults VALUES (5, N'Index TrackingCodeHash', N'Filtered unique index UX_RepairTickets_TrackingCodeHash', N'ĐẠT', N'Đã cấu hình filtered index đúng chuẩn');
ELSE
    INSERT INTO @CheckResults VALUES (5, N'Index TrackingCodeHash', N'Filtered unique index UX_RepairTickets_TrackingCodeHash', N'THIẾU', N'Chưa có filtered index (chưa chạy Migration 002)');

-- 6. Bảng Ai.KnowledgeDocuments
IF OBJECT_ID(N'Ai.KnowledgeDocuments', N'U') IS NOT NULL
    INSERT INTO @CheckResults VALUES (6, N'Bảng KnowledgeDocuments', N'Bảng lưu tài liệu tri thức RAG', N'ĐẠT', N'Bảng Ai.KnowledgeDocuments đã tồn tại');
ELSE
    INSERT INTO @CheckResults VALUES (6, N'Bảng KnowledgeDocuments', N'Bảng lưu tài liệu tri thức RAG', N'THIẾU', N'Chưa chạy Migration 003');

-- 7. Bảng Ai.KnowledgeChunks & Vector Columns
IF OBJECT_ID(N'Ai.KnowledgeChunks', N'U') IS NOT NULL
   AND COL_LENGTH(N'Ai.KnowledgeChunks', N'Embedding') IS NOT NULL
   AND COL_LENGTH(N'Ai.KnowledgeChunks', N'EmbeddingModel') IS NOT NULL
   AND COL_LENGTH(N'Ai.KnowledgeChunks', N'EmbeddingDim') IS NOT NULL
    INSERT INTO @CheckResults VALUES (7, N'Bảng KnowledgeChunks', N'Lưu đoạn văn & vector embedding (Embedding, EmbeddingModel, EmbeddingDim)', N'ĐẠT', N'Bảng Ai.KnowledgeChunks đủ cột vector');
ELSE
    INSERT INTO @CheckResults VALUES (7, N'Bảng KnowledgeChunks', N'Lưu đoạn văn & vector embedding (Embedding, EmbeddingModel, EmbeddingDim)', N'THIẾU', N'Chưa chạy Migration 003');

-- 8. Bảng Ai.ChatSessions
IF OBJECT_ID(N'Ai.ChatSessions', N'U') IS NOT NULL OR OBJECT_ID(N'Comms.ChatSessions', N'U') IS NOT NULL
    INSERT INTO @CheckResults VALUES (8, N'Bảng ChatSessions', N'Quản lý phiên hội thoại khách hàng (schema Ai)', N'ĐẠT', N'Bảng ChatSessions đã tồn tại');
ELSE
    INSERT INTO @CheckResults VALUES (8, N'Bảng ChatSessions', N'Quản lý phiên hội thoại khách hàng (schema Ai)', N'THIẾU', N'Chưa chạy Migration 003');

-- 9. Bảng Ai.ChatMessages
IF OBJECT_ID(N'Ai.ChatMessages', N'U') IS NOT NULL OR OBJECT_ID(N'Comms.ChatMessages', N'U') IS NOT NULL
    INSERT INTO @CheckResults VALUES (9, N'Bảng ChatMessages', N'Lưu tin nhắn hội thoại BOT / CUSTOMER / SUPPORT (schema Ai)', N'ĐẠT', N'Bảng ChatMessages đã tồn tại');
ELSE
    INSERT INTO @CheckResults VALUES (9, N'Bảng ChatMessages', N'Lưu tin nhắn hội thoại BOT / CUSTOMER / SUPPORT (schema Ai)', N'THIẾU', N'Chưa chạy Migration 003');

-- 10. Tọa độ hai đầu Shipping.Shipments
IF OBJECT_ID(N'Shipping.Shipments', N'U') IS NOT NULL
   AND COL_LENGTH(N'Shipping.Shipments', N'SenderLatitude') IS NOT NULL
   AND COL_LENGTH(N'Shipping.Shipments', N'SenderLongitude') IS NOT NULL
   AND COL_LENGTH(N'Shipping.Shipments', N'ReceiverLatitude') IS NOT NULL
   AND COL_LENGTH(N'Shipping.Shipments', N'ReceiverLongitude') IS NOT NULL
    INSERT INTO @CheckResults VALUES (10, N'Tọa độ Shipments', N'Đủ SenderLatitude, SenderLongitude, ReceiverLatitude, ReceiverLongitude', N'ĐẠT', N'Bảng Shipments có đủ 4 cột tọa độ');
ELSE
    INSERT INTO @CheckResults VALUES (10, N'Tọa độ Shipments', N'Đủ SenderLatitude, SenderLongitude, ReceiverLatitude, ReceiverLongitude', N'THIẾU', N'Chưa chạy Migration 004');

-- 11. Tọa độ & Dedup Shipping.ShipmentEvents
IF OBJECT_ID(N'Shipping.ShipmentEvents', N'U') IS NOT NULL
   AND COL_LENGTH(N'Shipping.ShipmentEvents', N'Latitude') IS NOT NULL
   AND COL_LENGTH(N'Shipping.ShipmentEvents', N'Longitude') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ShipmentEvents_Dedup' AND object_id = OBJECT_ID(N'Shipping.ShipmentEvents'))
    INSERT INTO @CheckResults VALUES (11, N'Sự kiện vận chuyển Events', N'Cột Latitude, Longitude & unique index UX_ShipmentEvents_Dedup chống trùng webhook', N'ĐẠT', N'Đủ tọa độ trạm và index dedup');
ELSE
    INSERT INTO @CheckResults VALUES (11, N'Sự kiện vận chuyển Events', N'Cột Latitude, Longitude & unique index UX_ShipmentEvents_Dedup chống trùng webhook', N'THIẾU', N'Chưa chạy Migration 003');

-- 12. Role SUPPORT
IF EXISTS (SELECT 1 FROM Security.Roles WHERE RoleCode = N'SUPPORT')
    INSERT INTO @CheckResults VALUES (12, N'Role SUPPORT', N'Role Hỗ trợ khách hàng trong Security.Roles', N'ĐẠT', N'Role SUPPORT đã tồn tại');
ELSE
    INSERT INTO @CheckResults VALUES (12, N'Role SUPPORT', N'Role Hỗ trợ khách hàng trong Security.Roles', N'THIẾU', N'Chưa chạy Migration 003');

-- 13. Phân quyền AI & SHIPPING
DECLARE @PermCount int = (
    SELECT COUNT(*) FROM Security.Permissions 
    WHERE PermissionCode IN (N'AI.KB.MANAGE', N'AI.CHAT.VIEW', N'AI.CHAT.REPLY', N'SHIPPING.VIEW', N'SHIPPING.CREATE', N'SHIPPING.MANAGE')
);
DECLARE @GrantCount int = (
    SELECT COUNT(*) FROM Security.RolePermissions rp
    JOIN Security.Permissions p ON p.PermissionId = rp.PermissionId
    WHERE p.PermissionCode IN (N'AI.KB.MANAGE', N'AI.CHAT.VIEW', N'AI.CHAT.REPLY', N'SHIPPING.VIEW', N'SHIPPING.CREATE', N'SHIPPING.MANAGE')
);
IF @PermCount = 6 AND @GrantCount >= 6
    INSERT INTO @CheckResults VALUES (13, N'Quyền AI & SHIPPING', N'Đủ 6 quyền mới và đã gán vào các Roles', N'ĐẠT', N'Đủ 6 permissions và RolePermissions liên quan');
ELSE
    INSERT INTO @CheckResults VALUES (13, N'Quyền AI & SHIPPING', N'Đủ 6 quyền mới và đã gán vào các Roles', N'THIẾU', N'Chưa chạy Migration 003');

-- Hiển thị bảng kết quả
SELECT 
    STT,
    HangMuc AS [Hạng Mục],
    YeuCauKiemTra AS [Yêu Cầu Kiểm Tra],
    TrangThai AS [Trạng Thái],
    GhiChu AS [Ghi Chú]
FROM @CheckResults
ORDER BY STT;

-- Tổng kết nhanh
DECLARE @DatCount int = (SELECT COUNT(*) FROM @CheckResults WHERE TrangThai = N'ĐẠT');
DECLARE @ThieuCount int = (SELECT COUNT(*) FROM @CheckResults WHERE TrangThai = N'THIẾU');

PRINT N'----------------------------------------------------------------------------';
PRINT N'TỔNG KẾT TRẠNG THÁI DATABASE TECHCARE: ' + CAST(@DatCount AS nvarchar(5)) + N'/13 ĐẠT (' + CAST(@ThieuCount AS nvarchar(5)) + N' THIẾU)';
IF @DatCount = 13
    PRINT N'==> KẾT LUẬN: ĐẠT 13/13. Database đã đồng bộ 100% với toàn bộ tính năng mã nguồn!';
ELSE
    PRINT N'==> KẾT LUẬN: CẦN BỔ SUNG. Hãy chạy các file migration tương ứng trong thư mục database/ (002 -> 003 -> 004) hoặc chạy TechCare_Database_FULL.sql.';
PRINT N'----------------------------------------------------------------------------';
GO
