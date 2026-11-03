/*
  TechCare - Migration 002: Sessions, Token Rotation, SecurityStamp & Concurrency
  Target: SQL Server 2019+
  IDEMPOTENT: Safe to run multiple times without data loss or error.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
BEGIN TRANSACTION;

-- 1. Ensure Schema Security exists
IF SCHEMA_ID(N'Security') IS NULL EXEC(N'CREATE SCHEMA Security');

-- 2. Add deleted user columns to Security.Users
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Security.Users') AND name = N'DeletedAtUtc')
BEGIN
    ALTER TABLE Security.Users ADD DeletedAtUtc datetime2(0) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Security.Users') AND name = N'DeletedByUserId')
BEGIN
    ALTER TABLE Security.Users ADD DeletedByUserId bigint NULL CONSTRAINT FK_Users_DeletedBy REFERENCES Security.Users(UserId);
END
GO

-- 3. Create Security.Sessions table
IF OBJECT_ID(N'Security.Sessions', N'U') IS NULL
BEGIN
    CREATE TABLE Security.Sessions(
        SessionId            uniqueidentifier NOT NULL PRIMARY KEY,
        UserId               bigint NOT NULL CONSTRAINT FK_Sessions_User REFERENCES Security.Users(UserId),
        CreatedAtUtc         datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        LastUsedAtUtc        datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        AbsoluteExpiresAtUtc datetime2(0) NOT NULL,
        RevokedAtUtc         datetime2(0) NULL,
        RevokeReason         nvarchar(50) NULL,
        CreatedByIp          nvarchar(45) NULL,
        UserAgent            nvarchar(500) NULL,
        DeviceLabel          nvarchar(200) NULL,
        RowVersion           rowversion
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sessions_User_Active' AND object_id = OBJECT_ID(N'Security.Sessions'))
BEGIN
    CREATE INDEX IX_Sessions_User_Active ON Security.Sessions(UserId) WHERE RevokedAtUtc IS NULL;
END
GO

-- 4. Alter Security.RefreshTokens table
IF OBJECT_ID(N'Security.RefreshTokens', N'U') IS NOT NULL
BEGIN
    -- Check if SessionId exists, if not add it
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Security.RefreshTokens') AND name = N'SessionId')
    BEGIN
        -- If table already has rows, ensure default session or create one
        ALTER TABLE Security.RefreshTokens ADD SessionId uniqueidentifier NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Security.RefreshTokens') AND name = N'RotatedAtUtc')
    BEGIN
        ALTER TABLE Security.RefreshTokens ADD RotatedAtUtc datetime2(0) NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Security.RefreshTokens') AND name = N'ReuseDetectedAtUtc')
    BEGIN
        ALTER TABLE Security.RefreshTokens ADD ReuseDetectedAtUtc datetime2(0) NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Security.RefreshTokens') AND name = N'LastUsedByIp')
    BEGIN
        ALTER TABLE Security.RefreshTokens ADD LastUsedByIp nvarchar(45) NULL;
    END
END
GO

-- 5. Fix RepairTickets.TrackingCodeHash filtered unique index
IF OBJECT_ID(N'Repair.RepairTickets', N'U') IS NOT NULL
BEGIN
    -- Drop old un-filtered unique constraint/index if it exists
    DECLARE @uqName nvarchar(200);
    SELECT @uqName = name FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'Repair.RepairTickets') 
      AND (name LIKE 'UQ__RepairTi__CEB02FAA%' OR name = 'UQ_RepairTickets_TrackingCodeHash')
      AND has_filter = 0;

    IF @uqName IS NOT NULL
    BEGIN
        DECLARE @dropSql nvarchar(300) = N'DROP INDEX ' + QUOTENAME(@uqName) + N' ON Repair.RepairTickets';
        EXEC sp_executesql @dropSql;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RepairTickets_TrackingCodeHash' AND object_id = OBJECT_ID(N'Repair.RepairTickets'))
    BEGIN
        CREATE UNIQUE INDEX UX_RepairTickets_TrackingCodeHash 
            ON Repair.RepairTickets(TrackingCodeHash) 
            WHERE TrackingCodeHash IS NOT NULL;
    END
END
GO

-- 6. Add missing Foreign Keys
IF OBJECT_ID(N'Repair.QuotationItems', N'U') IS NOT NULL AND OBJECT_ID(N'Inventory.Parts', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QItem_Part')
    BEGIN
        ALTER TABLE Repair.QuotationItems ADD CONSTRAINT FK_QItem_Part FOREIGN KEY(PartId) REFERENCES Inventory.Parts(PartId);
    END
END
GO

IF OBJECT_ID(N'Repair.DiagnosticRuns', N'U') IS NOT NULL AND OBJECT_ID(N'Core.FileAttachments', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DiagRun_ReportFile')
    BEGIN
        ALTER TABLE Repair.DiagnosticRuns ADD CONSTRAINT FK_DiagRun_ReportFile FOREIGN KEY(ReportFileId) REFERENCES Core.FileAttachments(FileId);
    END
END
GO

-- 7. Ensure Baseline Seed Data
IF OBJECT_ID(N'Core.Branches', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Core.Branches WHERE BranchId = 1)
BEGIN
    SET IDENTITY_INSERT Core.Branches ON;
    INSERT Core.Branches(BranchId, BranchCode, BranchName, Phone, Email, AddressLine, Province, IsActive)
    VALUES (1, N'TC-CENTRAL', N'TechCare Chi Nhánh Trung Tâm', N'1900-6789', N'contact@techcare.vn', N'123 Nguyễn Tri Phương', N'Đà Nẵng', 1);
    SET IDENTITY_INSERT Core.Branches OFF;
END
GO

IF OBJECT_ID(N'Security.Users', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Security.Users WHERE UserName = N'admin')
BEGIN
    SET IDENTITY_INSERT Security.Users ON;
    INSERT Security.Users(UserId, BranchId, UserName, NormalizedUserName, Email, NormalizedEmail, PhoneNumber, PasswordHash, SecurityStamp, IsEmailConfirmed, IsActive, CreatedAtUtc)
    VALUES (1, 1, N'admin', N'ADMIN', N'admin@techcare.vn', N'ADMIN@TECHCARE.VN', N'0901234567', N'0bZGvX8uYKmn1Mu6r6HBUw==:5eRLtAlFVoP/X4yOMtIwYu/zw4JXVjVx2LAHrgzGLGc=', CONVERT(nvarchar(100), NEWID()), 1, 1, SYSUTCDATETIME());
    SET IDENTITY_INSERT Security.Users OFF;

    DECLARE @adminRoleId int = (SELECT RoleId FROM Security.Roles WHERE RoleCode = N'ADMIN');
    IF @adminRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Security.UserRoles WHERE UserId = 1 AND RoleId = @adminRoleId)
    BEGIN
        INSERT Security.UserRoles(UserId, RoleId, AssignedAtUtc) VALUES (1, @adminRoleId, SYSUTCDATETIME());
    END
END
GO

COMMIT TRANSACTION;
PRINT N'TechCare Migration 002 (Sessions & Security) applied successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

