/* LOTUS PMS V3 QA SAFETY PATCH. Must run AFTER sql/03-v2-upgrade.sql on a V1/V2 DB.
   On a brand-new DB started with V3 backend, EnsureCreatedAsync generates these fields.
   BACK UP THE DATABASE BEFORE ALTERING SCHEMA. DBeaver SQL Server script editor may
   require executing the GO-separated statements as a script (Alt+X), not as one statement.
*/
USE [HotelChainPms];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.payments', 'idempotency_key') IS NULL
    ALTER TABLE dbo.payments ADD idempotency_key nvarchar(80) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_payments_idempotency_key' AND object_id = OBJECT_ID('dbo.payments'))
    CREATE UNIQUE INDEX IX_payments_idempotency_key ON dbo.payments(idempotency_key) WHERE idempotency_key IS NOT NULL;

IF OBJECT_ID('dbo.reservation_occupants', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.reservation_occupants (
      id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
      reservation_id int NOT NULL,
      full_name nvarchar(150) NOT NULL,
      phone nvarchar(30) NOT NULL,
      nationality nvarchar(80) NOT NULL,
      identity_last4 nvarchar(4) NOT NULL,
      is_primary bit NOT NULL,
      created_at_utc datetime2 NOT NULL,
      CONSTRAINT fk_reservation_occupants_booking FOREIGN KEY(reservation_id) REFERENCES dbo.reservations(id)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_reservation_occupants_reservation_id' AND object_id=OBJECT_ID('dbo.reservation_occupants'))
    CREATE INDEX IX_reservation_occupants_reservation_id ON dbo.reservation_occupants(reservation_id);
COMMIT TRANSACTION;
GO
SELECT 'V3 QA SAFETY MIGRATION COMPLETE' AS status;
SELECT COUNT(*) AS number_of_reservation_occupants FROM dbo.reservation_occupants;
