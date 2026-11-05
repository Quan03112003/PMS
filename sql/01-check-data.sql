-- Trong DBeaver: chọn database HotelChainPms, mở SQL Editor rồi chạy.
SELECT id, code, name, city FROM dbo.properties;
SELECT TOP (30) id, number, floor, housekeeping_status, is_out_of_order FROM dbo.rooms ORDER BY property_id, floor, number;
SELECT TOP (30) id, code, status, check_in_date, check_out_date FROM dbo.reservations ORDER BY id DESC;
SELECT TOP (30) id, reservation_id, category, description, amount FROM dbo.folio_charges ORDER BY id DESC;
SELECT TOP (30) id, reservation_id, amount, method, kind FROM dbo.payments ORDER BY id DESC;
SELECT TOP (30) id, property_id, actor_user_id, action, entity, occurred_at_utc FROM dbo.audit_events ORDER BY id DESC;
