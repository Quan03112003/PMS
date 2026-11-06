/**********************************************************************************************
 * LOTUS PMS V3 — SCRIPT KIỂM TRA DỮ LIỆU TEST (VERIFY)
 * Chạy trong DBeaver trên database HotelChainPms.
 **********************************************************************************************/

USE [HotelChainPms];
GO

PRINT '=== 1. CƠ SỞ KHÁCH SẢN ==='; GO
SELECT id, code, name, city, is_active FROM dbo.properties ORDER BY id;

PRINT '=== 2. HẠNG PHÒNG ==='; GO
SELECT p.code AS property_code, rt.code AS room_type_code, rt.name, rt.capacity, rt.base_rate
FROM dbo.room_types rt JOIN dbo.properties p ON p.id = rt.property_id ORDER BY p.code, rt.code;

PRINT '=== 3. PHÒNG (THEO CƠ SỞ) ==='; GO
SELECT p.code AS property_code, r.number, r.floor, rt.name AS room_type, r.housekeeping_status, r.is_out_of_order
FROM dbo.rooms r JOIN dbo.properties p ON p.id = r.property_id
JOIN dbo.room_types rt ON rt.id = r.room_type_id ORDER BY p.code, r.number;

PRINT '=== 4. KHÁCH HÀNG (TOP 20) ==='; GO
SELECT TOP 20 id, full_name, phone, email, nationality FROM dbo.guests ORDER BY id;

PRINT '=== 5. ĐẶT PHÒNG (RESERVATIONS) ==='; GO
SELECT r.code, p.code AS property_code, r.number AS room_no, g.full_name AS guest,
       r.check_in_date, r.check_out_date, r.adults, r.children, r.nightly_rate,
       r.status, r.booking_source, r.booking_mode
FROM dbo.reservations r
JOIN dbo.properties p ON p.id = r.property_id
JOIN dbo.rooms ro ON ro.id = r.room_id
JOIN dbo.guests g ON g.id = r.guest_id
ORDER BY r.id;

PRINT '=== 6. KHÁCH ĐANG Ở (STAYS) ==='; GO
SELECT s.id, r.code AS reservation_code, s.checked_in_at_utc, s.status
FROM dbo.stays s JOIN dbo.reservations r ON r.id = s.reservation_id ORDER BY s.id;

PRINT '=== 7. FOLIO CHARGES ==='; GO
SELECT fc.id, r.code AS reservation_code, fc.category, fc.description, fc.quantity,
       fc.unit_price, fc.amount, fc.posted_at_utc
FROM dbo.folio_charges fc JOIN dbo.reservations r ON r.id = fc.reservation_id
ORDER BY fc.id;

PRINT '=== 8. PAYMENTS ==='; GO
SELECT pmt.id, r.code AS reservation_code, pmt.amount, pmt.method, pmt.kind,
       pmt.reference, pmt.paid_at_utc
FROM dbo.payments pmt JOIN dbo.reservations r ON r.id = pmt.reservation_id
ORDER BY pmt.id;

PRINT '=== 9. CASH SHIFTS ==='; GO
SELECT cs.id, p.code AS property_code, u.full_name AS user_name, cs.status,
       cs.opening_amount, cs.closing_amount, cs.opened_at_utc
FROM dbo.cash_shifts cs
JOIN dbo.properties p ON p.id = cs.property_id
JOIN dbo.app_users u ON u.id = cs.user_id
ORDER BY cs.id;

PRINT '=== 10. CASH VOUCHERS ==='; GO
SELECT cv.id, cv.code, cv.type, cv.method, cv.category, cv.description, cv.amount, cv.created_at_utc
FROM dbo.cash_vouchers cv ORDER BY cv.id;

PRINT '=== 11. DEBT RECEIVABLES ==='; GO
SELECT dr.id, r.code AS reservation_code, dr.original_amount, dr.outstanding_amount,
       dr.status, dr.note
FROM dbo.debt_receivables dr JOIN dbo.reservations r ON r.id = dr.reservation_id
ORDER BY dr.id;

PRINT '=== 12. GUEST ASSETS ==='; GO
SELECT ga.id, ga.description, ga.quantity, ga.note, ga.received_at_utc, ga.returned_at_utc
FROM dbo.guest_assets ga ORDER BY ga.id;

PRINT '=== 13. STOCK MOVEMENTS ==='; GO
SELECT sm.id, p.code AS property_code, si.name AS service_item, sm.type,
       sm.quantity_change, sm.balance_after, sm.reference, sm.created_at_utc
FROM dbo.stock_movements sm
JOIN dbo.properties p ON p.id = sm.property_id
JOIN dbo.service_items si ON si.id = sm.service_item_id
ORDER BY sm.id;

PRINT '=== 14. RATE PLANS ==='; GO
SELECT p.code AS property_code, rp.code, rp.name, rp.mode, rp.base_price,
       rp.included_hours, rp.extra_hour_price
FROM dbo.rate_plans rp JOIN dbo.properties p ON p.id = rp.property_id
ORDER BY p.code, rp.code;

PRINT '=== 15. USERS ==='; GO
SELECT u.id, u.full_name, u.email, u.role, p.code AS property_code, u.is_active
FROM dbo.app_users u LEFT JOIN dbo.properties p ON p.id = u.property_id
ORDER BY u.role, u.email;

PRINT '=== 16. AGENCIES ==='; GO
SELECT p.code AS property_code, a.code, a.name, a.commission_percent
FROM dbo.agencies a JOIN dbo.properties p ON p.id = a.property_id
ORDER BY p.code, a.code;

PRINT '=== 17. BUILDINGS ==='; GO
SELECT p.code AS property_code, b.code, b.name
FROM dbo.hotel_buildings b JOIN dbo.properties p ON p.id = b.property_id
ORDER BY p.code, b.code;

PRINT '=== 18. WAITLIST ==='; GO
SELECT wl.id, p.code AS property_code, wl.guest_name, wl.phone,
       wl.check_in_date, wl.check_out_date, wl.status, wl.note
FROM dbo.waitlist_entries wl JOIN dbo.properties p ON p.id = wl.property_id
ORDER BY wl.id;

PRINT '=== 19. DAILY CLOSES ==='; GO
SELECT dc.id, p.code AS property_code, dc.business_date, dc.room_charges,
       dc.service_charges, dc.received_amount
FROM dbo.daily_closes dc JOIN dbo.properties p ON p.id = dc.property_id
ORDER BY dc.business_date;

PRINT '=== 20. SUPPLIERS ==='; GO
SELECT p.code AS property_code, s.code, s.name, s.phone
FROM dbo.suppliers s JOIN dbo.properties p ON p.id = s.property_id
ORDER BY p.code, s.code;

PRINT '=== 21. RETAIL SALES ==='; GO
SELECT rs.id, p.code AS property_code, si.name AS service_item, rs.quantity,
       rs.unit_price, rs.total_amount, rs.payment_method, rs.sold_at_utc
FROM dbo.retail_sales rs
JOIN dbo.properties p ON p.id = rs.property_id
JOIN dbo.service_items si ON si.id = rs.service_item_id
ORDER BY rs.id;

PRINT '=== KIỂM TRA HOÀN TẤT ==='; GO