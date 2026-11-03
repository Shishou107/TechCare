/* ============================================================================
   TechCare — Migration 006: Tài Khoản Khách Hàng (Email & Mật Khẩu), 
   Kỹ Thuật Viên Tự Động & Đơn Hàng Đầy Đủ Yếu Tố
   ----------------------------------------------------------------------------
   Mục đích:
   1. Đợt 6A: Tạo bảng Security.EmailTokens lưu mã xác thực 6 số (băm SHA-256)
      cho Xác thực Email & Quên/Đặt lại mật khẩu.
   2. Đợt 6B: Backfill hồ sơ Security.TechnicianProfiles cho toàn bộ tài khoản
      kỹ thuật viên hiện có nhưng chưa có hồ sơ công khai.
   3. Đợt 6C: Bổ sung cột DeliveryMethod và PickupAddress vào CRM.Appointments
      để hỗ trợ tiếp nhận tại quầy / gửi ship bưu điện / nhận máy tận nơi.
   Idempotent, có thể chạy an toàn nhiều lần trong 1 transaction.
   ============================================================================ */

USE [TechCare];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
BEGIN TRANSACTION;

-- 1. [6A] Bảng Security.EmailTokens lưu mã xác thực email / đặt lại mật khẩu (băm SHA-256)
IF OBJECT_ID(N'Security.EmailTokens', N'U') IS NULL
BEGIN
    CREATE TABLE Security.EmailTokens (
        TokenId        bigint IDENTITY(1,1) CONSTRAINT PK_EmailTokens PRIMARY KEY,
        UserId         bigint         NOT NULL CONSTRAINT FK_EmailTokens_User REFERENCES Security.Users(UserId) ON DELETE CASCADE,
        Email          nvarchar(254)  NOT NULL,
        TokenHash      varbinary(32)  NOT NULL,
        Purpose        nvarchar(50)   NOT NULL, -- 'EMAIL_CONFIRMATION', 'RESET_PASSWORD'
        ExpiresAtUtc   datetime2(0)   NOT NULL,
        FailedAttempts int            NOT NULL CONSTRAINT DF_EmailTokens_Failed DEFAULT 0,
        ConsumedAtUtc  datetime2(0)   NULL,
        IpAddress      nvarchar(45)   NULL,
        CreatedAtUtc   datetime2(0)   NOT NULL CONSTRAINT DF_EmailTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_EmailTokens_Failed CHECK (FailedAttempts >= 0)
    );

    CREATE INDEX IX_EmailTokens_Email_Purpose 
        ON Security.EmailTokens (Email, Purpose, CreatedAtUtc DESC);

    CREATE INDEX IX_EmailTokens_User_Purpose 
        ON Security.EmailTokens (UserId, Purpose, CreatedAtUtc DESC);
END;

-- 2. [6B] Backfill hồ sơ Security.TechnicianProfiles cho kỹ thuật viên chưa có hồ sơ
INSERT INTO Security.TechnicianProfiles (
    UserId, 
    DisplayName, 
    Specialty, 
    Bio, 
    ExperienceYears, 
    IsPublic, 
    IsAcceptingRequests, 
    DisplayOrder, 
    CreatedAtUtc
)
SELECT 
    u.UserId,
    COALESCE(u.UserName, N'Kỹ thuật viên TechCare'),
    N'Kỹ thuật viên Phần cứng & Phần mềm',
    N'Chuyên viên kỹ thuật tiếp nhận và xử lý máy tính tại xưởng TechCare.',
    2,
    1,
    1,
    0,
    SYSUTCDATETIME()
FROM Security.Users u
INNER JOIN Security.UserRoles ur ON u.UserId = ur.UserId
INNER JOIN Security.Roles r ON ur.RoleId = r.RoleId
LEFT JOIN Security.TechnicianProfiles tp ON u.UserId = tp.UserId
WHERE r.RoleCode = N'TECHNICIAN'
  AND tp.ProfileId IS NULL
  AND u.DeletedAtUtc IS NULL;

-- 3. [6C] Bổ sung cột DeliveryMethod và PickupAddress vào CRM.Appointments
IF COL_LENGTH(N'CRM.Appointments', N'DeliveryMethod') IS NULL
BEGIN
    ALTER TABLE CRM.Appointments ADD DeliveryMethod nvarchar(20) NOT NULL CONSTRAINT DF_Appointments_DeliveryMethod DEFAULT N'AT_STORE';
END;

IF COL_LENGTH(N'CRM.Appointments', N'PickupAddress') IS NULL
BEGIN
    ALTER TABLE CRM.Appointments ADD PickupAddress nvarchar(255) NULL;
END;

COMMIT TRANSACTION;
PRINT N'Migration 006 đã áp dụng thành công!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
    RAISERROR(@ErrMsg, 16, 1);
END CATCH;
GO
