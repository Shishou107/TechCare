/* ============================================================================
   TechCare — Migration 005: Tài Khoản Khách Hàng (OTP) & Chọn Kỹ Thuật Viên
   ----------------------------------------------------------------------------
   Mục đích:
   1. Đợt 5A: Thêm bảng Security.OtpCodes và liên kết CRM.Customers.UserId
   2. Đợt 5B: Thêm bảng Security.TechnicianProfiles, Repair.TicketRatings,
      và cột PreferredTechnicianUserId cho Appointments và RepairTickets
   Idempotent, có thể chạy nhiều lần an toàn trong 1 transaction.
   ============================================================================ */

USE [TechCare];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
BEGIN TRANSACTION;

-- 1. [5A] Bảng Security.OtpCodes lưu mã xác thực băm SHA-256
IF OBJECT_ID(N'Security.OtpCodes', N'U') IS NULL
BEGIN
    CREATE TABLE Security.OtpCodes (
        OtpId          bigint IDENTITY(1,1) CONSTRAINT PK_OtpCodes PRIMARY KEY,
        PhoneNumber    nvarchar(20)   NOT NULL,
        CodeHash       varbinary(32)  NOT NULL,
        ExpiresAtUtc   datetime2(0)   NOT NULL,
        FailedAttempts int            NOT NULL CONSTRAINT DF_OtpCodes_Failed DEFAULT 0,
        ConsumedAtUtc  datetime2(0)   NULL,
        IpAddress      nvarchar(45)   NULL,
        CreatedAtUtc   datetime2(0)   NOT NULL CONSTRAINT DF_OtpCodes_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_OtpCodes_Failed CHECK (FailedAttempts >= 0)
    );

    CREATE INDEX IX_OtpCodes_Phone_Active 
        ON Security.OtpCodes (PhoneNumber, CreatedAtUtc DESC);
END;

-- 2. [5A] Thêm cột UserId vào CRM.Customers
IF COL_LENGTH(N'CRM.Customers', N'UserId') IS NULL
BEGIN
    ALTER TABLE CRM.Customers ADD UserId bigint NULL;
    ALTER TABLE CRM.Customers ADD CONSTRAINT FK_Customers_User 
        FOREIGN KEY (UserId) REFERENCES Security.Users(UserId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Customers_UserId' AND object_id = OBJECT_ID(N'CRM.Customers'))
BEGIN
    CREATE UNIQUE INDEX UX_Customers_UserId 
        ON CRM.Customers (UserId) WHERE UserId IS NOT NULL;
END;

-- 3. [5B] Bảng Security.TechnicianProfiles
IF OBJECT_ID(N'Security.TechnicianProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE Security.TechnicianProfiles (
        ProfileId          bigint IDENTITY(1,1) CONSTRAINT PK_TechnicianProfiles PRIMARY KEY,
        UserId             bigint         NOT NULL UNIQUE CONSTRAINT FK_TechProfile_User REFERENCES Security.Users(UserId) ON DELETE CASCADE,
        DisplayName        nvarchar(150)  NOT NULL,
        AvatarUrl          nvarchar(500)  NULL,
        Specialty          nvarchar(250)  NOT NULL,
        Bio                nvarchar(1000) NULL,
        ExperienceYears    int            NOT NULL CONSTRAINT DF_TechProfile_Exp DEFAULT 1,
        IsPublic           bit            NOT NULL CONSTRAINT DF_TechProfile_Public DEFAULT 1,
        IsAcceptingRequests bit           NOT NULL CONSTRAINT DF_TechProfile_Accept DEFAULT 1,
        DisplayOrder       int            NOT NULL CONSTRAINT DF_TechProfile_Order DEFAULT 0,
        CreatedAtUtc       datetime2(0)   NOT NULL CONSTRAINT DF_TechProfile_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc       datetime2(0)   NULL
    );

    CREATE INDEX IX_TechProfile_PublicOrder 
        ON Security.TechnicianProfiles (IsPublic, DisplayOrder, ProfileId);
END;

-- 4. [5B] Thêm cột PreferredTechnicianUserId vào CRM.Appointments
IF COL_LENGTH(N'CRM.Appointments', N'PreferredTechnicianUserId') IS NULL
BEGIN
    ALTER TABLE CRM.Appointments ADD PreferredTechnicianUserId bigint NULL;
    ALTER TABLE CRM.Appointments ADD CONSTRAINT FK_Appt_PrefTech 
        FOREIGN KEY (PreferredTechnicianUserId) REFERENCES Security.Users(UserId);
END;

-- 5. [5B] Thêm cột PreferredTechnicianUserId vào Repair.RepairTickets
IF COL_LENGTH(N'Repair.RepairTickets', N'PreferredTechnicianUserId') IS NULL
BEGIN
    ALTER TABLE Repair.RepairTickets ADD PreferredTechnicianUserId bigint NULL;
    ALTER TABLE Repair.RepairTickets ADD CONSTRAINT FK_Ticket_PrefTech 
        FOREIGN KEY (PreferredTechnicianUserId) REFERENCES Security.Users(UserId);
END;

-- 6. [5B] Bảng Repair.TicketRatings lưu đánh giá của khách
IF OBJECT_ID(N'Repair.TicketRatings', N'U') IS NULL
BEGIN
    CREATE TABLE Repair.TicketRatings (
        RatingId           bigint IDENTITY(1,1) CONSTRAINT PK_TicketRatings PRIMARY KEY,
        RepairTicketId     bigint         NOT NULL UNIQUE CONSTRAINT FK_Rating_Ticket REFERENCES Repair.RepairTickets(RepairTicketId),
        CustomerId         bigint         NOT NULL CONSTRAINT FK_Rating_Customer REFERENCES CRM.Customers(CustomerId),
        TechnicianUserId   bigint         NOT NULL CONSTRAINT FK_Rating_Tech REFERENCES Security.Users(UserId),
        Score              int            NOT NULL CONSTRAINT CK_Rating_Score CHECK (Score BETWEEN 1 AND 5),
        Comment            nvarchar(1000) NULL,
        CreatedAtUtc       datetime2(0)   NOT NULL CONSTRAINT DF_Rating_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_Ratings_TechScore 
        ON Repair.TicketRatings (TechnicianUserId, Score);
END;

-- 7. Seed hồ sơ kỹ thuật viên mẫu nếu có tài khoản technician
DECLARE @techUserId bigint = (SELECT TOP 1 UserId FROM Security.Users WHERE UserName = N'technician');
IF @techUserId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Security.TechnicianProfiles WHERE UserId = @techUserId)
BEGIN
    INSERT INTO Security.TechnicianProfiles 
        (UserId, DisplayName, AvatarUrl, Specialty, Bio, ExperienceYears, IsPublic, IsAcceptingRequests, DisplayOrder)
    VALUES 
        (@techUserId, N'Trần Văn Tuấn', N'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&q=80&w=250', 
         N'Chuyên gia MacBook, Laptop Gaming & sửa chữa Mainboard', N'Hơn 7 năm kinh nghiệm đo đạc sơ đồ boardview, xử lý lỗi nguồn, thay chip VGA và phục hồi máy ngấm nước.', 7, 1, 1, 1);
END;

COMMIT TRANSACTION;
PRINT N'Migration 005 applied successfully (42 tables full architecture).';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

