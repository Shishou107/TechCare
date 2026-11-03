/* ============================================================================
   TechCare — Migration 004 (DRAFT, CHUA CHAY — DE NICOLE TU IMPORT)
   ----------------------------------------------------------------------------
   Muc dich: bo sung toa do hai dau cho van don de ban do theo doi dung
   du lieu that (FIX-1 trong KE-HOACH-SUA-CHUA-TechCare-Dot4.md).
   Them 4 cot nullable vao Shipping.Shipments:
     SenderLatitude, SenderLongitude, ReceiverLatitude, ReceiverLongitude
   Toa do su kien da co san o Shipping.ShipmentEvents (migration 003),
   khong can sua.

   Dieu kien: da chay migration 003. Idempotent, 1 transaction.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY

IF COL_LENGTH(N'Shipping.Shipments', N'SenderLatitude') IS NULL
    ALTER TABLE Shipping.Shipments ADD SenderLatitude decimal(9,6) NULL;
IF COL_LENGTH(N'Shipping.Shipments', N'SenderLongitude') IS NULL
    ALTER TABLE Shipping.Shipments ADD SenderLongitude decimal(9,6) NULL;
IF COL_LENGTH(N'Shipping.Shipments', N'ReceiverLatitude') IS NULL
    ALTER TABLE Shipping.Shipments ADD ReceiverLatitude decimal(9,6) NULL;
IF COL_LENGTH(N'Shipping.Shipments', N'ReceiverLongitude') IS NULL
    ALTER TABLE Shipping.Shipments ADD ReceiverLongitude decimal(9,6) NULL;

COMMIT TRANSACTION;
PRINT N'Migration 004 OK: Shipments da co toa do hai dau.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'Migration 004 LOI — da rollback.';
    THROW;
END CATCH;

-- Kiem tra sau chay:
-- SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
--  WHERE TABLE_SCHEMA = N'Shipping' AND TABLE_NAME = N'Shipments'
--    AND COLUMN_NAME LIKE N'%Latitude' OR COLUMN_NAME LIKE N'%Longitude';
