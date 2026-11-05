/* LOTUS PMS V2: read-only assertions after Python E2E runner, for SQL Server in DBeaver.
   Select HotelChainPms as connection database. No rows are modified. */
USE [HotelChainPms];
GO
SELECT TOP (100) r.code, r.status AS booking_status, g.full_name, r.check_in_date, r.check_out_date,
       s.status AS stay_status, s.checked_out_at_utc, room.number AS room_number
FROM dbo.reservations r
JOIN dbo.guests g ON g.id=r.guest_id
JOIN dbo.rooms room ON room.id=r.room_id
LEFT JOIN dbo.stays s ON s.reservation_id=r.id
WHERE g.full_name LIKE 'E2E-%'
ORDER BY r.id DESC;
GO
SELECT TOP (100) d.id, r.code, d.original_amount, d.outstanding_amount, d.status
FROM dbo.debt_receivables d
JOIN dbo.reservations r ON r.id=d.reservation_id
JOIN dbo.guests g ON g.id=r.guest_id
WHERE g.full_name LIKE 'E2E-%'
ORDER BY d.id DESC;
GO
SELECT TOP (80) m.id, m.service_item_id, m.[type], m.quantity_change, m.balance_after, m.reference, m.created_at_utc
FROM dbo.stock_movements m
ORDER BY m.id DESC;
GO
SELECT TOP (80) v.code, v.[type], v.method, v.category, v.amount, v.description, v.created_at_utc
FROM dbo.cash_vouchers v
WHERE v.category = 'E2E_TEST'
ORDER BY v.id DESC;
GO
SELECT COUNT(*) AS negative_inventory_rows
FROM dbo.services WHERE track_inventory = 1 AND stock_quantity < 0;
GO
SELECT TOP (100) r.code, r.status AS reservation_status,
       SUM(CASE WHEN c.id IS NULL THEN 0 ELSE c.amount END) AS charges -- join caution: use separate grouped sums for payments
FROM dbo.reservations r
JOIN dbo.guests g ON g.id=r.guest_id
LEFT JOIN dbo.folio_charges c ON c.reservation_id=r.id
WHERE g.full_name LIKE 'E2E-%'
GROUP BY r.code, r.status
ORDER BY r.code DESC;
GO
