/* LOTUS PMS V2 - SQL Server data-preserving upgrade for existing V1 demo DB.
   Run in DBeaver with database HotelChainPms selected BEFORE starting V2 backend.
   Backup V1 database first. Re-running is idempotent for table/column creation.
   IMPORTANT: designed for V1 schema only, not a general EF migrations system.
*/
USE [HotelChainPms];
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.rooms', 'building_id') IS NULL ALTER TABLE dbo.rooms ADD building_id int NULL;
IF COL_LENGTH('dbo.reservations', 'agency_id') IS NULL ALTER TABLE dbo.reservations ADD agency_id int NULL;
IF COL_LENGTH('dbo.folio_charges', 'service_item_id') IS NULL ALTER TABLE dbo.folio_charges ADD service_item_id int NULL;

IF OBJECT_ID('dbo.hotel_buildings','U') IS NULL
BEGIN
CREATE TABLE dbo.hotel_buildings (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, code nvarchar(30) NOT NULL, name nvarchar(160) NOT NULL,
 CONSTRAINT fk_hotel_buildings_property FOREIGN KEY(property_id) REFERENCES dbo.properties(id),
 CONSTRAINT uq_hotel_buildings UNIQUE(property_id,code));
END;
IF OBJECT_ID('dbo.rate_plans','U') IS NULL
BEGIN
CREATE TABLE dbo.rate_plans (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, code nvarchar(30) NOT NULL, name nvarchar(120) NOT NULL, mode nvarchar(20) NOT NULL,
 base_price decimal(18,2) NOT NULL, included_hours int NOT NULL, extra_hour_price decimal(18,2) NOT NULL, is_active bit NOT NULL,
 CONSTRAINT fk_rate_plans_property FOREIGN KEY(property_id) REFERENCES dbo.properties(id), CONSTRAINT uq_rate_plans UNIQUE(property_id,code));
END;
IF OBJECT_ID('dbo.waitlist_entries','U') IS NULL
BEGIN
CREATE TABLE dbo.waitlist_entries (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, room_type_id int NULL, guest_name nvarchar(150) NOT NULL,
 phone nvarchar(30) NOT NULL, check_in_date date NOT NULL, check_out_date date NOT NULL, status nvarchar(20) NOT NULL, note nvarchar(500) NULL,
 reservation_id int NULL, created_at_utc datetime2 NOT NULL,
 CONSTRAINT fk_waitlist_properties FOREIGN KEY(property_id) REFERENCES dbo.properties(id),
 CONSTRAINT fk_waitlist_room_types FOREIGN KEY(room_type_id) REFERENCES dbo.room_types(id),
 CONSTRAINT fk_waitlist_reservations FOREIGN KEY(reservation_id) REFERENCES dbo.reservations(id));
END;
IF OBJECT_ID('dbo.suppliers','U') IS NULL
BEGIN
CREATE TABLE dbo.suppliers (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, code nvarchar(30) NOT NULL, name nvarchar(160) NOT NULL,
 phone nvarchar(50) NOT NULL, address nvarchar(250) NULL,
 CONSTRAINT fk_suppliers_properties FOREIGN KEY(property_id) REFERENCES dbo.properties(id), CONSTRAINT uq_suppliers UNIQUE(property_id,code));
END;
IF OBJECT_ID('dbo.stock_movements','U') IS NULL
BEGIN
CREATE TABLE dbo.stock_movements (id bigint IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, service_item_id int NOT NULL, [type] nvarchar(24) NOT NULL,
 quantity_change int NOT NULL, balance_after int NOT NULL, unit_cost decimal(18,2) NOT NULL, reference nvarchar(150) NULL, supplier_id int NULL,
 actor_user_id int NOT NULL, created_at_utc datetime2 NOT NULL,
 CONSTRAINT fk_stock_properties FOREIGN KEY(property_id) REFERENCES dbo.properties(id),
 CONSTRAINT fk_stock_services FOREIGN KEY(service_item_id) REFERENCES dbo.services(id),
 CONSTRAINT fk_stock_suppliers FOREIGN KEY(supplier_id) REFERENCES dbo.suppliers(id));
END;
IF OBJECT_ID('dbo.retail_sales','U') IS NULL
BEGIN
CREATE TABLE dbo.retail_sales (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, service_item_id int NOT NULL, shift_id int NULL, quantity int NOT NULL,
 unit_price decimal(18,2) NOT NULL, total_amount decimal(18,2) NOT NULL, payment_method nvarchar(20) NOT NULL, actor_user_id int NOT NULL,
 sold_at_utc datetime2 NOT NULL, CONSTRAINT fk_retail_services FOREIGN KEY(service_item_id) REFERENCES dbo.services(id));
END;
IF COL_LENGTH('dbo.retail_sales', 'shift_id') IS NULL ALTER TABLE dbo.retail_sales ADD shift_id int NULL;
IF OBJECT_ID('dbo.cash_vouchers','U') IS NULL
BEGIN
CREATE TABLE dbo.cash_vouchers (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, code nvarchar(30) NOT NULL, [type] nvarchar(16) NOT NULL,
 method nvarchar(20) NOT NULL, category nvarchar(160) NOT NULL, description nvarchar(500) NOT NULL, amount decimal(18,2) NOT NULL,
 actor_user_id int NOT NULL, shift_id int NULL, created_at_utc datetime2 NOT NULL,
 CONSTRAINT fk_vouchers_property FOREIGN KEY(property_id) REFERENCES dbo.properties(id), CONSTRAINT uq_vouchers UNIQUE(property_id,code));
END;
IF OBJECT_ID('dbo.debt_receivables','U') IS NULL
BEGIN
CREATE TABLE dbo.debt_receivables (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, reservation_id int NOT NULL,
 original_amount decimal(18,2) NOT NULL, outstanding_amount decimal(18,2) NOT NULL, status nvarchar(20) NOT NULL, note nvarchar(400) NULL,
 created_at_utc datetime2 NOT NULL, settled_at_utc datetime2 NULL,
 CONSTRAINT fk_debts_reservation FOREIGN KEY(reservation_id) REFERENCES dbo.reservations(id), CONSTRAINT uq_debts_reservation UNIQUE(reservation_id));
END;
IF OBJECT_ID('dbo.guest_assets','U') IS NULL
BEGIN
CREATE TABLE dbo.guest_assets (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, stay_id int NOT NULL, description nvarchar(200) NOT NULL,
 quantity int NOT NULL, note nvarchar(500) NULL, received_at_utc datetime2 NOT NULL, returned_at_utc datetime2 NULL,
 CONSTRAINT fk_assets_stay FOREIGN KEY(stay_id) REFERENCES dbo.stays(id));
END;
IF OBJECT_ID('dbo.daily_closes','U') IS NULL
BEGIN
CREATE TABLE dbo.daily_closes (id int IDENTITY(1,1) PRIMARY KEY, property_id int NOT NULL, business_date date NOT NULL,
 room_charges decimal(18,2) NOT NULL, service_charges decimal(18,2) NOT NULL, received_amount decimal(18,2) NOT NULL,
 actor_user_id int NOT NULL, created_at_utc datetime2 NOT NULL,
 CONSTRAINT fk_closes_properties FOREIGN KEY(property_id) REFERENCES dbo.properties(id), CONSTRAINT uq_closes UNIQUE(property_id,business_date));
END;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='fk_rooms_building_v2')
 ALTER TABLE dbo.rooms WITH CHECK ADD CONSTRAINT fk_rooms_building_v2 FOREIGN KEY(building_id) REFERENCES dbo.hotel_buildings(id);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='fk_reservations_agency_v2')
 ALTER TABLE dbo.reservations WITH CHECK ADD CONSTRAINT fk_reservations_agency_v2 FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='fk_charges_service_v2')
 ALTER TABLE dbo.folio_charges WITH CHECK ADD CONSTRAINT fk_charges_service_v2 FOREIGN KEY(service_item_id) REFERENCES dbo.services(id);
COMMIT TRANSACTION;
GO
SELECT 'V2 UPGRADE COMPLETE' AS migration_status;
SELECT name FROM sys.tables WHERE name IN ('hotel_buildings','rate_plans','waitlist_entries','suppliers','stock_movements',
 'retail_sales','cash_vouchers','debt_receivables','guest_assets','daily_closes');
