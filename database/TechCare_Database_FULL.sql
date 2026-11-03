/* ============================================================================
   TechCare — Toàn Bộ CSDL Đầy Đủ (FULL Baseline Schema & Migrations 001 - 004)
   ----------------------------------------------------------------------------
   Mục đích: Khởi tạo database mới hoàn toàn (Đường A), sạch tuyệt đối 13/13 tiêu chí.
   Môi trường: SQL Server 2019+ (.\SQLEXPRESS) -> Database: TechCare
   Lưu ý: Chạy script này trên database TechCare mới tạo.
   ============================================================================ */

USE [TechCare];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
BEGIN TRANSACTION;

-- 1. SCHEMAS
IF SCHEMA_ID(N'Core') IS NULL EXEC(N'CREATE SCHEMA Core');
IF SCHEMA_ID(N'Security') IS NULL EXEC(N'CREATE SCHEMA Security');
IF SCHEMA_ID(N'CRM') IS NULL EXEC(N'CREATE SCHEMA CRM');
IF SCHEMA_ID(N'Repair') IS NULL EXEC(N'CREATE SCHEMA Repair');
IF SCHEMA_ID(N'Inventory') IS NULL EXEC(N'CREATE SCHEMA Inventory');
IF SCHEMA_ID(N'Billing') IS NULL EXEC(N'CREATE SCHEMA Billing');
IF SCHEMA_ID(N'Warranty') IS NULL EXEC(N'CREATE SCHEMA Warranty');
IF SCHEMA_ID(N'Comms') IS NULL EXEC(N'CREATE SCHEMA Comms');
IF SCHEMA_ID(N'Audit') IS NULL EXEC(N'CREATE SCHEMA Audit');
IF SCHEMA_ID(N'Ai') IS NULL EXEC(N'CREATE SCHEMA Ai');
IF SCHEMA_ID(N'Shipping') IS NULL EXEC(N'CREATE SCHEMA Shipping');

-- 2. CORE / ORGANIZATION
CREATE TABLE Core.Branches(
    BranchId int IDENTITY PRIMARY KEY, 
    BranchCode nvarchar(20) NOT NULL UNIQUE,
    BranchName nvarchar(150) NOT NULL, 
    Phone nvarchar(30), 
    Email nvarchar(254),
    AddressLine nvarchar(250), 
    Ward nvarchar(100), 
    District nvarchar(100), 
    Province nvarchar(100),
    IsActive bit NOT NULL DEFAULT 1, 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(0), 
    RowVersion rowversion
);

-- 3. SECURITY / RBAC
CREATE TABLE Security.Users(
    UserId bigint IDENTITY PRIMARY KEY, 
    BranchId int NULL,
    UserName nvarchar(100) NOT NULL, 
    NormalizedUserName nvarchar(100) NOT NULL,
    Email nvarchar(254), 
    NormalizedEmail nvarchar(254), 
    PhoneNumber nvarchar(30),
    PasswordHash nvarchar(500), 
    SecurityStamp nvarchar(100),
    IsEmailConfirmed bit NOT NULL DEFAULT 0, 
    IsPhoneConfirmed bit NOT NULL DEFAULT 0,
    AccessFailedCount int NOT NULL DEFAULT 0, 
    LockoutEndUtc datetime2(0),
    LockoutEnabled bit NOT NULL DEFAULT 1, 
    IsActive bit NOT NULL DEFAULT 1,
    DeletedAtUtc datetime2(0) NULL, 
    DeletedByUserId bigint NULL,
    LastLoginAtUtc datetime2(0), 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(0), 
    RowVersion rowversion,
    CONSTRAINT FK_Users_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT FK_Users_DeletedBy FOREIGN KEY(DeletedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Users_Failed CHECK(AccessFailedCount>=0)
);
CREATE UNIQUE INDEX UX_Users_UserName ON Security.Users(NormalizedUserName);
CREATE UNIQUE INDEX UX_Users_Email ON Security.Users(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;

CREATE TABLE Security.Roles(
    RoleId int IDENTITY PRIMARY KEY, 
    RoleCode nvarchar(50) NOT NULL UNIQUE,
    RoleName nvarchar(100) NOT NULL, 
    Description nvarchar(300), 
    IsSystemRole bit NOT NULL DEFAULT 0
);

CREATE TABLE Security.Permissions(
    PermissionId int IDENTITY PRIMARY KEY, 
    PermissionCode nvarchar(100) NOT NULL UNIQUE,
    PermissionName nvarchar(150) NOT NULL, 
    ModuleCode nvarchar(50) NOT NULL, 
    Description nvarchar(300)
);

CREATE TABLE Security.UserRoles(
    UserId bigint NOT NULL, 
    RoleId int NOT NULL, 
    AssignedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    AssignedByUserId bigint NULL, 
    CONSTRAINT PK_UserRoles PRIMARY KEY(UserId,RoleId),
    CONSTRAINT FK_UserRoles_User FOREIGN KEY(UserId) REFERENCES Security.Users(UserId),
    CONSTRAINT FK_UserRoles_Role FOREIGN KEY(RoleId) REFERENCES Security.Roles(RoleId),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY(AssignedByUserId) REFERENCES Security.Users(UserId)
);

CREATE TABLE Security.RolePermissions(
    RoleId int NOT NULL, 
    PermissionId int NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY(RoleId,PermissionId),
    CONSTRAINT FK_RP_Role FOREIGN KEY(RoleId) REFERENCES Security.Roles(RoleId),
    CONSTRAINT FK_RP_Permission FOREIGN KEY(PermissionId) REFERENCES Security.Permissions(PermissionId)
);

CREATE TABLE Security.Sessions(
    SessionId uniqueidentifier NOT NULL PRIMARY KEY,
    UserId bigint NOT NULL,
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    LastUsedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    AbsoluteExpiresAtUtc datetime2(0) NOT NULL,
    RevokedAtUtc datetime2(0) NULL,
    RevokeReason nvarchar(50) NULL,
    CreatedByIp nvarchar(45) NULL,
    UserAgent nvarchar(500) NULL,
    DeviceLabel nvarchar(200) NULL,
    RowVersion rowversion,
    CONSTRAINT FK_Sessions_User FOREIGN KEY(UserId) REFERENCES Security.Users(UserId)
);
CREATE INDEX IX_Sessions_User_Active ON Security.Sessions(UserId) WHERE RevokedAtUtc IS NULL;

CREATE TABLE Security.RefreshTokens(
    RefreshTokenId bigint IDENTITY PRIMARY KEY,
    SessionId uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    TokenHash varbinary(32) NOT NULL,
    ExpiresAtUtc datetime2(0) NOT NULL,
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    RotatedAtUtc datetime2(0) NULL,
    RevokedAtUtc datetime2(0) NULL,
    ReuseDetectedAtUtc datetime2(0) NULL,
    ReplacedByTokenHash varbinary(32),
    CreatedByIp nvarchar(45),
    LastUsedByIp nvarchar(45),
    RevokedByIp nvarchar(45),
    CONSTRAINT FK_RefreshTokens_Session FOREIGN KEY(SessionId) REFERENCES Security.Sessions(SessionId),
    CONSTRAINT FK_RefreshTokens_User FOREIGN KEY(UserId) REFERENCES Security.Users(UserId)
);
CREATE INDEX IX_RefreshTokens_Active ON Security.RefreshTokens(TokenHash) WHERE RevokedAtUtc IS NULL;

-- 4. CRM / CUSTOMERS & DEVICES
CREATE TABLE CRM.Customers(
    CustomerId bigint IDENTITY PRIMARY KEY, 
    CustomerCode nvarchar(30) NOT NULL UNIQUE,
    FullName nvarchar(150) NOT NULL, 
    PhoneNumber nvarchar(30) NOT NULL, 
    Email nvarchar(254),
    AddressLine nvarchar(250), 
    Ward nvarchar(100), 
    District nvarchar(100), 
    Province nvarchar(100),
    Notes nvarchar(1000), 
    IsActive bit NOT NULL DEFAULT 1, 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(0), 
    RowVersion rowversion
);
CREATE INDEX IX_Customers_Phone ON CRM.Customers(PhoneNumber);

CREATE TABLE CRM.DeviceBrands(
    BrandId int IDENTITY PRIMARY KEY, 
    BrandName nvarchar(100) NOT NULL UNIQUE, 
    IsActive bit NOT NULL DEFAULT 1
);

CREATE TABLE CRM.Devices(
    DeviceId bigint IDENTITY PRIMARY KEY, 
    CustomerId bigint NOT NULL, 
    BrandId int,
    DeviceType nvarchar(30) NOT NULL, 
    ModelName nvarchar(150) NOT NULL, 
    SerialNumber nvarchar(100),
    Color nvarchar(50), 
    Notes nvarchar(1000), 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(0), 
    RowVersion rowversion,
    CONSTRAINT FK_Devices_Customer FOREIGN KEY(CustomerId) REFERENCES CRM.Customers(CustomerId),
    CONSTRAINT FK_Devices_Brand FOREIGN KEY(BrandId) REFERENCES CRM.DeviceBrands(BrandId),
    CONSTRAINT CK_Devices_Type CHECK(DeviceType IN(N'LAPTOP',N'PC',N'MACBOOK',N'OTHER'))
);
CREATE INDEX IX_Devices_Serial ON CRM.Devices(SerialNumber) WHERE SerialNumber IS NOT NULL;

CREATE TABLE CRM.Appointments(
    AppointmentId bigint IDENTITY PRIMARY KEY, 
    BranchId int NOT NULL, 
    CustomerId bigint,
    AppointmentCode nvarchar(30) NOT NULL UNIQUE,
    CustomerName nvarchar(150) NOT NULL, 
    CustomerPhone nvarchar(30) NOT NULL, 
    CustomerEmail nvarchar(254),
    ScheduledAtUtc datetime2(0) NOT NULL, 
    Status nvarchar(20) NOT NULL DEFAULT N'BOOKED',
    IssueDescription nvarchar(1000), 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Appointments_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT FK_Appointments_Customer FOREIGN KEY(CustomerId) REFERENCES CRM.Customers(CustomerId),
    CONSTRAINT CK_Appointments_Status CHECK(Status IN(N'BOOKED',N'CONFIRMED',N'CANCELLED',N'COMPLETED',N'NO_SHOW'))
);

-- 5. REPAIR WORKFLOW
CREATE TABLE Repair.RepairTickets(
    RepairTicketId bigint IDENTITY PRIMARY KEY, 
    TicketCode nvarchar(30) NOT NULL UNIQUE,
    BranchId int NOT NULL, 
    CustomerId bigint NOT NULL, 
    DeviceId bigint NOT NULL, 
    AppointmentId bigint,
    IntakeStaffUserId bigint NOT NULL, 
    AssignedTechnicianUserId bigint,
    Status nvarchar(30) NOT NULL DEFAULT N'RECEIVED',
    Priority nvarchar(15) NOT NULL DEFAULT N'NORMAL',
    CustomerReportedFault nvarchar(2000) NOT NULL, 
    AccessoriesJson nvarchar(max), 
    DevicePasscode nvarchar(100),
    EstimatedCompletionAtUtc datetime2(0), 
    CompletedAtUtc datetime2(0), 
    ClosedAtUtc datetime2(0),
    TrackingCodeHash varbinary(32), 
    ReceivedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(0), 
    RowVersion rowversion,
    CONSTRAINT FK_Tickets_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT FK_Tickets_Customer FOREIGN KEY(CustomerId) REFERENCES CRM.Customers(CustomerId),
    CONSTRAINT FK_Tickets_Device FOREIGN KEY(DeviceId) REFERENCES CRM.Devices(DeviceId),
    CONSTRAINT FK_Tickets_Appointment FOREIGN KEY(AppointmentId) REFERENCES CRM.Appointments(AppointmentId),
    CONSTRAINT FK_Tickets_IntakeStaff FOREIGN KEY(IntakeStaffUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT FK_Tickets_Technician FOREIGN KEY(AssignedTechnicianUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Tickets_Status CHECK(Status IN(N'RECEIVED',N'DIAGNOSING',N'WAITING_APPROVAL',N'APPROVED',N'WAITING_PARTS',N'REPAIRING',N'TESTING',N'READY_FOR_PICKUP',N'COMPLETED',N'CANCELLED')),
    CONSTRAINT CK_Tickets_Priority CHECK(Priority IN(N'LOW',N'NORMAL',N'HIGH',N'URGENT')),
    CONSTRAINT CK_Tickets_AccJson CHECK(AccessoriesJson IS NULL OR ISJSON(AccessoriesJson)=1)
);
CREATE INDEX IX_Tickets_BranchStatus ON Repair.RepairTickets(BranchId,Status);
CREATE INDEX IX_Tickets_Customer ON Repair.RepairTickets(CustomerId);
CREATE INDEX IX_Tickets_TechnicianStatus ON Repair.RepairTickets(AssignedTechnicianUserId,Status);
CREATE UNIQUE INDEX UX_RepairTickets_TrackingCodeHash ON Repair.RepairTickets(TrackingCodeHash) WHERE TrackingCodeHash IS NOT NULL;

CREATE TABLE Repair.TicketStatusHistories(
    HistoryId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL,
    OldStatus nvarchar(30), 
    NewStatus nvarchar(30) NOT NULL, 
    ChangedByUserId bigint NOT NULL,
    ChangedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), 
    Note nvarchar(500),
    CONSTRAINT FK_TSH_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_TSH_User FOREIGN KEY(ChangedByUserId) REFERENCES Security.Users(UserId)
);
CREATE INDEX IX_TSH_TicketDate ON Repair.TicketStatusHistories(RepairTicketId,ChangedAtUtc DESC);

CREATE TABLE Repair.DiagnosisReports(
    DiagnosisReportId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL,
    DiagnosedByUserId bigint NOT NULL, 
    FaultDetails nvarchar(2000) NOT NULL,
    TechnicalFindings nvarchar(2000), 
    RecommendedSolution nvarchar(2000), 
    EstimatedMinutes int,
    DiagnosedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), 
    RowVersion rowversion,
    CONSTRAINT FK_Diagnosis_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_Diagnosis_User FOREIGN KEY(DiagnosedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Diagnosis_Minutes CHECK(EstimatedMinutes IS NULL OR EstimatedMinutes>=0)
);

CREATE TABLE Core.FileAttachments(
    FileId bigint IDENTITY PRIMARY KEY, 
    StorageProvider nvarchar(30) NOT NULL, 
    StorageKey nvarchar(500) NOT NULL,
    OriginalFileName nvarchar(255) NOT NULL, 
    ContentType nvarchar(150) NOT NULL, 
    SizeBytes bigint NOT NULL,
    Sha256Hash varbinary(32), 
    UploadedByUserId bigint, 
    UploadedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL DEFAULT 0,
    CONSTRAINT FK_File_User FOREIGN KEY(UploadedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_File_Size CHECK(SizeBytes>0),
    CONSTRAINT CK_File_Provider CHECK(StorageProvider IN(N'LOCAL',N'AZURE_BLOB',N'S3_COMPATIBLE'))
);

CREATE TABLE Repair.DiagnosticRuns(
    DiagnosticRunId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL,
    CollectedByUserId bigint, 
    AgentVersion nvarchar(50), 
    ConsentRecordedAtUtc datetime2(0) NOT NULL,
    CollectedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    DeviceSummaryJson nvarchar(max), 
    ReportFileId bigint,
    CONSTRAINT FK_DiagRun_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_DiagRun_User FOREIGN KEY(CollectedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT FK_DiagRun_ReportFile FOREIGN KEY(ReportFileId) REFERENCES Core.FileAttachments(FileId),
    CONSTRAINT CK_DiagRun_Json CHECK(DeviceSummaryJson IS NULL OR ISJSON(DeviceSummaryJson)=1)
);

CREATE TABLE Repair.Quotations(
    QuotationId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL,
    QuotationCode nvarchar(30) NOT NULL UNIQUE, 
    VersionNumber int NOT NULL,
    QuotationType nvarchar(15) NOT NULL DEFAULT N'INITIAL', 
    Status nvarchar(20) NOT NULL DEFAULT N'DRAFT',
    IssuedAtUtc datetime2(0), 
    ValidUntilUtc datetime2(0), 
    Subtotal decimal(18,2) NOT NULL DEFAULT 0,
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0, 
    TaxAmount decimal(18,2) NOT NULL DEFAULT 0,
    TotalAmount AS(Subtotal-DiscountAmount+TaxAmount) PERSISTED,
    CustomerNote nvarchar(1000), 
    CreatedByUserId bigint, 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion,
    CONSTRAINT UQ_Quote_TicketVersion UNIQUE(RepairTicketId,VersionNumber),
    CONSTRAINT FK_Quote_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_Quote_User FOREIGN KEY(CreatedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Quote_Version CHECK(VersionNumber>0),
    CONSTRAINT CK_Quote_Type CHECK(QuotationType IN(N'INITIAL',N'SUPPLEMENT')),
    CONSTRAINT CK_Quote_Status CHECK(Status IN(N'DRAFT',N'SENT',N'APPROVED',N'PARTIALLY_APPROVED',N'DECLINED',N'EXPIRED',N'VOID')),
    CONSTRAINT CK_Quote_Amounts CHECK(Subtotal>=0 AND DiscountAmount>=0 AND TaxAmount>=0 AND DiscountAmount<=Subtotal)
);

-- 6. INVENTORY
CREATE TABLE Inventory.PartCategories(
    PartCategoryId int IDENTITY PRIMARY KEY, 
    CategoryName nvarchar(100) NOT NULL UNIQUE,
    ParentCategoryId int, 
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT FK_PartCategory_Parent FOREIGN KEY(ParentCategoryId) REFERENCES Inventory.PartCategories(PartCategoryId)
);

CREATE TABLE Inventory.Suppliers(
    SupplierId bigint IDENTITY PRIMARY KEY, 
    SupplierCode nvarchar(30) NOT NULL UNIQUE,
    SupplierName nvarchar(180) NOT NULL, 
    ContactName nvarchar(150), 
    Phone nvarchar(30), 
    Email nvarchar(254), 
    AddressLine nvarchar(250), 
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Inventory.Parts(
    PartId bigint IDENTITY PRIMARY KEY, 
    PartCategoryId int, 
    PartCode nvarchar(50) NOT NULL UNIQUE,
    PartName nvarchar(180) NOT NULL, 
    Brand nvarchar(100), 
    Specification nvarchar(500),
    Unit nvarchar(30) NOT NULL DEFAULT N'piece', 
    DefaultCost decimal(18,2) NOT NULL DEFAULT 0,
    DefaultSalePrice decimal(18,2) NOT NULL DEFAULT 0, 
    ReorderLevel decimal(18,3) NOT NULL DEFAULT 0,
    IsSerialized bit NOT NULL DEFAULT 0, 
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), 
    RowVersion rowversion,
    CONSTRAINT FK_Part_Category FOREIGN KEY(PartCategoryId) REFERENCES Inventory.PartCategories(PartCategoryId),
    CONSTRAINT CK_Part_Prices CHECK(DefaultCost>=0 AND DefaultSalePrice>=0 AND ReorderLevel>=0)
);

CREATE TABLE Repair.QuotationItems(
    QuotationItemId bigint IDENTITY PRIMARY KEY, 
    QuotationId bigint NOT NULL, 
    ItemType nvarchar(15) NOT NULL,
    PartId bigint NULL, 
    Description nvarchar(300) NOT NULL, 
    Quantity decimal(18,3) NOT NULL,
    UnitPrice decimal(18,2) NOT NULL, 
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0,
    LineTotal AS(CONVERT(decimal(18,2),Quantity*UnitPrice)-DiscountAmount) PERSISTED,
    CustomerDecision nvarchar(15) NOT NULL DEFAULT N'PENDING',
    CONSTRAINT FK_QItem_Quote FOREIGN KEY(QuotationId) REFERENCES Repair.Quotations(QuotationId),
    CONSTRAINT FK_QItem_Part FOREIGN KEY(PartId) REFERENCES Inventory.Parts(PartId),
    CONSTRAINT CK_QItem_Type CHECK(ItemType IN(N'PART',N'LABOR',N'FEE',N'OTHER')),
    CONSTRAINT CK_QItem_Qty CHECK(Quantity>0),
    CONSTRAINT CK_QItem_Price CHECK(UnitPrice>=0 AND DiscountAmount>=0 AND DiscountAmount<=Quantity*UnitPrice),
    CONSTRAINT CK_QItem_Decision CHECK(CustomerDecision IN(N'PENDING',N'APPROVED',N'DECLINED'))
);
CREATE INDEX IX_QItems_Quote ON Repair.QuotationItems(QuotationId);

CREATE TABLE Repair.QuotationApprovals(
    QuotationApprovalId bigint IDENTITY PRIMARY KEY, 
    QuotationId bigint NOT NULL,
    Decision nvarchar(20) NOT NULL, 
    DecisionAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    DecisionChannel nvarchar(20) NOT NULL, 
    CustomerNote nvarchar(1000), 
    RecordedByUserId bigint,
    CONSTRAINT FK_QApproval_Quote FOREIGN KEY(QuotationId) REFERENCES Repair.Quotations(QuotationId),
    CONSTRAINT FK_QApproval_User FOREIGN KEY(RecordedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_QApproval_Decision CHECK(Decision IN(N'APPROVED',N'PARTIALLY_APPROVED',N'DECLINED')),
    CONSTRAINT CK_QApproval_Channel CHECK(DecisionChannel IN(N'PORTAL',N'IN_PERSON',N'PHONE',N'OTHER'))
);

CREATE TABLE Inventory.BranchStock(
    BranchId int NOT NULL, 
    PartId bigint NOT NULL, 
    QuantityOnHand decimal(18,3) NOT NULL DEFAULT 0,
    UpdatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), 
    RowVersion rowversion,
    CONSTRAINT PK_BranchStock PRIMARY KEY(BranchId,PartId),
    CONSTRAINT FK_BStock_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT FK_BStock_Part FOREIGN KEY(PartId) REFERENCES Inventory.Parts(PartId),
    CONSTRAINT CK_BStock_Qty CHECK(QuantityOnHand>=0)
);

CREATE TABLE Inventory.StockTransactions(
    StockTransactionId bigint IDENTITY PRIMARY KEY, 
    BranchId int NOT NULL, 
    PartId bigint NOT NULL,
    SupplierId bigint NULL, 
    RepairTicketId bigint NULL,
    TransactionType nvarchar(20) NOT NULL, 
    Quantity decimal(18,3) NOT NULL,
    UnitCost decimal(18,2), 
    ReferenceCode nvarchar(50), 
    Note nvarchar(1000),
    PerformedByUserId bigint NOT NULL, 
    PerformedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ST_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT FK_ST_Part FOREIGN KEY(PartId) REFERENCES Inventory.Parts(PartId),
    CONSTRAINT FK_ST_Supplier FOREIGN KEY(SupplierId) REFERENCES Inventory.Suppliers(SupplierId),
    CONSTRAINT FK_ST_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_ST_User FOREIGN KEY(PerformedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_ST_Type CHECK(TransactionType IN(N'RECEIPT',N'ISSUE_REPAIR',N'ISSUE_OTHER',N'RETURN',N'ADJUSTMENT_IN',N'ADJUSTMENT_OUT',N'TRANSFER_IN',N'TRANSFER_OUT')),
    CONSTRAINT CK_ST_Qty CHECK(Quantity>0), 
    CONSTRAINT CK_ST_Cost CHECK(UnitCost IS NULL OR UnitCost>=0)
);

CREATE TABLE Repair.RepairParts(
    RepairPartId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL, 
    PartId bigint NOT NULL,
    BranchId int NOT NULL, 
    StockTransactionId bigint NULL, 
    Quantity decimal(18,3) NOT NULL,
    UnitSalePrice decimal(18,2) NOT NULL, 
    InstalledAtUtc datetime2(0), 
    InstalledByUserId bigint, 
    Note nvarchar(500),
    CONSTRAINT FK_RPart_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_RPart_Part FOREIGN KEY(PartId) REFERENCES Inventory.Parts(PartId),
    CONSTRAINT FK_RPart_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT FK_RPart_ST FOREIGN KEY(StockTransactionId) REFERENCES Inventory.StockTransactions(StockTransactionId),
    CONSTRAINT FK_RPart_User FOREIGN KEY(InstalledByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_RPart_Qty CHECK(Quantity>0), 
    CONSTRAINT CK_RPart_Price CHECK(UnitSalePrice>=0)
);

-- 7. BILLING & INVOICES
CREATE TABLE Billing.Invoices(
    InvoiceId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL,
    InvoiceNumber nvarchar(40) NOT NULL UNIQUE, 
    InvoiceType nvarchar(15) NOT NULL DEFAULT N'SALE',
    Status nvarchar(15) NOT NULL DEFAULT N'DRAFT', 
    IssuedAtUtc datetime2(0),
    Subtotal decimal(18,2) NOT NULL, 
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0,
    TaxAmount decimal(18,2) NOT NULL DEFAULT 0,
    TotalAmount AS(Subtotal-DiscountAmount+TaxAmount) PERSISTED,
    Note nvarchar(1000), 
    CreatedByUserId bigint, 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion,
    CONSTRAINT FK_Invoice_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_Invoice_User FOREIGN KEY(CreatedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Invoice_Type CHECK(InvoiceType IN(N'SALE',N'REFUND')),
    CONSTRAINT CK_Invoice_Status CHECK(Status IN(N'DRAFT',N'ISSUED',N'VOID')),
    CONSTRAINT CK_Invoice_Amounts CHECK(Subtotal>=0 AND DiscountAmount>=0 AND TaxAmount>=0 AND DiscountAmount<=Subtotal)
);

CREATE TABLE Billing.Payments(
    PaymentId bigint IDENTITY PRIMARY KEY, 
    InvoiceId bigint NOT NULL,
    PaymentCode nvarchar(40) NOT NULL UNIQUE, 
    Amount decimal(18,2) NOT NULL,
    PaymentMethod nvarchar(20) NOT NULL, 
    PaymentStatus nvarchar(20) NOT NULL DEFAULT N'COMPLETED',
    TransactionReference nvarchar(100), 
    PaidAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    ReceivedByUserId bigint, 
    Note nvarchar(500),
    CONSTRAINT FK_Payment_Invoice FOREIGN KEY(InvoiceId) REFERENCES Billing.Invoices(InvoiceId),
    CONSTRAINT FK_Payment_User FOREIGN KEY(ReceivedByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Payment_Amount CHECK(Amount>0),
    CONSTRAINT CK_Payment_Method CHECK(PaymentMethod IN(N'CASH',N'BANK_TRANSFER',N'CARD',N'E_WALLET',N'OTHER')),
    CONSTRAINT CK_Payment_Status CHECK(PaymentStatus IN(N'PENDING',N'COMPLETED',N'FAILED',N'REVERSED'))
);

-- 8. WARRANTY
CREATE TABLE Warranty.Warranties(
    WarrantyId bigint IDENTITY PRIMARY KEY, 
    RepairTicketId bigint NOT NULL, 
    RepairPartId bigint NULL,
    WarrantyCode nvarchar(40) NOT NULL UNIQUE, 
    CoverageDescription nvarchar(1000) NOT NULL,
    StartAtUtc datetime2(0) NOT NULL, 
    EndAtUtc datetime2(0) NOT NULL, 
    Terms nvarchar(2000),
    Status nvarchar(15) NOT NULL DEFAULT N'ACTIVE', 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Warranty_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_Warranty_RepairPart FOREIGN KEY(RepairPartId) REFERENCES Repair.RepairParts(RepairPartId),
    CONSTRAINT CK_Warranty_Dates CHECK(EndAtUtc>=StartAtUtc),
    CONSTRAINT CK_Warranty_Status CHECK(Status IN(N'ACTIVE',N'EXPIRED',N'VOID'))
);

CREATE TABLE Warranty.WarrantyClaims(
    WarrantyClaimId bigint IDENTITY PRIMARY KEY, 
    WarrantyId bigint NOT NULL,
    ClaimCode nvarchar(40) NOT NULL UNIQUE, 
    CustomerDescription nvarchar(2000) NOT NULL,
    InspectionResult nvarchar(2000), 
    Status nvarchar(20) NOT NULL DEFAULT N'RECEIVED',
    ReceivedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), 
    ResolvedAtUtc datetime2(0),
    HandledByUserId bigint, 
    ResolutionNote nvarchar(1500),
    CONSTRAINT FK_Claim_Warranty FOREIGN KEY(WarrantyId) REFERENCES Warranty.Warranties(WarrantyId),
    CONSTRAINT FK_Claim_User FOREIGN KEY(HandledByUserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Claim_Status CHECK(Status IN(N'RECEIVED',N'INSPECTING',N'APPROVED',N'REJECTED',N'REPAIRING',N'RESOLVED',N'CLOSED'))
);

CREATE TABLE Repair.TicketAttachments(
    RepairTicketId bigint NOT NULL, 
    FileId bigint NOT NULL, 
    AttachmentCategory nvarchar(20) NOT NULL, 
    Caption nvarchar(300),
    CONSTRAINT PK_TicketAttachments PRIMARY KEY(RepairTicketId,FileId),
    CONSTRAINT FK_TA_Ticket FOREIGN KEY(RepairTicketId) REFERENCES Repair.RepairTickets(RepairTicketId),
    CONSTRAINT FK_TA_File FOREIGN KEY(FileId) REFERENCES Core.FileAttachments(FileId),
    CONSTRAINT CK_TA_Category CHECK(AttachmentCategory IN(N'INTAKE',N'DIAGNOSIS',N'REPAIR',N'QA',N'HANDOVER',N'OTHER'))
);

CREATE TABLE Comms.Notifications(
    NotificationId bigint IDENTITY PRIMARY KEY, 
    CustomerId bigint NULL, 
    UserId bigint NULL,
    Channel nvarchar(15) NOT NULL, 
    TemplateCode nvarchar(80), 
    Subject nvarchar(200),
    MessageBody nvarchar(max) NOT NULL, 
    Status nvarchar(15) NOT NULL DEFAULT N'QUEUED',
    ScheduledAtUtc datetime2(0), 
    SentAtUtc datetime2(0), 
    ProviderMessageId nvarchar(150),
    FailureReason nvarchar(1000), 
    CreatedAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Notification_Customer FOREIGN KEY(CustomerId) REFERENCES CRM.Customers(CustomerId),
    CONSTRAINT FK_Notification_User FOREIGN KEY(UserId) REFERENCES Security.Users(UserId),
    CONSTRAINT CK_Notification_Channel CHECK(Channel IN(N'EMAIL',N'SMS',N'IN_APP')),
    CONSTRAINT CK_Notification_Status CHECK(Status IN(N'QUEUED',N'SENDING',N'SENT',N'FAILED',N'CANCELLED'))
);

CREATE TABLE Audit.AuditLogs(
    AuditLogId bigint IDENTITY PRIMARY KEY, 
    UserId bigint NULL, 
    BranchId int NULL,
    Action nvarchar(50) NOT NULL, 
    EntityName nvarchar(128) NOT NULL, 
    EntityId nvarchar(100),
    OccurredAtUtc datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(), 
    IpAddress nvarchar(45),
    UserAgent nvarchar(500), 
    CorrelationId nvarchar(100), 
    OldValuesJson nvarchar(max), 
    NewValuesJson nvarchar(max),
    CONSTRAINT FK_Audit_User FOREIGN KEY(UserId) REFERENCES Security.Users(UserId),
    CONSTRAINT FK_Audit_Branch FOREIGN KEY(BranchId) REFERENCES Core.Branches(BranchId),
    CONSTRAINT CK_Audit_OldJson CHECK(OldValuesJson IS NULL OR ISJSON(OldValuesJson)=1),
    CONSTRAINT CK_Audit_NewJson CHECK(NewValuesJson IS NULL OR ISJSON(NewValuesJson)=1)
);

-- 9. AI CHATBOT & KNOWLEDGE BASE (RAG)
CREATE TABLE Ai.KnowledgeDocuments (
    DocumentId    bigint IDENTITY(1,1) CONSTRAINT PK_KnowledgeDocuments PRIMARY KEY,
    Title         nvarchar(200) NOT NULL,
    Category      nvarchar(50)  NOT NULL,
    RawContent    nvarchar(max) NOT NULL,
    Status        nvarchar(20)  NOT NULL CONSTRAINT DF_KnowledgeDocuments_Status DEFAULT N'ACTIVE',
    CreatedByUserId bigint      NULL CONSTRAINT FK_KnowledgeDocuments_CreatedBy REFERENCES Security.Users(UserId),
    CreatedAtUtc  datetime2(0)  NOT NULL CONSTRAINT DF_KnowledgeDocuments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc  datetime2(0)  NOT NULL CONSTRAINT DF_KnowledgeDocuments_UpdatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Ai.KnowledgeChunks (
    ChunkId        bigint IDENTITY(1,1) CONSTRAINT PK_KnowledgeChunks PRIMARY KEY,
    DocumentId     bigint        NOT NULL CONSTRAINT FK_KnowledgeChunks_Document REFERENCES Ai.KnowledgeDocuments(DocumentId) ON DELETE CASCADE,
    ChunkIndex     int           NOT NULL,
    Content        nvarchar(max) NOT NULL,
    Embedding      varbinary(max) NULL,
    EmbeddingModel nvarchar(60)  NULL,
    EmbeddingDim   int           NULL,
    CreatedAtUtc   datetime2(0)  NOT NULL CONSTRAINT DF_KnowledgeChunks_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_KnowledgeChunks_Document ON Ai.KnowledgeChunks (DocumentId);

CREATE TABLE Ai.ChatSessions (
    SessionId        uniqueidentifier NOT NULL CONSTRAINT PK_ChatSessions PRIMARY KEY,
    GuestPhone       nvarchar(20)     NULL,
    Status           nvarchar(20)     NOT NULL CONSTRAINT DF_ChatSessions_Status DEFAULT N'BOT',
    AssignedSupportId bigint          NULL CONSTRAINT FK_ChatSessions_Support REFERENCES Security.Users(UserId),
    StartedAtUtc     datetime2(0)     NOT NULL CONSTRAINT DF_ChatSessions_Started DEFAULT SYSUTCDATETIME(),
    LastMessageAtUtc datetime2(0)     NOT NULL CONSTRAINT DF_ChatSessions_LastMsg DEFAULT SYSUTCDATETIME(),
    ClosedAtUtc      datetime2(0)     NULL
);
CREATE INDEX IX_ChatSessions_Status_LastMsg ON Ai.ChatSessions (Status, LastMessageAtUtc DESC);

CREATE TABLE Ai.ChatMessages (
    MessageId     bigint IDENTITY(1,1) CONSTRAINT PK_ChatMessages PRIMARY KEY,
    SessionId     uniqueidentifier NOT NULL CONSTRAINT FK_ChatMessages_Session REFERENCES Ai.ChatSessions(SessionId) ON DELETE CASCADE,
    SenderType    nvarchar(20)  NOT NULL CONSTRAINT CK_ChatMessages_SenderType CHECK (SenderType IN (N'CUSTOMER', N'BOT', N'SUPPORT', N'SYSTEM')),
    SenderUserId  bigint        NULL CONSTRAINT FK_ChatMessages_SenderUser REFERENCES Security.Users(UserId),
    Content       nvarchar(max) NOT NULL,
    SourcesJson   nvarchar(max) NULL,
    CreatedAtUtc  datetime2(0)  NOT NULL CONSTRAINT DF_ChatMessages_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_ChatMessages_Session_Time ON Ai.ChatMessages (SessionId, CreatedAtUtc);

-- 10. SHIPPING / GIAO VẬN GHN
CREATE TABLE Shipping.Shipments (
    ShipmentId        bigint IDENTITY(1,1) CONSTRAINT PK_Shipments PRIMARY KEY,
    CarrierCode       nvarchar(30)   NOT NULL,
    WaybillCode       nvarchar(60)   NOT NULL,
    Direction         nvarchar(10)   NOT NULL CONSTRAINT CK_Shipments_Direction CHECK (Direction IN (N'INBOUND', N'OUTBOUND')),
    AppointmentId     bigint         NULL CONSTRAINT FK_Shipments_Appointment REFERENCES CRM.Appointments(AppointmentId),
    RepairTicketId    bigint         NULL CONSTRAINT FK_Shipments_Ticket REFERENCES Repair.RepairTickets(RepairTicketId),
    SenderName        nvarchar(150)  NOT NULL,
    SenderPhone       nvarchar(20)   NOT NULL,
    SenderAddress     nvarchar(500)  NOT NULL,
    SenderProvinceCode nvarchar(20)  NULL,
    SenderDistrictCode nvarchar(20)  NULL,
    SenderWardCode    nvarchar(30)   NULL,
    ReceiverName      nvarchar(150)  NOT NULL,
    ReceiverPhone     nvarchar(20)   NOT NULL,
    ReceiverAddress   nvarchar(500)  NOT NULL,
    ReceiverProvinceCode nvarchar(20) NULL,
    ReceiverDistrictCode nvarchar(20) NULL,
    ReceiverWardCode  nvarchar(30)   NULL,
    CodAmount         decimal(18,0)  NOT NULL CONSTRAINT DF_Shipments_Cod DEFAULT 0,
    InsuredValue      decimal(18,0)  NOT NULL CONSTRAINT DF_Shipments_Insured DEFAULT 0,
    WeightGram        int            NULL,
    FeeAmount         decimal(18,0)  NULL,
    FeePayer          nvarchar(10)   NULL CONSTRAINT CK_Shipments_FeePayer CHECK (FeePayer IN (N'SENDER', N'RECEIVER')),
    StatusCode        nvarchar(30)   NOT NULL CONSTRAINT DF_Shipments_Status DEFAULT N'CREATED',
    ExpectedDeliveryAtUtc datetime2(0) NULL,
    CreatedByUserId   bigint         NOT NULL CONSTRAINT FK_Shipments_CreatedBy REFERENCES Security.Users(UserId),
    CreatedAtUtc      datetime2(0)   NOT NULL CONSTRAINT DF_Shipments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc      datetime2(0)   NOT NULL CONSTRAINT DF_Shipments_UpdatedAt DEFAULT SYSUTCDATETIME(),
    RawCreateResponseJson nvarchar(max) NULL,
    -- Coordinates (Migration 004)
    SenderLatitude    decimal(9,6)   NULL,
    SenderLongitude   decimal(9,6)   NULL,
    ReceiverLatitude  decimal(9,6)   NULL,
    ReceiverLongitude decimal(9,6)   NULL
);
CREATE UNIQUE INDEX UX_Shipments_Carrier_Waybill ON Shipping.Shipments (CarrierCode, WaybillCode);
CREATE INDEX IX_Shipments_Ticket ON Shipping.Shipments (RepairTicketId);
CREATE INDEX IX_Shipments_Appointment ON Shipping.Shipments (AppointmentId);

CREATE TABLE Shipping.ShipmentEvents (
    EventId        bigint IDENTITY(1,1) CONSTRAINT PK_ShipmentEvents PRIMARY KEY,
    ShipmentId     bigint         NOT NULL CONSTRAINT FK_ShipmentEvents_Shipment REFERENCES Shipping.Shipments(ShipmentId) ON DELETE CASCADE,
    ProviderStatus nvarchar(60)   NOT NULL,
    InternalStatus nvarchar(30)   NOT NULL,
    Description    nvarchar(500)  NULL,
    LocationText   nvarchar(300)  NULL,
    Latitude       decimal(9,6)   NULL,
    Longitude      decimal(9,6)   NULL,
    OccurredAtUtc  datetime2(0)   NOT NULL,
    ReceivedAtUtc  datetime2(0)   NOT NULL CONSTRAINT DF_ShipmentEvents_ReceivedAt DEFAULT SYSUTCDATETIME(),
    RawPayloadJson nvarchar(max)  NULL
);
CREATE UNIQUE INDEX UX_ShipmentEvents_Dedup ON Shipping.ShipmentEvents (ShipmentId, ProviderStatus, OccurredAtUtc);
CREATE INDEX IX_ShipmentEvents_Shipment_Time ON Shipping.ShipmentEvents (ShipmentId, OccurredAtUtc);

-- 11. BASELINE SEED DATA
-- Default Branch
SET IDENTITY_INSERT Core.Branches ON;
INSERT Core.Branches(BranchId, BranchCode, BranchName, Phone, Email, AddressLine, Province, IsActive)
VALUES (1, N'TC-CENTRAL', N'TechCare Chi Nhánh Trung Tâm', N'1900-6789', N'contact@techcare.vn', N'123 Nguyễn Tri Phương', N'Đà Nẵng', 1);
SET IDENTITY_INSERT Core.Branches OFF;

-- Default Device Brands
INSERT CRM.DeviceBrands(BrandName, IsActive) VALUES
(N'Dell', 1), (N'Apple', 1), (N'Asus', 1), (N'HP', 1), (N'Lenovo', 1), (N'Acer', 1), (N'MSI', 1);

-- Default Part Categories
INSERT Inventory.PartCategories(CategoryName, IsActive) VALUES
(N'RAM Laptop', 1), (N'Ổ cứng SSD / HDD', 1), (N'Pin Laptop', 1), (N'Màn hình hiển thị', 1), (N'Bàn phím thay thế', 1), (N'Sạc / Nguồn Adapter', 1);

-- Roles (including SUPPORT)
INSERT Security.Roles(RoleCode, RoleName, Description, IsSystemRole) VALUES
(N'ADMIN', N'Quản trị viên', N'Quản trị hệ thống', 1),
(N'MANAGER', N'Quản lý', N'Quản lý chi nhánh', 1),
(N'RECEPTION', N'Lễ tân', N'Tiếp nhận', 1),
(N'TECHNICIAN', N'Kỹ thuật viên', N'Chẩn đoán và sửa chữa', 1),
(N'INVENTORY', N'Nhân viên kho', N'Quản lý linh kiện', 1),
(N'CASHIER', N'Thu ngân', N'Hóa đơn và thanh toán', 1),
(N'CUSTOMER', N'Khách hàng', N'Cổng khách hàng', 1),
(N'SUPPORT', N'Hỗ trợ khách hàng', N'Tiếp nhận chat AI chuyển người, hỗ trợ tra cứu và vận đơn', 1);

-- Permissions (All original + AI & Shipping)
INSERT Security.Permissions(PermissionCode, PermissionName, ModuleCode) VALUES
(N'CUSTOMER.READ', N'Xem khách hàng', N'CRM'),
(N'CUSTOMER.WRITE', N'Tạo/sửa khách hàng', N'CRM'),
(N'DEVICE.READ', N'Xem thiết bị', N'CRM'),
(N'DEVICE.WRITE', N'Tạo/sửa thiết bị', N'CRM'),
(N'APPOINTMENT.MANAGE', N'Quản lý lịch hẹn', N'CRM'),
(N'REPAIR.READ', N'Xem phiếu sửa chữa', N'REPAIR'),
(N'REPAIR.INTAKE', N'Tiếp nhận', N'REPAIR'),
(N'REPAIR.ASSIGN', N'Phân công kỹ thuật viên', N'REPAIR'),
(N'REPAIR.DIAGNOSE', N'Chẩn đoán', N'REPAIR'),
(N'REPAIR.UPDATE_STATUS', N'Cập nhật trạng thái', N'REPAIR'),
(N'QUOTE.MANAGE', N'Tạo báo giá', N'REPAIR'),
(N'QUOTE.APPROVE', N'Ghi nhận duyệt báo giá', N'REPAIR'),
(N'INVENTORY.READ', N'Xem kho', N'INVENTORY'),
(N'INVENTORY.TRANSACT', N'Giao dịch kho', N'INVENTORY'),
(N'BILLING.READ', N'Xem hóa đơn', N'BILLING'),
(N'BILLING.COLLECT', N'Thu tiền', N'BILLING'),
(N'WARRANTY.MANAGE', N'Quản lý bảo hành', N'WARRANTY'),
(N'REPORT.READ', N'Xem báo cáo', N'REPORT'),
(N'USER.MANAGE', N'Quản lý tài khoản', N'SECURITY'),
(N'ROLE.MANAGE', N'Quản lý phân quyền', N'SECURITY'),
(N'AUDIT.READ', N'Xem nhật ký', N'AUDIT'),
(N'AI.KB.MANAGE', N'Quản lý tri thức AI', N'AI'),
(N'AI.CHAT.VIEW', N'Xem hội thoại chat AI', N'AI'),
(N'AI.CHAT.REPLY', N'Trả lời chat thay bot', N'AI'),
(N'SHIPPING.VIEW', N'Xem vận đơn', N'SHIPPING'),
(N'SHIPPING.CREATE', N'Tạo vận đơn', N'SHIPPING'),
(N'SHIPPING.MANAGE', N'Hủy và cấu hình vận đơn', N'SHIPPING');

-- Role Permissions Grants
-- 1. ADMIN: Toàn bộ permissions
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r CROSS JOIN Security.Permissions p
WHERE r.RoleCode = N'ADMIN';

-- 2. MANAGER
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r JOIN Security.Permissions p ON
 (r.RoleCode = N'MANAGER' AND p.PermissionCode NOT IN (N'USER.MANAGE', N'ROLE.MANAGE', N'AI.KB.MANAGE'));

-- 3. RECEPTION
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r JOIN Security.Permissions p ON
 (r.RoleCode = N'RECEPTION' AND p.PermissionCode IN (N'CUSTOMER.READ', N'CUSTOMER.WRITE', N'DEVICE.READ', N'DEVICE.WRITE', N'APPOINTMENT.MANAGE', N'REPAIR.READ', N'REPAIR.INTAKE', N'SHIPPING.VIEW', N'SHIPPING.CREATE'));

-- 4. TECHNICIAN
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r JOIN Security.Permissions p ON
 (r.RoleCode = N'TECHNICIAN' AND p.PermissionCode IN (N'DEVICE.READ', N'REPAIR.READ', N'REPAIR.DIAGNOSE', N'REPAIR.UPDATE_STATUS'));

-- 5. INVENTORY
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r JOIN Security.Permissions p ON
 (r.RoleCode = N'INVENTORY' AND p.PermissionCode IN (N'INVENTORY.READ', N'INVENTORY.TRANSACT', N'REPAIR.READ'));

-- 6. CASHIER
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r JOIN Security.Permissions p ON
 (r.RoleCode = N'CASHIER' AND p.PermissionCode IN (N'CUSTOMER.READ', N'REPAIR.READ', N'BILLING.READ', N'BILLING.COLLECT', N'SHIPPING.VIEW'));

-- 7. SUPPORT
INSERT Security.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId 
FROM Security.Roles r JOIN Security.Permissions p ON
 (r.RoleCode = N'SUPPORT' AND p.PermissionCode IN (N'AI.CHAT.VIEW', N'AI.CHAT.REPLY', N'SHIPPING.VIEW', N'CUSTOMER.READ', N'REPAIR.READ'));

-- Default Admin User (admin / Admin@123)
SET IDENTITY_INSERT Security.Users ON;
INSERT Security.Users(UserId, BranchId, UserName, NormalizedUserName, Email, NormalizedEmail, PhoneNumber, PasswordHash, SecurityStamp, IsEmailConfirmed, IsActive, CreatedAtUtc)
VALUES (1, 1, N'admin', N'ADMIN', N'admin@techcare.vn', N'ADMIN@TECHCARE.VN', N'0901234567', N'0bZGvX8uYKmn1Mu6r6HBUw==:5eRLtAlFVoP/X4yOMtIwYu/zw4JXVjVx2LAHrgzGLGc=', CONVERT(nvarchar(100), NEWID()), 1, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT Security.Users OFF;

DECLARE @adminRoleId int = (SELECT RoleId FROM Security.Roles WHERE RoleCode = N'ADMIN');
IF @adminRoleId IS NOT NULL
BEGIN
    INSERT Security.UserRoles(UserId, RoleId, AssignedAtUtc) VALUES (1, @adminRoleId, SYSUTCDATETIME());
END;

COMMIT TRANSACTION;
PRINT N'TechCare FULL schema & migrations (001 - 004) created successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER VIEW Inventory.vw_BranchStockStatus
AS
SELECT bs.BranchId, b.BranchCode, b.BranchName, p.PartId, p.PartCode, p.PartName,
       bs.QuantityOnHand, p.ReorderLevel,
       CAST(CASE WHEN bs.QuantityOnHand <= p.ReorderLevel THEN 1 ELSE 0 END AS bit) AS IsLowStock
FROM Inventory.BranchStock bs
JOIN Core.Branches b ON b.BranchId = bs.BranchId
JOIN Inventory.Parts p ON p.PartId = bs.PartId
WHERE p.IsActive = 1;
GO
