/* LOTUS PMS V3 — Read-only SQL Server post-test validation. Use on DEMO DB only.
   Run as script in DBeaver after qa/07_acceptance_e2e_windows.ps1.
*/
USE [HotelChainPms];
GO
-- The first result must be empty: no duplicate non-null operation keys.
SELECT idempotency_key, COUNT(*) AS duplicate_count
FROM dbo.payments
WHERE idempotency_key IS NOT NULL
GROUP BY idempotency_key
HAVING COUNT(*) > 1;

-- Payment ledger, including negative refund rows and associated reservation status.
SELECT TOP (100) p.id, p.reservation_id, r.code, r.status AS reservation_status,
       p.kind, p.amount, p.method, p.reference, p.idempotency_key, p.paid_at_utc
FROM dbo.payments p
JOIN dbo.reservations r ON r.id=p.reservation_id
ORDER BY p.id DESC;

-- Bookings with deposits that are cancelled while still holding net cash. MUST return zero rows.
SELECT r.id, r.code, SUM(p.amount) AS net_amount_after_cancellation
FROM dbo.reservations r
JOIN dbo.payments p ON p.reservation_id=r.id
WHERE r.status='CANCELLED'
GROUP BY r.id, r.code
HAVING SUM(p.amount) <> 0;

-- Each actual occupant should be associated with the correct booking/group room.
SELECT TOP(100) r.group_code, r.code, room.number AS room_number,
       occupant.id, occupant.full_name, occupant.nationality, occupant.is_primary
FROM dbo.reservation_occupants occupant
JOIN dbo.reservations r ON occupant.reservation_id=r.id
JOIN dbo.rooms room ON r.room_id=room.id
ORDER BY occupant.id DESC;

-- Room capacity overflow must be absent.
SELECT r.id AS reservation_id, r.code, rt.capacity, COUNT(o.id) AS occupant_count
FROM dbo.reservations r
JOIN dbo.rooms room ON room.id=r.room_id
JOIN dbo.room_types rt ON rt.id=room.room_type_id
JOIN dbo.reservation_occupants o ON o.reservation_id=r.id
GROUP BY r.id, r.code, rt.capacity
HAVING COUNT(o.id)>rt.capacity;

-- Inspect folio vs payments, debt amounts and housekeeping of recent checkouts.
SELECT TOP(100) r.id, r.code, r.status,
    COALESCE((SELECT SUM(c.amount) FROM dbo.folio_charges c WHERE c.reservation_id=r.id),0) AS charge_amount,
    COALESCE((SELECT SUM(p.amount) FROM dbo.payments p WHERE p.reservation_id=r.id),0) AS payment_amount,
    debt.status AS debt_status, debt.outstanding_amount AS debt_outstanding,
    room.number AS room_number, room.housekeeping_status
FROM dbo.reservations r
JOIN dbo.rooms room ON room.id=r.room_id
LEFT JOIN dbo.debt_receivables debt ON debt.reservation_id=r.id
ORDER BY r.id DESC;
