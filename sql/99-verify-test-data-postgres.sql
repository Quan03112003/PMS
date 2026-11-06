/**********************************************************************************************
 * LOTUS PMS V3 — SCRIPT KIỂM TRA DỮ LIỆU TEST (VERIFY) CHO POSTGRESQL (Neon)
 * Chạy trong DBeaver/pgAdmin trên database Neon.
 **********************************************************************************************/

SELECT '=== 1. CƠ SỞ KHÁCH SẢN ===' AS info;
SELECT id, code, name, city, is_active FROM properties ORDER BY id;

SELECT '=== 2. HẠNG PHÒNG ===' AS info;
SELECT p.code AS property_code, rt.code AS room_type_code, rt.name, rt.capacity, rt.base_rate
FROM room_types rt JOIN properties p ON p.id = rt.property_id ORDER BY p.code, rt.code;

SELECT '=== 3. PHÒNG (THEO CƠ SỞ) ===' AS info;
SELECT p.code AS property_code, r.number, r.floor, rt.name AS room_type, r.housekeeping_status, r.is_out_of_order
FROM rooms r JOIN properties p ON p.id = r.property_id
JOIN room_types rt ON rt.id = r.room_type_id ORDER BY p.code, r.number;

SELECT '=== 4. KHÁCH HÀNG (TOP 20) ===' AS info;
SELECT id, full_name, phone, email, nationality FROM guests ORDER BY id LIMIT 20;

SELECT '=== 5. ĐẶT PHÒNG (RESERVATIONS) ===' AS info;
SELECT r.code, p.code AS property_code, ro.number AS room_no, g.full_name AS guest,
       r.check_in_date, r.check_out_date, r.adults, r.children, r.nightly_rate,
       r.status, r.booking_source, r.booking_mode
FROM reservations r
JOIN properties p ON p.id = r.property_id
JOIN rooms ro ON ro.id = r.room_id
JOIN guests g ON g.id = r.guest_id
ORDER BY r.id;

SELECT '=== 6. KHÁCH ĐANG Ở (STAYS) ===' AS info;
SELECT s.id, r.code AS reservation_code, s.checked_in_at_utc, s.status
FROM stays s JOIN reservations r ON r.id = s.reservation_id ORDER BY s.id;

SELECT '=== 7. FOLIO CHARGES ===' AS info;
SELECT fc.id, r.code AS reservation_code, fc.category, fc.description, fc.quantity,
       fc.unit_price, fc.amount, fc.posted_at_utc
FROM folio_charges fc JOIN reservations r ON r.id = fc.reservation_id
ORDER BY fc.id;

SELECT '=== 8. PAYMENTS ===' AS info;
SELECT pmt.id, r.code AS reservation_code, pmt.amount, pmt.method, pmt.kind,
       pmt.reference, pmt.paid_at_utc
FROM payments pmt JOIN reservations r ON r.id = pmt.reservation_id
ORDER BY pmt.id;

SELECT '=== 9. CASH SHIFTS ===' AS info;
SELECT cs.id, p.code AS property_code, u.full_name AS user_name, cs.status,
       cs.opening_amount, cs.closing_amount, cs.opened_at_utc
FROM cash_shifts cs
JOIN properties p ON p.id = cs.property_id
JOIN app_users u ON u.id = cs.user_id
ORDER BY cs.id;

SELECT '=== 10. CASH VOUCHERS ===' AS info;
SELECT cv.id, cv.code, cv.type, cv.method, cv.category, cv.description, cv.amount, cv.created_at_utc
FROM cash_vouchers cv ORDER BY cv.id;

SELECT '=== 11. DEBT RECEIVABLES ===' AS info;
SELECT dr.id, r.code AS reservation_code, dr.original_amount, dr.outstanding_amount,
       dr.status, dr.note
FROM debt_receivables dr JOIN reservations r ON r.id = dr.reservation_id
ORDER BY dr.id;

SELECT '=== 12. GUEST ASSETS ===' AS info;
SELECT ga.id, ga.description, ga.quantity, ga.note, ga.received_at_utc, ga.returned_at_utc
FROM guest_assets ga ORDER BY ga.id;

SELECT '=== 13. STOCK MOVEMENTS ===' AS info;
SELECT sm.id, p.code AS property_code, si.name AS service_item, sm.type,
       sm.quantity_change, sm.balance_after, sm.reference, sm.created_at_utc
FROM stock_movements sm
JOIN properties p ON p.id = sm.property_id
JOIN service_items si ON si.id = sm.service_item_id
ORDER BY sm.id;

SELECT '=== 14. RATE PLANS ===' AS info;
SELECT p.code AS property_code, rp.code, rp.name, rp.mode, rp.base_price,
       rp.included_hours, rp.extra_hour_price
FROM rate_plans rp JOIN properties p ON p.id = rp.property_id
ORDER BY p.code, rp.code;

SELECT '=== 15. USERS ===' AS info;
SELECT u.id, u.full_name, u.email, u.role, p.code AS property_code, u.is_active
FROM app_users u LEFT JOIN properties p ON p.id = u.property_id
ORDER BY u.role, u.email;

SELECT '=== 16. AGENCIES ===' AS info;
SELECT p.code AS property_code, a.code, a.name, a.commission_percent
FROM agencies a JOIN properties p ON p.id = a.property_id
ORDER BY p.code, a.code;

SELECT '=== 17. BUILDINGS ===' AS info;
SELECT p.code AS property_code, b.code, b.name
FROM hotel_buildings b JOIN properties p ON p.id = b.property_id
ORDER BY p.code, b.code;

SELECT '=== 18. WAITLIST ===' AS info;
SELECT wl.id, p.code AS property_code, wl.guest_name, wl.phone,
       wl.check_in_date, wl.check_out_date, wl.status, wl.note
FROM waitlist_entries wl JOIN properties p ON p.id = wl.property_id
ORDER BY wl.id;

SELECT '=== 19. DAILY CLOSES ===' AS info;
SELECT dc.id, p.code AS property_code, dc.business_date, dc.room_charges,
       dc.service_charges, dc.received_amount
FROM daily_closes dc JOIN properties p ON p.id = dc.property_id
ORDER BY dc.business_date;

SELECT '=== 20. SUPPLIERS ===' AS info;
SELECT p.code AS property_code, s.code, s.name, s.phone
FROM suppliers s JOIN properties p ON p.id = s.property_id
ORDER BY p.code, s.code;

SELECT '=== 21. RETAIL SALES ===' AS info;
SELECT rs.id, p.code AS property_code, si.name AS service_item, rs.quantity,
       rs.unit_price, rs.total_amount, rs.payment_method, rs.sold_at_utc
FROM retail_sales rs
JOIN properties p ON p.id = rs.property_id
JOIN service_items si ON si.id = rs.service_item_id
ORDER BY rs.id;

SELECT '=== KIỂM TRA HOÀN TẤT ===' AS info;