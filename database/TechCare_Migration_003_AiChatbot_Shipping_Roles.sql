/* ============================================================================
   TechCare — Migration 003 (DRAFT, CHUA CHAY — DE NICOLE TU IMPORT SAU REVIEW)
   ----------------------------------------------------------------------------
   Muc dich: bo sung schema cho 3 tinh nang moi (theo tai lieu
   Phan-tich-thiet-ke-AI-RAG-Vanchuyen-Role-TechCare.md):
   1) Chatbot AI theo RAG: schema Ai (tai lieu tri thuc, chunk + embedding,
      phien chat, tin nhan).
   2) Tich hop van chuyen (GHN truoc): schema Shipping (van don, su kien
      hanh trinh phuc vu timeline + ban do theo doi cho khach).
   3) Role SUPPORT (ho tro khach hang) + 6 permission moi va gan quyen san.

   Dieu kien tien quyet: da chay script goc TechCare_Database.sql va
   Migration 002 (Sessions). Script nay idempotent (chay lai khong loi),
   chay trong 1 transaction, loi thi rollback.

   Co so doi chieu cot bang Security (tu script goc Nicole cung cap):
   Roles(RoleId int, RoleCode unique, RoleName, Description, IsSystemRole),
   Permissions(PermissionId int, PermissionCode unique, PermissionName,
   ModuleCode, Description), RolePermissions(RoleId, PermissionId).

   QUYET DINH DA GHI SAN (doi y thi sua truoc khi chay):
   - Embedding luu varbinary(max) dang float32 little-endian; SQL Server
     2019 chua co kieu VECTOR nen tinh cosine o tang ung dung. Cot
     EmbeddingDim giu so chieu de phat hien lech model.
   - Role SUPPORT duoc gan san: AI.CHAT.VIEW, AI.CHAT.REPLY,
     SHIPPING.VIEW, CUSTOMER.READ, REPAIR.READ (2 ma cuoi chi gan neu
     da ton tai trong bang Permissions).
   - ADMIN nhan tat ca permission moi; MANAGER: AI.CHAT.VIEW +
     SHIPPING.*; RECEPTION: SHIPPING.VIEW + SHIPPING.CREATE (tao van
     don chieu khach gui may den); CASHIER: SHIPPING.VIEW (doi soat COD).
   - Khong co khoa API nao trong script nay (khoa nam o env/user-secrets).
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY

/* ------------------------------------------------------------------ */
/* Buoc 1: Schema moi                                                  */
/* ------------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Ai')
    EXEC (N'CREATE SCHEMA Ai');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Shipping')
    EXEC (N'CREATE SCHEMA Shipping');

/* ------------------------------------------------------------------ */
/* Buoc 2: Bang tri thuc AI (RAG)                                      */
/* ------------------------------------------------------------------ */
IF OBJECT_ID(N'Ai.KnowledgeDocuments', N'U') IS NULL
CREATE TABLE Ai.KnowledgeDocuments (
    DocumentId      bigint IDENTITY(1,1) CONSTRAINT PK_KnowledgeDocuments PRIMARY KEY,
    Title           nvarchar(300)  NOT NULL,
    Category        nvarchar(100)  NULL,
    SourceType      nvarchar(20)   NOT NULL CONSTRAINT DF_KnowledgeDocuments_SourceType DEFAULT N'TEXT'
                    CONSTRAINT CK_KnowledgeDocuments_SourceType CHECK (SourceType IN (N'TEXT', N'FILE', N'URL')),
    OriginalFileName nvarchar(260) NULL,
    RawContent      nvarchar(max)  NOT NULL,
    Status          nvarchar(20)   NOT NULL CONSTRAINT DF_KnowledgeDocuments_Status DEFAULT N'ACTIVE'
                    CONSTRAINT CK_KnowledgeDocuments_Status CHECK (Status IN (N'DRAFT', N'ACTIVE', N'DISABLED')),
    CreatedByUserId bigint         NOT NULL CONSTRAINT FK_KnowledgeDocuments_CreatedBy
                    REFERENCES Security.Users(UserId),
    CreatedAtUtc    datetime2(0)   NOT NULL CONSTRAINT DF_KnowledgeDocuments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc    datetime2(0)   NOT NULL CONSTRAINT DF_KnowledgeDocuments_UpdatedAt DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID(N'Ai.KnowledgeChunks', N'U') IS NULL
CREATE TABLE Ai.KnowledgeChunks (
    ChunkId         bigint IDENTITY(1,1) CONSTRAINT PK_KnowledgeChunks PRIMARY KEY,
    DocumentId      bigint         NOT NULL CONSTRAINT FK_KnowledgeChunks_Document
                    REFERENCES Ai.KnowledgeDocuments(DocumentId) ON DELETE CASCADE,
    ChunkIndex      int            NOT NULL,
    Content         nvarchar(max)  NOT NULL,
    TokenCount      int            NULL,
    Embedding       varbinary(max) NULL,   -- float32[] little-endian; NULL = chua nhung / che do tu khoa
    EmbeddingModel  nvarchar(100)  NULL,
    EmbeddingDim    int            NULL,
    CreatedAtUtc    datetime2(0)   NOT NULL CONSTRAINT DF_KnowledgeChunks_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_KnowledgeChunks_Doc_Index UNIQUE (DocumentId, ChunkIndex)
);

/* ------------------------------------------------------------------ */
/* Buoc 3: Bang hoi thoai chat                                         */
/* ------------------------------------------------------------------ */
IF OBJECT_ID(N'Ai.ChatSessions', N'U') IS NULL
CREATE TABLE Ai.ChatSessions (
    SessionId         uniqueidentifier NOT NULL CONSTRAINT PK_ChatSessions PRIMARY KEY
                      CONSTRAINT DF_ChatSessions_Id DEFAULT NEWID(),
    CustomerId        bigint        NULL CONSTRAINT FK_ChatSessions_Customer
                      REFERENCES CRM.Customers(CustomerId),
    GuestName         nvarchar(150) NULL,
    GuestPhone        nvarchar(20)  NULL,
    Status            nvarchar(20)  NOT NULL CONSTRAINT DF_ChatSessions_Status DEFAULT N'BOT'
                      CONSTRAINT CK_ChatSessions_Status CHECK (Status IN (N'BOT', N'HUMAN', N'CLOSED')),
    AssignedSupportId bigint        NULL CONSTRAINT FK_ChatSessions_Support
                      REFERENCES Security.Users(UserId),
    StartedAtUtc      datetime2(0)  NOT NULL CONSTRAINT DF_ChatSessions_StartedAt DEFAULT SYSUTCDATETIME(),
    LastMessageAtUtc  datetime2(0)  NOT NULL CONSTRAINT DF_ChatSessions_LastMsg DEFAULT SYSUTCDATETIME(),
    ClosedAtUtc       datetime2(0)  NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatSessions_Status_LastMsg'
               AND object_id = OBJECT_ID(N'Ai.ChatSessions'))
    CREATE INDEX IX_ChatSessions_Status_LastMsg
        ON Ai.ChatSessions (Status, LastMessageAtUtc DESC);

IF OBJECT_ID(N'Ai.ChatMessages', N'U') IS NULL
CREATE TABLE Ai.ChatMessages (
    MessageId     bigint IDENTITY(1,1) CONSTRAINT PK_ChatMessages PRIMARY KEY,
    SessionId     uniqueidentifier NOT NULL CONSTRAINT FK_ChatMessages_Session
                  REFERENCES Ai.ChatSessions(SessionId) ON DELETE CASCADE,
    SenderType    nvarchar(20)  NOT NULL
                  CONSTRAINT CK_ChatMessages_SenderType CHECK (SenderType IN (N'CUSTOMER', N'BOT', N'SUPPORT', N'SYSTEM')),
    SenderUserId  bigint        NULL CONSTRAINT FK_ChatMessages_SenderUser
                  REFERENCES Security.Users(UserId),
    Content       nvarchar(max) NOT NULL,
    SourcesJson   nvarchar(max) NULL,   -- trich dan nguon RAG (JSON), chi co o tin cua BOT
    CreatedAtUtc  datetime2(0)  NOT NULL CONSTRAINT DF_ChatMessages_CreatedAt DEFAULT SYSUTCDATETIME()
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_Session_Time'
               AND object_id = OBJECT_ID(N'Ai.ChatMessages'))
    CREATE INDEX IX_ChatMessages_Session_Time
        ON Ai.ChatMessages (SessionId, CreatedAtUtc);

/* ------------------------------------------------------------------ */
/* Buoc 4: Bang van chuyen                                             */
/* ------------------------------------------------------------------ */
IF OBJECT_ID(N'Shipping.Shipments', N'U') IS NULL
CREATE TABLE Shipping.Shipments (
    ShipmentId        bigint IDENTITY(1,1) CONSTRAINT PK_Shipments PRIMARY KEY,
    CarrierCode       nvarchar(30)   NOT NULL,           -- 'GHN' (sau nay: 'GHTK', ...)
    WaybillCode       nvarchar(60)   NOT NULL,           -- ma van don cua hang (GHN order_code)
    Direction         nvarchar(10)   NOT NULL
                      CONSTRAINT CK_Shipments_Direction CHECK (Direction IN (N'INBOUND', N'OUTBOUND')),
    AppointmentId     bigint         NULL CONSTRAINT FK_Shipments_Appointment
                      REFERENCES CRM.Appointments(AppointmentId),
    RepairTicketId    bigint         NULL CONSTRAINT FK_Shipments_Ticket
                      REFERENCES Repair.RepairTickets(RepairTicketId),
    SenderName        nvarchar(150)  NOT NULL,
    SenderPhone       nvarchar(20)   NOT NULL,
    SenderAddress     nvarchar(500)  NOT NULL,
    SenderProvinceCode nvarchar(20)  NULL,               -- ma theo danh muc cua hang van chuyen
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
    FeeAmount         decimal(18,0)  NULL,               -- phi thuc te hang tra ve
    FeePayer          nvarchar(10)   NULL
                      CONSTRAINT CK_Shipments_FeePayer CHECK (FeePayer IN (N'SENDER', N'RECEIVER')),
    StatusCode        nvarchar(30)   NOT NULL CONSTRAINT DF_Shipments_Status DEFAULT N'CREATED',
                      -- CREATED, PICKING, PICKED, IN_TRANSIT, OUT_FOR_DELIVERY, DELIVERED,
                      -- DELIVERY_FAILED, RETURNING, RETURNED, CANCELLED, EXCEPTION
    ExpectedDeliveryAtUtc datetime2(0) NULL,
    CreatedByUserId   bigint         NOT NULL CONSTRAINT FK_Shipments_CreatedBy
                      REFERENCES Security.Users(UserId),
    CreatedAtUtc      datetime2(0)   NOT NULL CONSTRAINT DF_Shipments_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc      datetime2(0)   NOT NULL CONSTRAINT DF_Shipments_UpdatedAt DEFAULT SYSUTCDATETIME(),
    RawCreateResponseJson nvarchar(max) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Shipments_Carrier_Waybill'
               AND object_id = OBJECT_ID(N'Shipping.Shipments'))
    CREATE UNIQUE INDEX UX_Shipments_Carrier_Waybill
        ON Shipping.Shipments (CarrierCode, WaybillCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_Ticket'
               AND object_id = OBJECT_ID(N'Shipping.Shipments'))
    CREATE INDEX IX_Shipments_Ticket ON Shipping.Shipments (RepairTicketId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_Appointment'
               AND object_id = OBJECT_ID(N'Shipping.Shipments'))
    CREATE INDEX IX_Shipments_Appointment ON Shipping.Shipments (AppointmentId);

IF OBJECT_ID(N'Shipping.ShipmentEvents', N'U') IS NULL
CREATE TABLE Shipping.ShipmentEvents (
    EventId        bigint IDENTITY(1,1) CONSTRAINT PK_ShipmentEvents PRIMARY KEY,
    ShipmentId     bigint         NOT NULL CONSTRAINT FK_ShipmentEvents_Shipment
                   REFERENCES Shipping.Shipments(ShipmentId) ON DELETE CASCADE,
    ProviderStatus nvarchar(60)   NOT NULL,              -- ma trang thai tho cua hang (vd GHN 'transporting')
    InternalStatus nvarchar(30)   NOT NULL,              -- trang thai noi bo da anh xa
    Description    nvarchar(500)  NULL,
    LocationText   nvarchar(300)  NULL,                  -- dia danh kho/buu cuc tu su kien
    Latitude       decimal(9,6)   NULL,                  -- toa do doi chieu tinh (neu co), phuc vu ban do
    Longitude      decimal(9,6)   NULL,
    OccurredAtUtc  datetime2(0)   NOT NULL,
    ReceivedAtUtc  datetime2(0)   NOT NULL CONSTRAINT DF_ShipmentEvents_ReceivedAt DEFAULT SYSUTCDATETIME(),
    RawPayloadJson nvarchar(max)  NULL
);
-- Unique nay la khoa chong trung webhook: GHN gui lai su kien thi bo qua, khong nhan doi
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ShipmentEvents_Dedup'
               AND object_id = OBJECT_ID(N'Shipping.ShipmentEvents'))
    CREATE UNIQUE INDEX UX_ShipmentEvents_Dedup
        ON Shipping.ShipmentEvents (ShipmentId, ProviderStatus, OccurredAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShipmentEvents_Shipment_Time'
               AND object_id = OBJECT_ID(N'Shipping.ShipmentEvents'))
    CREATE INDEX IX_ShipmentEvents_Shipment_Time
        ON Shipping.ShipmentEvents (ShipmentId, OccurredAtUtc);

/* ------------------------------------------------------------------ */
/* Buoc 5: Role SUPPORT + permission moi + gan quyen                   */
/* ------------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM Security.Roles WHERE RoleCode = N'SUPPORT')
    INSERT INTO Security.Roles (RoleCode, RoleName, Description, IsSystemRole)
    VALUES (N'SUPPORT', N'Ho tro khach hang', N'Tiep nhan chat AI chuyen nguoi, ho tro tra cuu va van don', 1);

DECLARE @NewPermissions TABLE (PermissionCode nvarchar(100), PermissionName nvarchar(150), ModuleCode nvarchar(50));
INSERT INTO @NewPermissions (PermissionCode, PermissionName, ModuleCode) VALUES
    (N'AI.KB.MANAGE',      N'Quan ly tri thuc AI',        N'AI'),
    (N'AI.CHAT.VIEW',      N'Xem hoi thoai chat AI',      N'AI'),
    (N'AI.CHAT.REPLY',     N'Tra loi chat thay bot',      N'AI'),
    (N'SHIPPING.VIEW',     N'Xem van don',                N'SHIPPING'),
    (N'SHIPPING.CREATE',   N'Tao van don',                N'SHIPPING'),
    (N'SHIPPING.MANAGE',   N'Huy va cau hinh van don',    N'SHIPPING');

INSERT INTO Security.Permissions (PermissionCode, PermissionName, ModuleCode, Description)
SELECT np.PermissionCode, np.PermissionName, np.ModuleCode, NULL
FROM @NewPermissions np
WHERE NOT EXISTS (SELECT 1 FROM Security.Permissions p WHERE p.PermissionCode = np.PermissionCode);

-- Bang gan quyen: (RoleCode, PermissionCode) — chi chen dong chua co;
-- permission chua ton tai trong DB se tu bi bo qua nho phep JOIN.
DECLARE @Grants TABLE (RoleCode nvarchar(50), PermissionCode nvarchar(100));
INSERT INTO @Grants (RoleCode, PermissionCode) VALUES
    -- ADMIN: tat ca quyen moi
    (N'ADMIN', N'AI.KB.MANAGE'), (N'ADMIN', N'AI.CHAT.VIEW'), (N'ADMIN', N'AI.CHAT.REPLY'),
    (N'ADMIN', N'SHIPPING.VIEW'), (N'ADMIN', N'SHIPPING.CREATE'), (N'ADMIN', N'SHIPPING.MANAGE'),
    -- MANAGER
    (N'MANAGER', N'AI.CHAT.VIEW'),
    (N'MANAGER', N'SHIPPING.VIEW'), (N'MANAGER', N'SHIPPING.CREATE'), (N'MANAGER', N'SHIPPING.MANAGE'),
    -- SUPPORT: chat + xem van don + doc thong tin khach/phieu de tra loi
    (N'SUPPORT', N'AI.CHAT.VIEW'), (N'SUPPORT', N'AI.CHAT.REPLY'), (N'SUPPORT', N'SHIPPING.VIEW'),
    (N'SUPPORT', N'CUSTOMER.READ'), (N'SUPPORT', N'REPAIR.READ'),
    -- RECEPTION: tao van don chieu khach gui may den
    (N'RECEPTION', N'SHIPPING.VIEW'), (N'RECEPTION', N'SHIPPING.CREATE'),
    -- CASHIER: xem van don de doi soat COD
    (N'CASHIER', N'SHIPPING.VIEW');

INSERT INTO Security.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM @Grants g
JOIN Security.Roles r       ON r.RoleCode = g.RoleCode
JOIN Security.Permissions p ON p.PermissionCode = g.PermissionCode
WHERE NOT EXISTS (
    SELECT 1 FROM Security.RolePermissions rp
    WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
);

COMMIT TRANSACTION;
PRINT N'Migration 003 OK: schema Ai + Shipping, role SUPPORT, 6 permission moi.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'Migration 003 LOI — da rollback. Chi tiet:';
    THROW;
END CATCH;

/* ------------------------------------------------------------------ */
/* Kiem tra sau khi chay (chay tay cac lenh duoi day)                  */
/* ------------------------------------------------------------------ */
-- SELECT name FROM sys.schemas WHERE name IN (N'Ai', N'Shipping');
-- SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
--  WHERE s.name IN (N'Ai', N'Shipping') ORDER BY s.name, t.name;
-- SELECT RoleCode FROM Security.Roles WHERE RoleCode = N'SUPPORT';
-- SELECT PermissionCode FROM Security.Permissions
--  WHERE PermissionCode LIKE N'AI.%' OR PermissionCode LIKE N'SHIPPING.%' ORDER BY PermissionCode;
-- SELECT r.RoleCode, p.PermissionCode FROM Security.RolePermissions rp
--   JOIN Security.Roles r ON r.RoleId = rp.RoleId
--   JOIN Security.Permissions p ON p.PermissionId = rp.PermissionId
--  WHERE p.PermissionCode LIKE N'AI.%' OR p.PermissionCode LIKE N'SHIPPING.%'
--     OR r.RoleCode = N'SUPPORT' ORDER BY r.RoleCode, p.PermissionCode;
