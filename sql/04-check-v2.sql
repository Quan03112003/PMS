USE [HotelChainPms];
GO
SELECT 'hotel_buildings' AS table_name, COUNT(*) AS count_rows FROM dbo.hotel_buildings
UNION ALL SELECT 'rate_plans',COUNT(*) FROM dbo.rate_plans
UNION ALL SELECT 'waitlist_entries',COUNT(*) FROM dbo.waitlist_entries
UNION ALL SELECT 'suppliers',COUNT(*) FROM dbo.suppliers
UNION ALL SELECT 'stock_movements',COUNT(*) FROM dbo.stock_movements
UNION ALL SELECT 'retail_sales',COUNT(*) FROM dbo.retail_sales
UNION ALL SELECT 'cash_vouchers',COUNT(*) FROM dbo.cash_vouchers
UNION ALL SELECT 'debt_receivables',COUNT(*) FROM dbo.debt_receivables
UNION ALL SELECT 'guest_assets',COUNT(*) FROM dbo.guest_assets
UNION ALL SELECT 'daily_closes',COUNT(*) FROM dbo.daily_closes;
SELECT property_id, status, COUNT(*) AS debt_cases, SUM(outstanding_amount) AS total_outstanding FROM dbo.debt_receivables GROUP BY property_id,status;
SELECT property_id, service_item_id, [type], SUM(quantity_change) AS delta FROM dbo.stock_movements GROUP BY property_id,service_item_id,[type] ORDER BY property_id,service_item_id,[type];
SELECT p.code AS property_code, r.number AS room_no, b.name AS building_name FROM dbo.rooms r JOIN dbo.properties p ON p.id=r.property_id LEFT JOIN dbo.hotel_buildings b ON b.id=r.building_id ORDER BY p.code,r.number;
SELECT TOP 50 * FROM dbo.audit_events ORDER BY id DESC;
