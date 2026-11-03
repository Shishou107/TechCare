/* ============================================================================
   TechCare — Script KIEM TRA TRANG THAI DATABASE v3 (chi doc, khong sua)
   Ban v4: cap nhat theo ban FULL moi nhat (43 bang, gom migration 005)
   va them muc kiem tra rieng cho migration 005. Van chiu duoc DB trong:
   muc chua co se bao THIEU thay vi dung loi.
   Cach dung: SSMS -> ket noi .\SQLEXPRESS -> chon dung database TechCare
   (xem dong "DB dang chon" o ket qua dau tien) -> Execute (F5).
   ============================================================================ */

USE [TechCare];
GO

SET NOCOUNT ON;

IF OBJECT_ID(N'tempdb..#R') IS NOT NULL DROP TABLE #R;
CREATE TABLE #R (HangMuc nvarchar(120), TrangThai nvarchar(10), ChiTiet nvarchar(300));

INSERT #R SELECT N'DB dang chon', N'THONG TIN', DB_NAME();

-- 1. Schema (ban hoan chinh: 11)
INSERT #R SELECT N'So schema', CASE WHEN COUNT(*) = 11 THEN N'DAT' ELSE N'THIEU' END,
       N'Tim thay ' + CAST(COUNT(*) AS nvarchar(5)) + N'/11'
FROM sys.schemas
WHERE name IN (N'Core',N'Security',N'CRM',N'Repair',N'Inventory',N'Billing',N'Warranty',N'Comms',N'Audit',N'Ai',N'Shipping');

-- 2. Tong so bang (ban hoan chinh: 42)
INSERT #R SELECT N'Tong so bang', CASE WHEN COUNT(*) >= 43 THEN N'DAT' ELSE N'THIEU' END,
       N'Tim thay ' + CAST(COUNT(*) AS nvarchar(5)) + N'/42 bang'
FROM sys.tables;

-- 3. Migration 002: Security.Sessions
INSERT #R SELECT N'[002] Bang Security.Sessions',
       CASE WHEN OBJECT_ID(N'Security.Sessions', N'U') IS NOT NULL THEN N'DAT' ELSE N'THIEU' END,
       CASE WHEN OBJECT_ID(N'Security.Sessions', N'U') IS NOT NULL THEN N'Da co' ELSE N'Chua chay migration 002' END;

-- 4. Migration 002: Users soft-delete
INSERT #R SELECT N'[002] Users.DeletedAtUtc + DeletedByUserId',
       CASE WHEN COL_LENGTH(N'Security.Users', N'DeletedAtUtc') IS NOT NULL
             AND COL_LENGTH(N'Security.Users', N'DeletedByUserId') IS NOT NULL THEN N'DAT' ELSE N'THIEU' END,
       N'Neu chua co bang Users thi muc nay THIEU la dung';

-- 5. Migration 002: RefreshTokens gan session
INSERT #R SELECT N'[002] RefreshTokens.SessionId + RotatedAtUtc + ReuseDetectedAtUtc + LastUsedByIp',
       CASE WHEN COL_LENGTH(N'Security.RefreshTokens', N'SessionId') IS NOT NULL
             AND COL_LENGTH(N'Security.RefreshTokens', N'RotatedAtUtc') IS NOT NULL
             AND COL_LENGTH(N'Security.RefreshTokens', N'ReuseDetectedAtUtc') IS NOT NULL
             AND COL_LENGTH(N'Security.RefreshTokens', N'LastUsedByIp') IS NOT NULL THEN N'DAT' ELSE N'THIEU' END,
       N'Xem chi tiet tung cot bang truy van tay neu can';

-- 6. Migration 002: TrackingCodeHash filtered unique index
INSERT #R SELECT N'[002] Index UX_RepairTickets_TrackingCodeHash (filtered unique)',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE name = N'UX_RepairTickets_TrackingCodeHash'
                           AND object_id = OBJECT_ID(N'Repair.RepairTickets')
                           AND is_unique = 1 AND has_filter = 1) THEN N'DAT' ELSE N'THIEU' END,
       N'Can unique index co filter [TrackingCodeHash] IS NOT NULL';

-- 7. Migration 003: bang Ai (4 bang)
INSERT #R SELECT N'[003] Bang schema Ai (4 bang)',
       CASE WHEN COUNT(*) = 4 THEN N'DAT' ELSE N'THIEU' END,
       N'Tim thay ' + CAST(COUNT(*) AS nvarchar(5)) + N'/4'
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'Ai';

-- 8. Migration 003: bang Shipping (2 bang)
INSERT #R SELECT N'[003] Bang schema Shipping (2 bang)',
       CASE WHEN COUNT(*) = 2 THEN N'DAT' ELSE N'THIEU' END,
       N'Tim thay ' + CAST(COUNT(*) AS nvarchar(5)) + N'/2'
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'Shipping';

-- 9. Migration 004: toa do hai dau cua Shipments
INSERT #R SELECT N'[004] Shipments co 4 cot toa do',
       CASE WHEN COL_LENGTH(N'Shipping.Shipments', N'SenderLatitude') IS NOT NULL
             AND COL_LENGTH(N'Shipping.Shipments', N'SenderLongitude') IS NOT NULL
             AND COL_LENGTH(N'Shipping.Shipments', N'ReceiverLatitude') IS NOT NULL
             AND COL_LENGTH(N'Shipping.Shipments', N'ReceiverLongitude') IS NOT NULL THEN N'DAT' ELSE N'THIEU' END,
       N'Neu chua co bang Shipments thi muc nay THIEU la dung';

-- 10. Role SUPPORT (doc du lieu that — chay dong, chi khi bang Roles ton tai)
IF OBJECT_ID(N'Security.Roles', N'U') IS NOT NULL
    EXEC sp_executesql N'
        INSERT #R SELECT N''[003] Role SUPPORT'',
               CASE WHEN EXISTS (SELECT 1 FROM Security.Roles WHERE RoleCode = N''SUPPORT'') THEN N''DAT'' ELSE N''THIEU'' END,
               N''Tong so role: '' + CAST((SELECT COUNT(*) FROM Security.Roles) AS nvarchar(5)) + N'' (ban chuan: 8)'';';
ELSE
    INSERT #R SELECT N'[003] Role SUPPORT', N'THIEU', N'Chua co bang Security.Roles — DB chua duoc tao schema';

-- 11. Permission moi (doc du lieu that — chay dong)
IF OBJECT_ID(N'Security.Permissions', N'U') IS NOT NULL
    EXEC sp_executesql N'
        INSERT #R SELECT N''[003] Permission AI.* + SHIPPING.* (6) / tong 27'',
               CASE WHEN (SELECT COUNT(*) FROM Security.Permissions WHERE PermissionCode LIKE N''AI.%'' OR PermissionCode LIKE N''SHIPPING.%'') = 6
                     AND (SELECT COUNT(*) FROM Security.Permissions) = 27 THEN N''DAT'' ELSE N''THIEU'' END,
               N''Moi: '' + CAST((SELECT COUNT(*) FROM Security.Permissions WHERE PermissionCode LIKE N''AI.%'' OR PermissionCode LIKE N''SHIPPING.%'') AS nvarchar(5))
               + N''/6, tong: '' + CAST((SELECT COUNT(*) FROM Security.Permissions) AS nvarchar(5)) + N''/27'';';
ELSE
    INSERT #R SELECT N'[003] Permission AI.* + SHIPPING.* (6) / tong 27', N'THIEU', N'Chua co bang Security.Permissions';

-- 12. View ton kho
INSERT #R SELECT N'View Inventory.vw_BranchStockStatus',
       CASE WHEN OBJECT_ID(N'Inventory.vw_BranchStockStatus', N'V') IS NOT NULL THEN N'DAT' ELSE N'THIEU' END,
       N'View canh bao ton kho';

-- 13. Du lieu nen (doc du lieu that — chay dong)
IF OBJECT_ID(N'Core.Branches', N'U') IS NOT NULL AND OBJECT_ID(N'Security.Users', N'U') IS NOT NULL
    EXEC sp_executesql N'
        INSERT #R SELECT N''Du lieu nen (Branch 1 + user admin)'',
               CASE WHEN EXISTS (SELECT 1 FROM Core.Branches WHERE BranchId = 1)
                     AND EXISTS (SELECT 1 FROM Security.Users WHERE UserName = N''admin'') THEN N''DAT'' ELSE N''THIEU'' END,
               N''Can cho API chay va dang nhap lan dau'';';
ELSE
    INSERT #R SELECT N'Du lieu nen (Branch 1 + user admin)', N'THIEU', N'Chua co bang Core.Branches / Security.Users';

-- 14. Migration 005: tai khoan khach + ky thuat vien
INSERT #R SELECT N'[005] OtpCodes + TechnicianProfiles + TicketRatings + Customers.UserId + PreferredTechnicianUserId',
       CASE WHEN OBJECT_ID(N'Security.OtpCodes', N'U') IS NOT NULL
             AND OBJECT_ID(N'Security.TechnicianProfiles', N'U') IS NOT NULL
             AND OBJECT_ID(N'Repair.TicketRatings', N'U') IS NOT NULL
             AND COL_LENGTH(N'CRM.Customers', N'UserId') IS NOT NULL
             AND COL_LENGTH(N'CRM.Appointments', N'PreferredTechnicianUserId') IS NOT NULL
             AND COL_LENGTH(N'Repair.RepairTickets', N'PreferredTechnicianUserId') IS NOT NULL
            THEN N'DAT' ELSE N'THIEU' END,
       N'Neu dung file FULL moi nhat thi muc nay co san sau khi cai DB';

-- 15. Migration 006: EmailTokens + DeliveryMethod + PickupAddress
INSERT #R SELECT N'[006] EmailTokens + Appointments.DeliveryMethod + Appointments.PickupAddress',
       CASE WHEN OBJECT_ID(N'Security.EmailTokens', N'U') IS NOT NULL
             AND COL_LENGTH(N'CRM.Appointments', N'DeliveryMethod') IS NOT NULL
             AND COL_LENGTH(N'CRM.Appointments', N'PickupAddress') IS NOT NULL
            THEN N'DAT' ELSE N'THIEU' END,
       N'Ho tro dang ky email, dat lai mat khau va hinh thuc gui may';

SELECT HangMuc AS [Hạng Mục], TrangThai AS [Trạng Thái], ChiTiet AS [Chi Tiết] FROM #R;
DROP TABLE #R;
GO
