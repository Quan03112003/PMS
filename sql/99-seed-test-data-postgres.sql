/**********************************************************************************************
 * LOTUS PMS V3 — SCRIPT CHÈN DỮ LIỆU TEST LỚN CHO POSTGRESQL (Neon)
 * Chạy trên database Neon (PostgreSQL) của bạn.
 * Mục đích: cung cấp nhiều dữ liệu để kiểm thử toàn diện các chức năng PMS.
 *
 * Cách chạy:
 *   psql "postgresql://neondb_owner:npg_Mx9y7ShHUXGu@ep-nameless-frost-aodat4et-pooler.c-2.ap-southeast-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require" -f sql/99-seed-test-data-postgres.sql
 *
 * Hoặc dùng DBeaver/pgAdmin: kết nối Neon, mở file này, chạy toàn bộ.
 **********************************************************************************************/

/**********************************************************************************************
 * 1. THÊM CƠ SỞ KHÁCH SẢN MỚI
 **********************************************************************************************/
INSERT INTO properties (code, name, city, address, phone, check_in_time, check_out_time, is_active)
SELECT 'HCMC-SIGNATURE', 'Lotus Signature TP.HCM', 'TP. Hồ Chí Minh', 'Quận 1, TP. Hồ Chí Minh', '028-8888-2001', '14:00', '12:00', true
WHERE NOT EXISTS (SELECT 1 FROM properties WHERE code = 'HCMC-SIGNATURE');

INSERT INTO properties (code, name, city, address, phone, check_in_time, check_out_time, is_active)
SELECT 'NHA-TRAM', 'Lotus Beach Nha Trang', 'Nha Trang', 'Vĩnh Nguyên, Nha Trang, Khánh Hòa', '0258-8888-3001', '14:00', '12:00', true
WHERE NOT EXISTS (SELECT 1 FROM properties WHERE code = 'NHA-TRAM');

/**********************************************************************************************
 * 2. THÊM HẠNG PHÒNG CHO CƠ SỞ MỚI
 **********************************************************************************************/
INSERT INTO room_types (property_id, code, name, capacity, base_rate)
SELECT p.id, 'STD', 'Standard', 2, 850000
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO room_types (property_id, code, name, capacity, base_rate)
SELECT p.id, 'DLX', 'Deluxe', 2, 1250000
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO room_types (property_id, code, name, capacity, base_rate)
SELECT p.id, 'SUITE', 'Executive Suite', 4, 2200000
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO room_types (property_id, code, name, capacity, base_rate)
SELECT p.id, 'STD', 'Standard Garden', 2, 750000
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO room_types (property_id, code, name, capacity, base_rate)
SELECT p.id, 'DLX', 'Ocean View', 3, 1350000
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO room_types (property_id, code, name, capacity, base_rate)
SELECT p.id, 'VILLA', 'Pool Villa', 6, 3500000
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

/**********************************************************************************************
 * 3. THÊM PHÒNG (50 phòng mới cho 2 cơ sở)
 **********************************************************************************************/
-- HCMC: 4 tầng x 8 phòng = 32 phòng
INSERT INTO rooms (property_id, room_type_id, number, floor, housekeeping_status, is_out_of_order)
SELECT
    p.id,
    CASE WHEN j <= 3 THEN rt_std.id
         WHEN j <= 6 THEN rt_dlx.id
         ELSE rt_suite.id END,
    (i::text || lpad(j::text, 2, '0')),
    i,
    'INSPECTED',
    false
FROM properties p
CROSS JOIN LATERAL generate_series(1, 4) AS i
CROSS JOIN LATERAL generate_series(1, 8) AS j
LEFT JOIN room_types rt_std ON rt_std.property_id = p.id AND rt_std.code = 'STD'
LEFT JOIN room_types rt_dlx ON rt_dlx.property_id = p.id AND rt_dlx.code = 'DLX'
LEFT JOIN room_types rt_suite ON rt_suite.property_id = p.id AND rt_suite.code = 'SUITE'
WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, number) DO NOTHING;

-- Nha Trang: 3 tầng x 6 phòng = 18 phòng
INSERT INTO rooms (property_id, room_type_id, number, floor, housekeeping_status, is_out_of_order)
SELECT
    p.id,
    CASE WHEN j <= 2 THEN rt_std.id
         WHEN j <= 4 THEN rt_dlx.id
         ELSE rt_villa.id END,
    (i::text || lpad(j::text, 2, '0')),
    i,
    'INSPECTED',
    false
FROM properties p
CROSS JOIN LATERAL generate_series(1, 3) AS i
CROSS JOIN LATERAL generate_series(1, 6) AS j
LEFT JOIN room_types rt_std ON rt_std.property_id = p.id AND rt_std.code = 'STD'
LEFT JOIN room_types rt_dlx ON rt_dlx.property_id = p.id AND rt_dlx.code = 'DLX'
LEFT JOIN room_types rt_villa ON rt_villa.property_id = p.id AND rt_villa.code = 'VILLA'
WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, number) DO NOTHING;
/**********************************************************************************************
 * 4. THÊM KHÁCH HÀNG (50 khách hàng test)
 **********************************************************************************************/
INSERT INTO guests (full_name, phone, email, nationality, identity_last4, created_at_utc)
SELECT * FROM (VALUES
    ('Nguyễn Văn An', '0901111111', 'an.nguyen@email.com', 'Việt Nam', '1234', '2026-01-10 08:00:00'::timestamp),
    ('Trần Thị Bình', '0901111112', 'binh.tran@email.com', 'Việt Nam', '5678', '2026-01-11 09:00:00'::timestamp),
    ('Lê Hoàng Cường', '0901111113', 'cuong.le@email.com', 'Việt Nam', '9012', '2026-01-12 10:00:00'::timestamp),
    ('Phạm Dung', '0901111114', 'dung.pham@email.com', 'Việt Nam', '3456', '2026-01-13 11:00:00'::timestamp),
    ('Hoàng Vũ Em', '0901111115', 'em.hoang@email.com', 'Việt Nam', '7890', '2026-01-14 12:00:00'::timestamp),
    ('Vũ Thanh Bình', '0901111116', 'binh.vu@email.com', 'Việt Nam', '2345', '2026-01-15 13:00:00'::timestamp),
    ('Đỗ Minh Chiến', '0901111117', 'chien.do@email.com', 'Việt Nam', '6789', '2026-01-16 14:00:00'::timestamp),
    ('Ngô Thị Duyên', '0901111118', 'duyen.ngo@email.com', 'Việt Nam', '0123', '2026-01-17 15:00:00'::timestamp),
    ('Bùi Hoàng Tuấn', '0901111119', 'tuan.bui@email.com', 'Việt Nam', '4567', '2026-01-18 16:00:00'::timestamp),
    ('Lý Thị Phương', '0901111120', 'phuong.ly@email.com', 'Việt Nam', '8901', '2026-01-19 17:00:00'::timestamp),
    ('Đinh Văn Quang', '0901111121', 'quang.dinh@email.com', 'Việt Nam', '2345', '2026-01-20 08:00:00'::timestamp),
    ('Mai Thị Hà', '0901111122', 'ha.mai@email.com', 'Việt Nam', '6789', '2026-01-21 09:00:00'::timestamp),
    ('Trương Văn Nam', '0901111123', 'nam.truong@email.com', 'Việt Nam', '0123', '2026-01-22 10:00:00'::timestamp),
    ('Tô Thị Oanh', '0901111124', 'oanh.to@email.com', 'Việt Nam', '4567', '2026-01-23 11:00:00'::timestamp),
    ('Võ Văn Phúc', '0901111125', 'phuc.vo@email.com', 'Việt Nam', '8901', '2026-01-24 12:00:00'::timestamp),
    ('Nguyễn Thị Quyên', '0901111126', 'quyen.nguyen@email.com', 'Việt Nam', '2345', '2026-01-25 13:00:00'::timestamp),
    ('Lê Văn Sơn', '0901111127', 'son.le@email.com', 'Việt Nam', '6789', '2026-01-26 14:00:00'::timestamp),
    ('Trần Thị Tâm', '0901111128', 'tam.tran@email.com', 'Việt Nam', '0123', '2026-01-27 15:00:00'::timestamp),
    ('Phạm Đức Vĩnh', '0901111129', 'vinh.pham@email.com', 'Việt Nam', '4567', '2026-01-28 16:00:00'::timestamp),
    ('Đặng Thị Xuân', '0901111130', 'xuan.dang@email.com', 'Việt Nam', '8901', '2026-01-29 08:00:00'::timestamp),
    ('Hoàng Văn Ya', '0901111131', 'ya.hoang@email.com', 'Việt Nam', '2345', '2026-01-30 09:00:00'::timestamp),
    ('Ngô Thị Zen', '0901111132', 'zen.ngo@email.com', 'Việt Nam', '6789', '2026-02-01 10:00:00'::timestamp),
    ('Bùi Văn Ái', '0901111133', 'ai.bui@email.com', 'Việt Nam', '0123', '2026-02-02 11:00:00'::timestamp),
    ('Lý Thị Bảo', '0901111134', 'bao.ly@email.com', 'Việt Nam', '4567', '2026-02-03 12:00:00'::timestamp),
    ('Đinh Văn Cường', '0901111135', 'cuong.dinh@email.com', 'Việt Nam', '8901', '2026-02-04 13:00:00'::timestamp),
    ('Mai Thị Dung', '0901111136', 'dung.mai@email.com', 'Việt Nam', '2345', '2026-02-05 14:00:00'::timestamp),
    ('Trương Văn Em', '0901111137', 'em.truong@email.com', 'Việt Nam', '6789', '2026-02-06 15:00:00'::timestamp),
    ('Tô Thị Fát', '0901111138', 'fat.to@email.com', 'Việt Nam', '0123', '2026-02-07 16:00:00'::timestamp),
    ('Võ Văn Giang', '0901111139', 'giang.vo@email.com', 'Việt Nam', '4567', '2026-02-08 08:00:00'::timestamp),
    ('Nguyễn Thị Hương', '0901111140', 'huong.nguyen@email.com', 'Việt Nam', '8901', '2026-02-09 09:00:00'::timestamp),
    ('Lê Văn Hải', '0901111141', 'hai.le@email.com', 'Việt Nam', '2345', '2026-02-10 10:00:00'::timestamp),
    ('Trần Thị Hoàng', '0901111142', 'hoang.tran@email.com', 'Việt Nam', '6789', '2026-02-11 11:00:00'::timestamp),
    ('Phạm Văn Hưng', '0901111143', 'hung.pham@email.com', 'Việt Nam', '0123', '2026-02-12 12:00:00'::timestamp),
    ('Đặng Thị Lan', '0901111144', 'lan.dang@email.com', 'Việt Nam', '4567', '2026-02-13 13:00:00'::timestamp),
    ('Hoàng Văn Minh', '0901111145', 'minh.hoang@email.com', 'Việt Nam', '8901', '2026-02-14 14:00:00'::timestamp),
    ('Ngô Thị Nga', '0901111146', 'nga.ngo@email.com', 'Việt Nam', '2345', '2026-02-15 15:00:00'::timestamp),
    ('Bùi Văn Oánh', '0901111147', 'oanh.bui@email.com', 'Việt Nam', '6789', '2026-02-16 16:00:00'::timestamp),
    ('Lý Thị Phụng', '0901111148', 'phung.ly@email.com', 'Việt Nam', '0123', '2026-02-17 08:00:00'::timestamp),
    ('Đinh Văn Quyền', '0901111149', 'quyen.dinh@email.com', 'Việt Nam', '4567', '2026-02-18 09:00:00'::timestamp),
    ('Mai Thị Phương', '0901111150', 'phuong.mai@email.com', 'Việt Nam', '8901', '2026-02-19 10:00:00'::timestamp),
    ('Trương Văn Quang', '0901111151', 'quang.truong@email.com', 'Việt Nam', '2345', '2026-02-20 11:00:00'::timestamp),
    ('Tô Thị Ram', '0901111152', 'ram.to@email.com', 'Việt Nam', '6789', '2026-02-21 12:00:00'::timestamp),
    ('Võ Văn Sơn', '0901111153', 'son.vo@email.com', 'Việt Nam', '0123', '2026-02-22 13:00:00'::timestamp),
    ('Nguyễn Thị Thảo', '0901111154', 'thao.nguyen@email.com', 'Việt Nam', '4567', '2026-02-23 14:00:00'::timestamp),
    ('Lê Văn Tuấn', '0901111155', 'tuan.le@email.com', 'Việt Nam', '8901', '2026-02-24 15:00:00'::timestamp),
    ('Trần Thị Vân', '0901111156', 'van.tran@email.com', 'Việt Nam', '2345', '2026-02-25 16:00:00'::timestamp),
    ('Phạm Văn Vũ', '0901111157', 'vu.pham@email.com', 'Việt Nam', '6789', '2026-02-26 08:00:00'::timestamp),
    ('Đặng Thị Xuân', '0901111158', 'xuan.dang2@email.com', 'Việt Nam', '0123', '2026-02-27 09:00:00'::timestamp)
) AS v(full_name, phone, email, nationality, identity_last4, created_at_utc)
WHERE NOT EXISTS (SELECT 1 FROM guests g WHERE g.phone = v.phone);
/**********************************************************************************************
 * 5. THÊM ĐẠI LÝ (Agencies)
 **********************************************************************************************/
INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'EXPEDIA', 'Expedia Việt Nam', 12, '024-3333-1111'
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'TRIP', 'Trip.com', 14, '024-3333-2222'
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'VIETNAM-TRAVEL', 'Vietnam Travel', 10, '024-3333-3333'
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'BOOKING', 'Booking.com Đà Nẵng', 15, '0236-3333-1111'
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'AGODA', 'Agoda Đà Nẵng', 16, '0236-3333-2222'
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'SUN-TRAVEL', 'Sun Travel', 11, '0236-3333-3333'
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'BOOKING', 'Booking.com HCMC', 15, '028-3333-1111'
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'AGODA', 'Agoda HCMC', 16, '028-3333-2222'
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'BOOKING', 'Booking.com Nha Trang', 15, '0258-3333-1111'
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO agencies (property_id, code, name, commission_percent, contact_phone)
SELECT p.id, 'AGODA', 'Agoda Nha Trang', 16, '0258-3333-2222'
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;
/**********************************************************************************************
 * 6. THÊM DỊCH VỤ / MINIBAR
 **********************************************************************************************/
INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-WATER', 'Nước suối 500ml', 'Chai', 15000, true, 120, true
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-SODA', 'Nước ngọt', 'Lon', 25000, true, 80, true
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-BEER', 'Bia 333', 'Lon', 35000, true, 60, true
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'LAUNDRY', 'Giặt là', 'Lượt', 80000, false, 0, true
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'EXTRA-BED', 'Giường phụ', 'Đêm', 250000, false, 0, true
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'AIRPORT', 'Đưa đón sân bay', 'Lượt', 350000, false, 0, true
FROM properties p WHERE p.code = 'HN-CENTRAL'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-WATER', 'Nước suối', 'Chai', 15000, true, 50, true
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-SODA', 'Nước ngọt', 'Lon', 25000, true, 40, true
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'LAUNDRY', 'Giặt là', 'Lượt', 70000, false, 0, true
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'AIRPORT', 'Xe đưa đón sân bay', 'Lượt', 300000, false, 0, true
FROM properties p WHERE p.code = 'DN-RIVERSIDE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-WATER', 'Nước suối 500ml', 'Chai', 18000, true, 100, true
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-SODA', 'Nước ngọt', 'Lon', 30000, true, 70, true
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-BEER', 'Bia Sài Gòn', 'Lon', 40000, true, 50, true
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'LAUNDRY', 'Giặt là', 'Lượt', 90000, false, 0, true
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'SPA', 'Massage spa', 'Lượt', 500000, false, 0, true
FROM properties p WHERE p.code = 'HCMC-SIGNATURE'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-WATER', 'Nước suối', 'Chai', 15000, true, 60, true
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'MIN-SODA', 'Nước ngọt', 'Lon', 25000, true, 45, true
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'LAUNDRY', 'Giặt là', 'Lượt', 75000, false, 0, true
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'AIRPORT', 'Xe đưa đón sân bay', 'Lượt', 280000, false, 0, true
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;

INSERT INTO service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT p.id, 'SUNBED', 'Mặt bãi biển', 'Ngày', 100000, false, 0, true
FROM properties p WHERE p.code = 'NHA-TRAM'
ON CONFLICT (property_id, code) DO NOTHING;
/**********************************************************************************************
 * 7. TẠO ĐẶT PHÒNG (RESERVATIONS) - MIX các trạng thái
 **********************************************************************************************/
DO $$
DECLARE
    v_today DATE := CURRENT_DATE;
    v_p_hanoi INT;
    v_p_hcmc INT;
    v_p_nhatrang INT;
    v_r_101 INT; v_r_102 INT; v_r_103 INT; v_r_201 INT; v_r_301 INT;
    v_r_101_hcmc INT; v_r_201_nt INT;
    v_g1 INT; v_g2 INT; v_g3 INT; v_g4 INT; v_g5 INT;
    v_g6 INT; v_g7 INT; v_g8 INT; v_g9 INT; v_g10 INT;
DO $$
DECLARE
    v_today DATE := CURRENT_DATE;
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
    v_p_hcmc INT := (SELECT id FROM properties WHERE code = 'HCMC-SIGNATURE');
    v_p_nhatrang INT := (SELECT id FROM properties WHERE code = 'NHA-TRAM');
    v_r_101 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '101');
    v_r_102 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '102');
    v_r_103 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '103');
    v_r_201 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '201');
    v_r_301 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '301');
    v_r_101_hcmc INT := (SELECT id FROM rooms WHERE property_id = v_p_hcmc AND number = '101');
    v_r_201_nt INT := (SELECT id FROM rooms WHERE property_id = v_p_nhatrang AND number = '201');
    v_g1 INT := (SELECT id FROM guests WHERE phone = '0901111111');
    v_g2 INT := (SELECT id FROM guests WHERE phone = '0901111112');
    v_g3 INT := (SELECT id FROM guests WHERE phone = '0901111113');
    v_g4 INT := (SELECT id FROM guests WHERE phone = '0901111114');
    v_g5 INT := (SELECT id FROM guests WHERE phone = '0901111115');
    v_g6 INT := (SELECT id FROM guests WHERE phone = '0901111116');
    v_g7 INT := (SELECT id FROM guests WHERE phone = '0901111117');
    v_g8 INT := (SELECT id FROM guests WHERE phone = '0901111118');
    v_g9 INT := (SELECT id FROM guests WHERE phone = '0901111119');
    v_g10 INT := (SELECT id FROM guests WHERE phone = '0901111120');
    v_a_booking INT := (SELECT id FROM agencies WHERE property_id = v_p_hanoi AND code = 'BOOKING');
    v_a_agoda INT := (SELECT id FROM agencies WHERE property_id = v_p_hanoi AND code = 'AGODA');
BEGIN
    -- BK-TEST-001: CHECKED_IN
    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-001') AND v_r_101 IS NOT NULL AND v_g1 IS NOT NULL THEN
DO $$
DECLARE
    v_today DATE := CURRENT_DATE;
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
    v_r_103 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '103');
    v_r_201 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '201');
    v_r_301 INT := (SELECT id FROM rooms WHERE property_id = v_p_hanoi AND number = '301');
    v_g3 INT := (SELECT id FROM guests WHERE phone = '0901111113');
    v_g4 INT := (SELECT id FROM guests WHERE phone = '0901111114');
    v_g5 INT := (SELECT id FROM guests WHERE phone = '0901111115');
    v_g6 INT := (SELECT id FROM guests WHERE phone = '0901111116');
    v_g7 INT := (SELECT id FROM guests WHERE phone = '0901111117');
    v_g8 INT := (SELECT id FROM guests WHERE phone = '0901111118');
BEGIN
    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-003') AND v_r_103 IS NOT NULL AND v_g3 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-003', v_p_hanoi, v_r_103, v_g3, NULL, v_today, v_today + INTERVAL '3 day', 1, 0, 650000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Đặt phòng trực tiếp', '2026-02-20 10:00:00'::timestamp);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-004') AND v_r_201 IS NOT NULL AND v_g4 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-004', v_p_hanoi, v_r_201, v_g4, NULL, v_today + INTERVAL '1 day', v_today + INTERVAL '3 day', 2, 0, 950000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Lịch hẹn hợp tác', '2026-02-21 11:00:00'::timestamp);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-005') AND v_r_301 IS NOT NULL AND v_g5 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-005', v_p_hanoi, v_r_301, v_g5, NULL, v_today + INTERVAL '5 day', v_today + INTERVAL '8 day', 3, 1, 1650000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Khu gia đình 3 người', '2026-02-22 12:00:00'::timestamp);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-006') AND v_r_103 IS NOT NULL AND v_g6 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-006', v_p_hanoi, v_r_103, v_g6, NULL, v_today - INTERVAL '10 day', v_today - INTERVAL '7 day', 1, 0, 650000, 'CHECKED_OUT', 'DIRECT', 'OVERNIGHT', N'Đã trả phòng', '2026-01-15 08:00:00'::timestamp);
    END IF;

DO $$
DECLARE
    v_today DATE := CURRENT_DATE;
    v_p_hcmc INT := (SELECT id FROM properties WHERE code = 'HCMC-SIGNATURE');
    v_p_nhatrang INT := (SELECT id FROM properties WHERE code = 'NHA-TRAM');
    v_r_101_hcmc INT := (SELECT id FROM rooms WHERE property_id = v_p_hcmc AND number = '101');
    v_r_201_nt INT := (SELECT id FROM rooms WHERE property_id = v_p_nhatrang AND number = '201');
    v_g9 INT := (SELECT id FROM guests WHERE phone = '0901111119');
    v_g10 INT := (SELECT id FROM guests WHERE phone = '0901111120');
BEGIN
    IF v_r_101_hcmc IS NOT NULL AND v_g9 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-009') THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-009', v_p_hcmc, v_r_101_hcmc, v_g9, NULL, v_today + INTERVAL '2 day', v_today + INTERVAL '5 day', 2, 0, 850000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Khởi hành công tác HCMC', '2026-02-23 14:00:00'::timestamp);
    END IF;

    IF v_r_201_nt IS NOT NULL AND v_g10 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-010') THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-010', v_p_nhatrang, v_r_201_nt, v_g10, NULL, v_today + INTERVAL '3 day', v_today + INTERVAL '7 day', 2, 1, 1350000, 'CONFIRMED', 'OTA', 'OVERNIGHT', N'Du lịch nghỉ dưỡng', '2026-02-24 15:00:00'::timestamp);
    END IF;
END $$;
    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-007') AND v_r_201 IS NOT NULL AND v_g7 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-007', v_p_hanoi, v_r_201, v_g7, NULL, v_today - INTERVAL '8 day', v_today - INTERVAL '5 day', 2, 0, 950000, 'CHECKED_OUT', 'OTA', 'OVERNIGHT', N'Đã thanh toán đủ', '2026-01-17 09:00:00'::timestamp);
    END IF;
/**********************************************************************************************
 * 8. TẠO STAYS (check-in) CHO CÁC RESERVATION CHECKED_IN
 **********************************************************************************************/
DO $$
DECLARE
    v_res1 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-001');
    v_res2 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-002');
BEGIN
    IF v_res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM stays WHERE reservation_id = v_res1) THEN
        INSERT INTO stays (reservation_id, room_id, checked_in_at_utc, status)
        VALUES (v_res1, (SELECT room_id FROM reservations WHERE id = v_res1), NOW() - INTERVAL '14 hours', 'IN_HOUSE');
    END IF;

    IF v_res2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM stays WHERE reservation_id = v_res2) THEN
        INSERT INTO stays (reservation_id, room_id, checked_in_at_utc, status)
        VALUES (v_res2, (SELECT room_id FROM reservations WHERE id = v_res2), NOW() - INTERVAL '8 hours', 'IN_HOUSE');
    END IF;
END $$;

/**********************************************************************************************
 * 9. TẠO FOLIO CHARGES (phát sinh phí) - Phòng + Dịch vụ
 **********************************************************************************************/
DO $$
DECLARE
    v_res1 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-001');
    v_res2 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-002');
    v_svc_water INT := (SELECT id FROM service_items WHERE code = 'MIN-WATER' AND property_id = (SELECT id FROM properties WHERE code = 'HN-CENTRAL'));
    v_svc_soda INT := (SELECT id FROM service_items WHERE code = 'MIN-SODA' AND property_id = (SELECT id FROM properties WHERE code = 'HN-CENTRAL'));
BEGIN
    IF v_res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM folio_charges WHERE reservation_id = v_res1 AND category = 'ROOM') THEN
        INSERT INTO folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
        VALUES (v_res1, NULL, 'ROOM', N'Tiền phòng 3 đêm', 3, 650000, 1950000, NOW() - INTERVAL '12 hours');
    END IF;

    IF v_res2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM folio_charges WHERE reservation_id = v_res2 AND category = 'ROOM') THEN
        INSERT INTO folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
        VALUES (v_res2, NULL, 'ROOM', N'Tiền phòng 3 đêm', 3, 950000, 2850000, NOW() - INTERVAL '6 hours');
    END IF;

    IF v_res1 IS NOT NULL AND v_svc_water IS NOT NULL AND NOT EXISTS (SELECT 1 FROM folio_charges WHERE reservation_id = v_res1 AND description LIKE '%Nước suối%') THEN
        INSERT INTO folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
        VALUES (v_res1, v_svc_water, 'SERVICE', N'Nước suối minibar', 2, 15000, 30000, NOW() - INTERVAL '10 hours');
    END IF;

    IF v_res1 IS NOT NULL AND v_svc_soda IS NOT NULL AND NOT EXISTS (SELECT 1 FROM folio_charges WHERE reservation_id = v_res1 AND description LIKE '%Nước ngọt%') THEN
        INSERT INTO folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
        VALUES (v_res1, v_svc_soda, 'SERVICE', N'Nước ngọt minibar', 1, 25000, 25000, NOW() - INTERVAL '8 hours');
    END IF;

    IF v_res2 IS NOT NULL AND v_svc_water IS NOT NULL AND NOT EXISTS (SELECT 1 FROM folio_charges WHERE reservation_id = v_res2 AND description LIKE '%Nước suối%') THEN
/**********************************************************************************************
 * 10. TẠO PAYMENTS (thanh toán) - Có idempotency key
 **********************************************************************************************/
DO $$
DECLARE
    v_res1 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-001');
BEGIN
    IF v_res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM payments WHERE reservation_id = v_res1 AND kind = 'PAYMENT') THEN
        INSERT INTO payments (reservation_id, shift_id, amount, method, kind, reference, idempotency_key, paid_at_utc)
        VALUES (v_res1, NULL, 1000000, 'TRANSFER', 'PAYMENT', 'TEST-PAY-001', '11111111-1111-1111-1111-111111111101', NOW() - INTERVAL '10 hours');
    END IF;

    IF v_res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM payments WHERE reservation_id = v_res1 AND kind = 'PAYMENT' AND reference = 'TEST-PAY-002') THEN
        INSERT INTO payments (reservation_id, shift_id, amount, method, kind, reference, idempotency_key, paid_at_utc)
        VALUES (v_res1, NULL, 950000, 'CASH', 'PAYMENT', 'TEST-PAY-002', '11111111-1111-1111-1111-111111111102', NOW() - INTERVAL '2 hours');
    END IF;
END $$;

/**********************************************************************************************
 * 11. TẠO CASH SHIFTS (ca thu ngân)
 **********************************************************************************************/
DO $$
DECLARE
    v_u_reception INT := (SELECT id FROM app_users WHERE email = 'reception@pms.local');
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
BEGIN
    IF v_u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM cash_shifts WHERE property_id = v_p_hanoi AND user_id = v_u_reception AND status = 'OPEN') THEN
        INSERT INTO cash_shifts (property_id, user_id, status, opening_amount, opened_at_utc, note)
        VALUES (v_p_hanoi, v_u_reception, 'OPEN', 2000000, NOW() - INTERVAL '1 day', N'Ca mở sáng - test data');
    END IF;

    IF v_u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM cash_shifts WHERE property_id = v_p_hanoi AND user_id = v_u_reception AND status = 'CLOSED' AND opening_amount = 1500000) THEN
        INSERT INTO cash_shifts (property_id, user_id, status, opening_amount, closing_amount, opened_at_utc, closed_at_utc, note)
        VALUES (v_p_hanoi, v_u_reception, 'CLOSED', 1500000, 3200000, NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days', N'Ca đã đóng - test data');
    END IF;
END $$;

/**********************************************************************************************
 * 12. TẠO PHIẾU THU / CHI (CASH VOUCHERS)
 **********************************************************************************************/
DO $$
DECLARE
    v_u_reception INT := (SELECT id FROM app_users WHERE email = 'reception@pms.local');
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
BEGIN
    IF v_u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM cash_vouchers WHERE code = 'VCH-TEST-001') THEN
        INSERT INTO cash_vouchers (property_id, code, type, method, category, description, amount, actor_user_id, created_at_utc)
        VALUES (v_p_hanoi, 'VCH-TEST-001', 'RECEIPT', 'CASH', 'OTHER', N'Thu phi phát sinh khác', 500000, v_u_reception, NOW() - INTERVAL '1 day');
    END IF;

    IF v_u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM cash_vouchers WHERE code = 'VCH-TEST-002') THEN
        INSERT INTO cash_vouchers (property_id, code, type, method, category, description, amount, actor_user_id, created_at_utc)
        VALUES (v_p_hanoi, 'VCH-TEST-002', 'EXPENSE', 'TRANSFER', 'OTHER', N'Chi tiền điện nước tháng 2', 2500000, v_u_reception, NOW() - INTERVAL '1 day');
    END IF;
END $$;

/**********************************************************************************************
 * 13. TẠO CÔNG NỢ (DEBT RECEIVABLES)
 **********************************************************************************************/
DO $$
DECLARE
    v_res6 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-006');
    v_res7 INT := (SELECT id FROM reservations WHERE code = 'BK-TEST-007');
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
/**********************************************************************************************
 * 14. TẠO TÀI SẢN KHÁCH (GUEST ASSETS)
 **********************************************************************************************/
DO $$
DECLARE
    v_stay1 INT := (SELECT id FROM stays WHERE reservation_id = (SELECT id FROM reservations WHERE code = 'BK-TEST-001'));
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
BEGIN
    IF v_stay1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM guest_assets WHERE stay_id = v_stay1) THEN
        INSERT INTO guest_assets (property_id, stay_id, description, quantity, note, received_at_utc)
        VALUES (v_p_hanoi, v_stay1, N'Vali đen 20 inch', 1, N'Khách gửi khi đi tham quan thành phố', NOW() - INTERVAL '10 hours');
    END IF;

    IF v_stay1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM guest_assets WHERE stay_id = v_stay1 AND description LIKE '%Mũ%') THEN
        INSERT INTO guest_assets (property_id, stay_id, description, quantity, note, received_at_utc)
        VALUES (v_p_hanoi, v_stay1, N'Mũ bảo tay', 1, NULL, NOW() - INTERVAL '8 hours');
    END IF;
END $$;
BEGIN
    IF v_res6 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM debt_receivables WHERE reservation_id = v_res6) THEN
        INSERT INTO debt_receivables (property_id, reservation_id, original_amount, outstanding_amount, status, note, created_at_utc)
        VALUES (v_p_hanoi, v_res6, 500000, 500000, 'OPEN', N'Còn không đủ tiền mặt lúc checkout', NOW() - INTERVAL '7 days');
    END IF;

    IF v_res7 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM debt_receivables WHERE reservation_id = v_res7) THEN
/**********************************************************************************************
 * 15. TẠO STOCK MOVEMENTS (nhập xuất tồn kho)
 **********************************************************************************************/
DO $$
DECLARE
    v_u_reception INT := (SELECT id FROM app_users WHERE email = 'reception@pms.local');
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
    v_svc_water INT := (SELECT id FROM service_items WHERE code = 'MIN-WATER' AND property_id = v_p_hanoi);
    v_svc_soda INT := (SELECT id FROM service_items WHERE code = 'MIN-SODA' AND property_id = v_p_hanoi);
BEGIN
    IF v_u_reception IS NOT NULL AND v_svc_water IS NOT NULL AND NOT EXISTS (SELECT 1 FROM stock_movements WHERE reference = 'STOCK-INIT-001') THEN
        INSERT INTO stock_movements (property_id, service_item_id, type, quantity_change, balance_after, unit_cost, reference, actor_user_id, created_at_utc)
        VALUES (v_p_hanoi, v_svc_water, 'IN', 120, 120, 8000, 'STOCK-INIT-001', v_u_reception, NOW() - INTERVAL '10 days');
    END IF;

    IF v_u_reception IS NOT NULL AND v_svc_water IS NOT NULL AND NOT EXISTS (SELECT 1 FROM stock_movements WHERE reference = 'STOCK-CONS-001') THEN
        INSERT INTO stock_movements (property_id, service_item_id, type, quantity_change, balance_after, unit_cost, reference, actor_user_id, created_at_utc)
        VALUES (v_p_hanoi, v_svc_water, 'CONSUMPTION', -5, 115, 8000, 'STOCK-CONS-001', v_u_reception, NOW() - INTERVAL '2 days');
    END IF;

    IF v_u_reception IS NOT NULL AND v_svc_soda IS NOT NULL AND NOT EXISTS (SELECT 1 FROM stock_movements WHERE reference = 'STOCK-CONS-002') THEN
        INSERT INTO stock_movements (property_id, service_item_id, type, quantity_change, balance_after, unit_cost, reference, actor_user_id, created_at_utc)
        VALUES (v_p_hanoi, v_svc_soda, 'CONSUMPTION', -3, 77, 12000, 'STOCK-CONS-002', v_u_reception, NOW() - INTERVAL '1 day');
    END IF;
END $$;
        INSERT INTO debt_receivables (property_id, reservation_id, original_amount, outstanding_amount, status, note, created_at_utc)
        VALUES (v_p_hanoi, v_res7, 200000, 100000, 'OPEN', N'Đã thu một lần còn còn lại', NOW() - INTERVAL '5 days');
/**********************************************************************************************
 * 16. TẠO RATE PLANS (bảng giá V2)
 **********************************************************************************************/
DO $$
DECLARE
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
BEGIN
    INSERT INTO rate_plans (property_id, code, name, mode, base_price, included_hours, extra_hour_price, is_active)
    SELECT v_p_hanoi, 'NIGHT-STD', N'Qua đêm tiêu chuẩn', 'OVERNIGHT', 650000, 24, 75000, true
    WHERE NOT EXISTS (SELECT 1 FROM rate_plans WHERE property_id = v_p_hanoi AND code = 'NIGHT-STD');

    INSERT INTO rate_plans (property_id, code, name, mode, base_price, included_hours, extra_hour_price, is_active)
    SELECT v_p_hanoi, 'DAY-STD', N'Nghỉ ngày', 'DAY_USE', 450000, 8, 80000, true
    WHERE NOT EXISTS (SELECT 1 FROM rate_plans WHERE property_id = v_p_hanoi AND code = 'DAY-STD');

    INSERT INTO rate_plans (property_id, code, name, mode, base_price, included_hours, extra_hour_price, is_active)
    SELECT v_p_hanoi, 'HOUR-STD', N'Nghỉ giờ 2h', 'HOURLY', 250000, 2, 75000, true
    WHERE NOT EXISTS (SELECT 1 FROM rate_plans WHERE property_id = v_p_hanoi AND code = 'HOUR-STD');
END $$;

/**********************************************************************************************
 * 17. TẠO BUILDINGS (tòa nhà)
 **********************************************************************************************/
DO $$
DECLARE
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
/**********************************************************************************************
 * 18. TẠO WAITLIST ENTRIES (phòng hàng đợi)
 **********************************************************************************************/
DO $$
DECLARE
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
    v_rt_std INT := (SELECT id FROM room_types WHERE property_id = (SELECT id FROM properties WHERE code = 'HN-CENTRAL') AND code = 'STD');
    v_today DATE := CURRENT_DATE;
BEGIN
    IF v_rt_std IS NOT NULL AND NOT EXISTS (SELECT 1 FROM waitlist_entries WHERE guest_name = N'Nguyễn Test Hàng Đợi') THEN
        INSERT INTO waitlist_entries (property_id, room_type_id, guest_name, phone, check_in_date, check_out_date, status, note, created_at_utc)
        VALUES (v_p_hanoi, v_rt_std, N'Nguyễn Test Hàng Đợi', '0901999999', v_today + INTERVAL '1 day', v_today + INTERVAL '2 day', 'WAITING', N'Muốn phòng tầng cao', NOW() - INTERVAL '1 day');
    END IF;

    IF v_rt_std IS NOT NULL AND NOT EXISTS (SELECT 1 FROM waitlist_entries WHERE guest_name = N'Trần Test Đã Gán Phòng') THEN
        INSERT INTO waitlist_entries (property_id, room_type_id, guest_name, phone, check_in_date, check_out_date, status, note, created_at_utc)
        VALUES (v_p_hanoi, v_rt_std, N'Trần Test Đã Gán Phòng', '0901999998', v_today + INTERVAL '2 day', v_today + INTERVAL '4 day', 'ASSIGNED', N'Đã gán phòng 205', NOW() - INTERVAL '1 day');
    END IF;
END $$;

/**********************************************************************************************
 * 19. TẠO DAILY CLOSE (snapshot chốt ngày)
 **********************************************************************************************/
DO $$
DECLARE
    v_u_manager INT := (SELECT id FROM app_users WHERE email = 'manager@pms.local');
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
    v_today DATE := CURRENT_DATE;
BEGIN
    IF v_u_manager IS NOT NULL AND NOT EXISTS (SELECT 1 FROM daily_closes WHERE property_id = v_p_hanoi AND business_date = v_today - INTERVAL '1 day') THEN
        INSERT INTO daily_closes (property_id, business_date, room_charges, service_charges, received_amount, actor_user_id, created_at_utc)
        VALUES (v_p_hanoi, v_today - INTERVAL '1 day', 4800000, 75000, 4000000, v_u_manager, NOW() - INTERVAL '1 day');
    END IF;
END $$;

/**********************************************************************************************
 * 20. TẠO SUPPLIERS (nhà cung cấp)
 **********************************************************************************************/
DO $$
DECLARE
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
BEGIN
    INSERT INTO suppliers (property_id, code, name, phone, address)
    SELECT v_p_hanoi, 'SUP-001', N'Công ty Thực phẩm Minh Châu', '024-2222-1111', N'Hà Nội'
    WHERE NOT EXISTS (SELECT 1 FROM suppliers WHERE property_id = v_p_hanoi AND code = 'SUP-001');

    INSERT INTO suppliers (property_id, code, name, phone, address)
    SELECT v_p_hanoi, 'SUP-002', N'Công ty Nước giải khát Hà Đông', '024-2222-2222', N'Hà Đông, Hà Nội'
    WHERE NOT EXISTS (SELECT 1 FROM suppliers WHERE property_id = v_p_hanoi AND code = 'SUP-002');
END $$;

/**********************************************************************************************
 * 21. TẠO RETAIL SALES (bán hàng lẻ ngoài phòng)
 **********************************************************************************************/
DO $$
DECLARE
    v_shift_open INT := (SELECT id FROM cash_shifts WHERE property_id = (SELECT id FROM properties WHERE code = 'HN-CENTRAL') AND status = 'OPEN' LIMIT 1);
    v_svc_water INT := (SELECT id FROM service_items WHERE code = 'MIN-WATER' AND property_id = (SELECT id FROM properties WHERE code = 'HN-CENTRAL'));
    v_u_reception INT := (SELECT id FROM app_users WHERE email = 'reception@pms.local');
    v_p_hanoi INT := (SELECT id FROM properties WHERE code = 'HN-CENTRAL');
BEGIN
    IF v_shift_open IS NOT NULL AND v_svc_water IS NOT NULL AND v_u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM retail_sales WHERE service_item_id = v_svc_water AND sold_at_utc >= NOW() - INTERVAL '1 day') THEN
        INSERT INTO retail_sales (property_id, shift_id, service_item_id, quantity, unit_price, total_amount, payment_method, actor_user_id, sold_at_utc)
        VALUES (v_p_hanoi, v_shift_open, v_svc_water, 10, 15000, 150000, 'CASH', v_u_reception, NOW() - INTERVAL '5 hours');
    END IF;
END $$;

SELECT 'SEED TEST DATA COMPLETE - Đã chèn dữ liệu test thành công!' AS status;
BEGIN
    INSERT INTO hotel_buildings (property_id, code, name)
    SELECT v_p_hanoi, 'MAIN', N'Tòa nhà chính'
    WHERE NOT EXISTS (SELECT 1 FROM hotel_buildings WHERE property_id = v_p_hanoi AND code = 'MAIN');

    INSERT INTO hotel_buildings (property_id, code, name)
    SELECT v_p_hanoi, 'ANNEX', N'Tòa nhà phụ'
    WHERE NOT EXISTS (SELECT 1 FROM hotel_buildings WHERE property_id = v_p_hanoi AND code = 'ANNEX');
END $$;
    END IF;
END $$;
        INSERT INTO folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
        VALUES (v_res2, v_svc_water, 'SERVICE', N'Nước suối minibar', 3, 15000, 45000, NOW() - INTERVAL '5 hours');
    END IF;
END $$;

    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-008') AND v_r_201 IS NOT NULL AND v_g8 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-008', v_p_hanoi, v_r_201, v_g8, NULL, v_today + INTERVAL '10 day', v_today + INTERVAL '12 day', 1, 0, 950000, 'CANCELLED', 'DIRECT', 'OVERNIGHT', N'Khách hủy bỏ', '2026-02-25 10:00:00'::timestamp);
    END IF;
END $$;
        INSERT INTO reservations (code, property_id, room_id, guest_id, agency_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-001', v_p_hanoi, v_r_101, v_g1, v_a_booking, v_today - INTERVAL '2 days', v_today + INTERVAL '1 day', 2, 0, 650000, 'CHECKED_IN', 'OTA', 'OVERNIGHT', N'Khách quốc tếCheck-in sớm', NOW() - INTERVAL '10 days');
    END IF;

    -- BK-TEST-002: CHECKED_IN
    IF NOT EXISTS (SELECT 1 FROM reservations WHERE code = 'BK-TEST-002') AND v_r_102 IS NOT NULL AND v_g2 IS NOT NULL THEN
        INSERT INTO reservations (code, property_id, room_id, guest_id, agency_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
        VALUES ('BK-TEST-002', v_p_hanoi, v_r_102, v_g2, v_a_agoda, v_today - INTERVAL '1 day', v_today + INTERVAL '2 day', 2, 1, 950000, 'CHECKED_IN', 'OTA', 'OVERNIGHT', N'Giường phụ yêu cầu', NOW() - INTERVAL '9 days');
    END IF;
END $$;
    v_a_booking INT; v_a_agoda INT;
    v_res1 INT; v_res2 INT;
BEGIN
    SELECT id INTO v_p_hanoi FROM properties WHERE code = 'HN-CENTRAL';
    SELECT id INTO v_p_hcmc FROM properties WHERE code = 'HCMC-SIGNATURE';
    SELECT id INTO v_p_nhatrang FROM properties WHERE code = 'NHA-TRAM';

    SELECT id INTO v_r_101 FROM rooms WHERE property_id = v_p_hanoi AND number = '101';
    SELECT id INTO v_r_102 FROM rooms WHERE property_id = v_p_hanoi AND number = '102';
    SELECT id INTO v_r_103 FROM rooms WHERE property_id = v_p_hanoi AND number = '103';
    SELECT id INTO v_r_201 FROM rooms WHERE property_id = v_p_hanoi AND number = '201';
    SELECT id INTO v_r_301 FROM rooms WHERE property_id = v_p_hanoi AND number = '301';
    SELECT id INTO v_r_101_hcmc FROM rooms WHERE property_id = v_p_hcmc AND number = '101';
    SELECT id INTO v_r_201_nt FROM rooms WHERE property_id = v_p_nhatrang AND number = '201';

    SELECT id INTO v_g1 FROM guests WHERE phone = '0901111111';
    SELECT id INTO v_g2 FROM guests WHERE phone = '0901111112';
    SELECT id INTO v_g3 FROM guests WHERE phone = '0901111113';
    SELECT id INTO v_g4 FROM guests WHERE phone = '0901111114';
    SELECT id INTO v_g5 FROM guests WHERE phone = '0901111115';
    SELECT id INTO v_g6 FROM guests WHERE phone = '0901111116';
    SELECT id INTO v_g7 FROM guests WHERE phone = '0901111117';
    SELECT id INTO v_g8 FROM guests WHERE phone = '0901111118';
    SELECT id INTO v_g9 FROM guests WHERE phone = '0901111119';
    SELECT id INTO v_g10 FROM guests WHERE phone = '0901111120';

    SELECT id INTO v_a_booking FROM agencies WHERE property_id = v_p_hanoi AND code = 'BOOKING';
    SELECT id INTO v_a_agoda FROM agencies WHERE property_id = v_p_hanoi AND code = 'AGODA';
END $$;