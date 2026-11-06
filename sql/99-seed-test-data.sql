/**********************************************************************************************
 * LOTUS PMS V3 — SCRIPT CHÈN DỮ LIỆU TEST LỚN (QA SAFETY)
 * Chạy trên database HotelChainPms đã có dữ liệu seed mẫu (DemoSeeder).
 * Mục đích: cung cấp nhiều dữ liệu để kiểm thử toàn diện các chức năng PMS.
 * Lưu ý: Chạy bằng DBeaver (chọn database HotelChainPms, bật Alt+X để chạy script có GO).
 **********************************************************************************************/

USE [HotelChainPms];
GO
SET XACT_ABORT ON;
GO

/**********************************************************************************************
 * 1. THÊM CƠ SỞ KHÁCH SẢN MỚI (Thuế propriété 3, 4)
 **********************************************************************************************/
IF NOT EXISTS (SELECT 1 FROM dbo.properties WHERE code = 'HCMC-SIGNATURE')
    INSERT INTO dbo.properties (code, name, city, address, phone, check_in_time, check_out_time, is_active)
    VALUES ('HCMC-SIGNATURE', 'Lotus Signature TP.HCM', 'TP. Hồ Chí Minh', 'Quận 1, TP. Hồ Chí Minh', '028-8888-2001', '14:00', '12:00', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.properties WHERE code = 'NHA-TRAM')
    INSERT INTO dbo.properties (code, name, city, address, phone, check_in_time, check_out_time, is_active)
    VALUES ('NHA-TRAM', 'Lotus Beach Nha Trang', 'Nha Trang', 'Vĩnh Nguyên, Nha Trang, Khánh Hòa', '0258-8888-3001', '14:00', '12:00', 1);

GO
/**********************************************************************************************
 * 2. THÊM HẠNG PHÒNG CHO CƠ SỞ MỚI
 **********************************************************************************************/
DECLARE @p_hcmc INT = (SELECT id FROM dbo.properties WHERE code = 'HCMC-SIGNATURE');
DECLARE @p_nhatrang INT = (SELECT id FROM dbo.properties WHERE code = 'NHA-TRAM');

-- HCMC room types
IF NOT EXISTS (SELECT 1 FROM dbo.room_types WHERE property_id = @p_hcmc AND code = 'STD')
    INSERT INTO dbo.room_types (property_id, code, name, capacity, base_rate) VALUES
    (@p_hcmc, 'STD', 'Standard', 2, 850000),
    (@p_hcmc, 'DLX', 'Deluxe', 2, 1250000),
    (@p_hcmc, 'SUITE', 'Executive Suite', 4, 2200000);

-- Nha Trang room types
IF NOT EXISTS (SELECT 1 FROM dbo.room_types WHERE property_id = @p_nhatrang AND code = 'STD')
    INSERT INTO dbo.room_types (property_id, code, name, capacity, base_rate) VALUES
    (@p_nhatrang, 'STD', 'Standard Garden', 2, 750000),
    (@p_nhatrang, 'DLX', 'Ocean View', 3, 1350000),
    (@p_nhatrang, 'VILLA', 'Pool Villa', 6, 3500000);

GO
/**********************************************************************************************
 * 3. THÊM PHÒNG (Khoảng 50 phòng mới cho 2 cơ sở mới)
 **********************************************************************************************/
-- HCMC: 4 tầng, mỗi tầng 8 phòng = 32 phòng
DECLARE @rt_hcmc_std INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_hcmc AND code = 'STD');
DECLARE @rt_hcmc_dlx INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_hcmc AND code = 'DLX');
DECLARE @rt_hcmc_suite INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_hcmc AND code = 'SUITE');

DECLARE @i INT = 1;
WHILE @i <= 4
BEGIN
    DECLARE @j INT = 1;
    WHILE @j <= 8
    BEGIN
        DECLARE @room_no NVARCHAR(12) = CAST(@i AS NVARCHAR) + RIGHT('00' + CAST(@j AS NVARCHAR), 2);
        DECLARE @rt_id INT;
        IF @j <= 3 SET @rt_id = @rt_hcmc_std;
        ELSE IF @j <= 6 SET @rt_id = @rt_hcmc_dlx;
        ELSE SET @rt_id = @rt_hcmc_suite;

        IF NOT EXISTS (SELECT 1 FROM dbo.rooms WHERE property_id = @p_hcmc AND number = @room_no)
            INSERT INTO dbo.rooms (property_id, room_type_id, number, floor, housekeeping_status, is_out_of_order)
            VALUES (@p_hcmc, @rt_id, @room_no, @i, 'INSPECTED', 0);

        SET @j = @j + 1;
    END
    SET @i = @i + 1;
END

-- Nha Trang: 3 tầng, mỗi tầng 6 phòng = 18 phòng
DECLARE @rt_nt_std INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_nhatrang AND code = 'STD');
DECLARE @rt_nt_dlx INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_nhatrang AND code = 'DLX');
DECLARE @rt_nt_villa INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_nhatrang AND code = 'VILLA');

SET @i = 1;
WHILE @i <= 3
BEGIN
    DECLARE @j INT = 1;
    WHILE @j <= 6
    BEGIN
        DECLARE @room_no NVARCHAR(12) = CAST(@i AS NVARCHAR) + RIGHT('00' + CAST(@j AS NVARCHAR), 2);
        DECLARE @rt_id INT;
        IF @j <= 2 SET @rt_id = @rt_nt_std;
        ELSE IF @j <= 4 SET @rt_id = @rt_nt_dlx;
        ELSE SET @rt_id = @rt_nt_villa;

        IF NOT EXISTS (SELECT 1 FROM dbo.rooms WHERE property_id = @p_nhatrang AND number = @room_no)
            INSERT INTO dbo.rooms (property_id, room_type_id, number, floor, housekeeping_status, is_out_of_order)
            VALUES (@p_nhatrang, @rt_id, @room_no, @i, 'INSPECTED', 0);

        SET @j = @j + 1;
    END
    SET @i = @i + 1;
END

GO
/**********************************************************************************************
 * 4. THÊM KHÁCH HÀNG (50 khách hàng test)
 **********************************************************************************************/
INSERT INTO dbo.guests (full_name, phone, email, nationality, identity_last4, created_at_utc)
SELECT * FROM (VALUES
    ('Nguyễn Văn An', '0901111111', 'an.nguyen@email.com', 'Việt Nam', '1234', '2026-01-10 08:00:00'),
    ('Trần Thị Bình', '0901111112', 'binh.tran@email.com', 'Việt Nam', '5678', '2026-01-11 09:00:00'),
    ('Lê Hoàng Cường', '0901111113', 'cuong.le@email.com', 'Việt Nam', '9012', '2026-01-12 10:00:00'),
    ('Phạm Dung', '0901111114', 'dung.pham@email.com', 'Việt Nam', '3456', '2026-01-13 11:00:00'),
    ('Hoàng Vũ Em', '0901111115', 'em.hoang@email.com', 'Việt Nam', '7890', '2026-01-14 12:00:00'),
    ('Vũ Thanh Bình', '0901111116', 'binh.vu@email.com', 'Việt Nam', '2345', '2026-01-15 13:00:00'),
    ('Đỗ Minh Chiến', '0901111117', 'chien.do@email.com', 'Việt Nam', '6789', '2026-01-16 14:00:00'),
    ('Ngô Thị Duyên', '0901111118', 'duyen.ngo@email.com', 'Việt Nam', '0123', '2026-01-17 15:00:00'),
    ('Bùi Hoàng Tuấn', '0901111119', 'tuan.bui@email.com', 'Việt Nam', '4567', '2026-01-18 16:00:00'),
    ('Lý Thị Phương', '0901111120', 'phuong.ly@email.com', 'Việt Nam', '8901', '2026-01-19 17:00:00'),
    ('Đinh Văn Quang', '0901111121', 'quang.dinh@email.com', 'Việt Nam', '2345', '2026-01-20 08:00:00'),
    ('Mai Thị Hà', '0901111122', 'ha.mai@email.com', 'Việt Nam', '6789', '2026-01-21 09:00:00'),
    ('Trương Văn Nam', '0901111123', 'nam.truong@email.com', 'Việt Nam', '0123', '2026-01-22 10:00:00'),
    ('Tô Thị Oanh', '0901111124', 'oanh.to@email.com', 'Việt Nam', '4567', '2026-01-23 11:00:00'),
    ('Võ Văn Phúc', '0901111125', 'phuc.vo@email.com', 'Việt Nam', '8901', '2026-01-24 12:00:00'),
    ('Nguyễn Thị Quyên', '0901111126', 'quyen.nguyen@email.com', 'Việt Nam', '2345', '2026-01-25 13:00:00'),
    ('Lê Văn Sơn', '0901111127', 'son.le@email.com', 'Việt Nam', '6789', '2026-01-26 14:00:00'),
    ('Trần Thị Tâm', '0901111128', 'tam.tran@email.com', 'Việt Nam', '0123', '2026-01-27 15:00:00'),
    ('Phạm Đức Vĩnh', '0901111129', 'vinh.pham@email.com', 'Việt Nam', '4567', '2026-01-28 16:00:00'),
    ('Đặng Thị Xuân', '0901111130', 'xuan.dang@email.com', 'Việt Nam', '8901', '2026-01-29 08:00:00'),
    ('Hoàng Văn Ya', '0901111131', 'ya.hoang@email.com', 'Việt Nam', '2345', '2026-01-30 09:00:00'),
    ('Ngô Thị Zen', '0901111132', 'zen.ngo@email.com', 'Việt Nam', '6789', '2026-02-01 10:00:00'),
    ('Bùi Văn Ái', '0901111133', 'ai.bui@email.com', 'Việt Nam', '0123', '2026-02-02 11:00:00'),
    ('Lý Thị Bảo', '0901111134', 'bao.ly@email.com', 'Việt Nam', '4567', '2026-02-03 12:00:00'),
    ('Đinh Văn Cường', '0901111135', 'cuong.dinh@email.com', 'Việt Nam', '8901', '2026-02-04 13:00:00'),
    ('Mai Thị Dung', '0901111136', 'dung.mai@email.com', 'Việt Nam', '2345', '2026-02-05 14:00:00'),
    ('Trương Văn Em', '0901111137', 'em.truong@email.com', 'Việt Nam', '6789', '2026-02-06 15:00:00'),
    ('Tô Thị Fát', '0901111138', 'fat.to@email.com', 'Việt Nam', '0123', '2026-02-07 16:00:00'),
    ('Võ Văn Giang', '0901111139', 'giang.vo@email.com', 'Việt Nam', '4567', '2026-02-08 08:00:00'),
    ('Nguyễn Thị Hương', '0901111140', 'huong.nguyen@email.com', 'Việt Nam', '8901', '2026-02-09 09:00:00'),
    ('Lê Văn Hải', '0901111141', 'hai.le@email.com', 'Việt Nam', '2345', '2026-02-10 10:00:00'),
    ('Trần Thị Hoàng', '0901111142', 'hoang.tran@email.com', 'Việt Nam', '6789', '2026-02-11 11:00:00'),
    ('Phạm Văn Hưng', '0901111143', 'hung.pham@email.com', 'Việt Nam', '0123', '2026-02-12 12:00:00'),
    ('Đặng Thị Lan', '0901111144', 'lan.dang@email.com', 'Việt Nam', '4567', '2026-02-13 13:00:00'),
    ('Hoàng Văn Minh', '0901111145', 'minh.hoang@email.com', 'Việt Nam', '8901', '2026-02-14 14:00:00'),
    ('Ngô Thị Nga', '0901111146', 'nga.ngo@email.com', 'Việt Nam', '2345', '2026-02-15 15:00:00'),
    ('Bùi Văn Oánh', '0901111147', 'oanh.bui@email.com', 'Việt Nam', '6789', '2026-02-16 16:00:00'),
    ('Lý Thị Phụng', '0901111148', 'phung.ly@email.com', 'Việt Nam', '0123', '2026-02-17 08:00:00'),
    ('Đinh Văn Quyền', '0901111149', 'quyen.dinh@email.com', 'Việt Nam', '4567', '2026-02-18 09:00:00'),
    ('Mai Thị Phương', '0901111150', 'phuong.mai@email.com', 'Việt Nam', '8901', '2026-02-19 10:00:00'),
    ('Trương Văn Quang', '0901111151', 'quang.truong@email.com', 'Việt Nam', '2345', '2026-02-20 11:00:00'),
    ('Tô Thị Ram', '0901111152', 'ram.to@email.com', 'Việt Nam', '6789', '2026-02-21 12:00:00'),
    ('Võ Văn Sơn', '0901111153', 'son.vo@email.com', 'Việt Nam', '0123', '2026-02-22 13:00:00'),
    ('Nguyễn Thị Thảo', '0901111154', 'thao.nguyen@email.com', 'Việt Nam', '4567', '2026-02-23 14:00:00'),
    ('Lê Văn Tuấn', '0901111155', 'tuan.le@email.com', 'Việt Nam', '8901', '2026-02-24 15:00:00'),
    ('Trần Thị Vân', '0901111156', 'van.tran@email.com', 'Việt Nam', '2345', '2026-02-25 16:00:00'),
    ('Phạm Văn Vũ', '0901111157', 'vu.pham@email.com', 'Việt Nam', '6789', '2026-02-26 08:00:00'),
    ('Đặng Thị Xuân', '0901111158', 'xuan.dang2@email.com', 'Việt Nam', '0123', '2026-02-27 09:00:00')
) AS v(full_name, phone, email, nationality, identity_last4, created_at_utc)
WHERE NOT EXISTS (SELECT 1 FROM dbo.guests g WHERE g.phone = v.phone);

GO
/**********************************************************************************************
 * 5. THÊM ĐẠI LÝ (Agencies) CHO TỪNG CƠ SỞ
 **********************************************************************************************/
DECLARE @p_hanoi INT = (SELECT id FROM dbo.properties WHERE code = 'HN-CENTRAL');
DECLARE @p_danang INT = (SELECT id FROM dbo.properties WHERE code = 'DN-RIVERSIDE');

INSERT INTO dbo.agencies (property_id, code, name, commission_percent, contact_phone)
SELECT * FROM (VALUES
    (@p_hanoi, 'EXPEDIA', 'Expedia Việt Nam', 12, '024-3333-1111'),
    (@p_hanoi, 'TRIP', 'Trip.com', 14, '024-3333-2222'),
    (@p_hanoi, 'VIETNAM-TRAVEL', 'Vietnam Travel', 10, '024-3333-3333'),
    (@p_danang, 'BOOKING', 'Booking.com Đà Nẵng', 15, '0236-3333-1111'),
    (@p_danang, 'AGODA', 'Agoda Đà Nẵng', 16, '0236-3333-2222'),
    (@p_danang, 'SUN-TRAVEL', 'Sun Travel', 11, '0236-3333-3333'),
    (@p_hcmc, 'BOOKING', 'Booking.com HCMC', 15, '028-3333-1111'),
    (@p_hcmc, 'AGODA', 'Agoda HCMC', 16, '028-3333-2222'),
    (@p_nhatrang, 'BOOKING', 'Booking.com Nha Trang', 15, '0258-3333-1111'),
    (@p_nhatrang, 'AGODA', 'Agoda Nha Trang', 16, '0258-3333-2222')
) AS v(property_id, code, name, commission_percent, contact_phone)
WHERE NOT EXISTS (SELECT 1 FROM dbo.agencies a WHERE a.property_id = v.property_id AND a.code = v.code);

GO

/**********************************************************************************************
 * 6. THÊM DỊCH VỤ / MINIBAR CHO TỪNG CƠ SỞ
 **********************************************************************************************/
INSERT INTO dbo.service_items (property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
SELECT * FROM (VALUES
    (@p_hanoi, 'MIN-WATER', 'Nước suối 500ml', 'Chai', 15000, 1, 120, 1),
    (@p_hanoi, 'MIN-SODA', 'Nước ngọt', 'Lon', 25000, 1, 80, 1),
    (@p_hanoi, 'MIN-BEER', 'Bia 333', 'Lon', 35000, 1, 60, 1),
    (@p_hanoi, 'LAUNDRY', 'Giặt là', 'Lượt', 80000, 0, 0, 1),
    (@p_hanoi, 'EXTRA-BED', 'Giường phụ', 'Đêm', 250000, 0, 0, 1),
    (@p_hanoi, 'AIRPORT', 'Đưa đón sân bay', 'Lượt', 350000, 0, 0, 1),
    (@p_danang, 'MIN-WATER', 'Nước suối', 'Chai', 15000, 1, 50, 1),
    (@p_danang, 'MIN-SODA', 'Nước ngọt', 'Lon', 25000, 1, 40, 1),
    (@p_danang, 'LAUNDRY', 'Giặt là', 'Lượt', 70000, 0, 0, 1),
    (@p_danang, 'AIRPORT', 'Xe đưa đón sân bay', 'Lượt', 300000, 0, 0, 1),
    (@p_hcmc, 'MIN-WATER', 'Nước suối 500ml', 'Chai', 18000, 1, 100, 1),
    (@p_hcmc, 'MIN-SODA', 'Nước ngọt', 'Lon', 30000, 1, 70, 1),
    (@p_hcmc, 'MIN-BEER', 'Bia Sài Gòn', 'Lon', 40000, 1, 50, 1),
    (@p_hcmc, 'LAUNDRY', 'Giặt là', 'Lượt', 90000, 0, 0, 1),
    (@p_hcmc, 'SPA', 'Massage spa', 'Lượt', 500000, 0, 0, 1),
    (@p_nhatrang, 'MIN-WATER', 'Nước suối', 'Chai', 15000, 1, 60, 1),
    (@p_nhatrang, 'MIN-SODA', 'Nước ngọt', 'Lon', 25000, 1, 45, 1),
    (@p_nhatrang, 'LAUNDRY', 'Giặt là', 'Lượt', 75000, 0, 0, 1),
    (@p_nhatrang, 'AIRPORT', 'Xe đưa đón sân bay', 'Lượt', 280000, 0, 0, 1),
    (@p_nhatrang, 'SUNBED', 'Mặt bãi biển', 'Ngày', 100000, 0, 0, 1)
) AS v(property_id, code, name, unit, price, track_inventory, stock_quantity, is_active)
WHERE NOT EXISTS (SELECT 1 FROM dbo.service_items s WHERE s.property_id = v.property_id AND s.code = v.code);

GO
/**********************************************************************************************
 * 7. TẠO NHIỀU ĐẶT PHÒNG (RESERVATIONS) - MIX các trạng thái
 *    Dùng ngày hôm nay làm mốc (GETDATE())
 **********************************************************************************************/
DECLARE @today DATE = CAST(GETDATE() AS DATE);
DECLARE @r_101 INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_hanoi AND number = '101');
DECLARE @r_102 INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_hanoi AND number = '102');
DECLARE @r_103 INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_hanoi AND number = '103');
DECLARE @r_201 INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_hanoi AND number = '201');
DECLARE @r_301 INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_hanoi AND number = '301');
DECLARE @r_101_hcmc INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_hcmc AND number = '101');
DECLARE @r_201_nt INT = (SELECT id FROM dbo.rooms WHERE property_id = @p_nhatrang AND number = '201');
DECLARE @g1 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111111');
DECLARE @g2 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111112');
DECLARE @g3 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111113');
DECLARE @g4 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111114');
DECLARE @g5 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111115');
DECLARE @g6 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111116');
DECLARE @g7 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111117');
-- Đặt phòng đã CHECK_IN (khách đang ở) - HN-CENTRAL
IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-001')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, agency_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-001', @p_hanoi, @r_101, @g1, @a_booking, DATEADD(day, -2, @today), DATEADD(day, 1, @today), 2, 0, 650000, 'CHECKED_IN', 'OTA', 'OVERNIGHT', N'Khách quốc tếCheck-in sớm', '2026-01-10 08:00:00');

IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-002')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, agency_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-002', @p_hanoi, @r_102, @g2, @a_agoda, DATEADD(day, -1, @today), DATEADD(day, 2, @today), 2, 1, 950000, 'CHECKED_IN', 'OTA', 'OVERNIGHT', N'Giường phụ yêu cầu', '2026-01-11 09:00:00');

-- Đặt phòng CONFIRMED (sắp đến) - HN-CENTRAL
IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-003')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-003', @p_hanoi, @r_103, @g3, NULL, @today, DATEADD(day, 3, @today), 1, 0, 650000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Đặt phòng trực tiếp', '2026-02-20 10:00:00');

IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-004')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-004', @p_hanoi, @r_201, @g4, NULL, DATEADD(day, 1, @today), DATEADD(day, 3, @today), 2, 0, 950000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Lịch hẹn hợp tác', '2026-02-21 11:00:00');

IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-005')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-005', @p_hanoi, @r_301, @g5, NULL, DATEADD(day, 5, @today), DATEADD(day, 8, @today), 3, 1, 1650000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Khu gia đình 3 người', '2026-02-22 12:00:00');

GO
DECLARE @g8 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111118');
DECLARE @g9 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111119');
DECLARE @g10 INT = (SELECT id FROM dbo.guests WHERE phone = '0901111120');
DECLARE @a_booking INT = (SELECT id FROM dbo.agencies WHERE property_id = @p_hanoi AND code = 'BOOKING');
DECLARE @a_agoda INT = (SELECT id FROM dbo.agencies WHERE property_id = @p_hanoi AND code = 'AGODA');
GO
-- Đặt phòng đã CHECK_OUT - HN-CENTRAL
IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-006')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-006', @p_hanoi, @r_101, @g6, DATEADD(day, -10, @today), DATEADD(day, -7, @today), 1, 0, 650000, 'CHECKED_OUT', 'DIRECT', 'OVERNIGHT', N'Đã trả phòng', '2026-01-15 08:00:00');

IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-007')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-007', @p_hanoi, @r_102, @g7, DATEADD(day, -8, @today), DATEADD(day, -5, @today), 2, 0, 950000, 'CHECKED_OUT', 'OTA', 'OVERNIGHT', N'Đã thanh toán đủ', '2026-01-17 09:00:00');

-- Đặt phòng CANCELLED - HN-CENTRAL
IF NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-008')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-008', @p_hanoi, @r_201, @g8, DATEADD(day, 10, @today), DATEADD(day, 12, @today), 1, 0, 950000, 'CANCELLED', 'DIRECT', 'OVERNIGHT', N'Khách hủy bỏ', '2026-02-25 10:00:00');

-- Đặt phòng cho HCMC
IF @r_101_hcmc IS NOT NULL AND @g9 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-009')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-009', @p_hcmc, @r_101_hcmc, @g9, NULL, DATEADD(day, 2, @today), DATEADD(day, 5, @today), 2, 0, 850000, 'CONFIRMED', 'DIRECT', 'OVERNIGHT', N'Khởi hành công tác HCMC', '2026-02-23 14:00:00');

-- Đặt phòng cho Nha Trang
IF @r_201_nt IS NOT NULL AND @g10 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.reservations WHERE code = 'BK-TEST-010')
    INSERT INTO dbo.reservations (code, property_id, room_id, guest_id, check_in_date, check_out_date, adults, children, nightly_rate, status, booking_source, booking_mode, note, created_at_utc)
    VALUES ('BK-TEST-010', @p_nhatrang, @r_201_nt, @g10, NULL, DATEADD(day, 3, @today), DATEADD(day, 7, @today), 2, 1, 1350000, 'CONFIRMED', 'OTA', 'OVERNIGHT', N'Du lịch nghỉ dưỡng', '2026-02-24 15:00:00');

GO
/**********************************************************************************************
 * 8. TẠO STAYS (check-in) CHO CÁC RESERVATION CHECKED_IN
 **********************************************************************************************/
DECLARE @res1 INT = (SELECT id FROM dbo.reservations WHERE code = 'BK-TEST-001');
DECLARE @res2 INT = (SELECT id FROM dbo.reservations WHERE code = 'BK-TEST-002');

IF @res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.stays WHERE reservation_id = @res1)
    INSERT INTO dbo.stays (reservation_id, room_id, checked_in_at_utc, status)
    VALUES (@res1, (SELECT room_id FROM dbo.reservations WHERE id = @res1), DATEADD(hour, -14, GETDATE()), 'IN_HOUSE');

IF @res2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.stays WHERE reservation_id = @res2)
    INSERT INTO dbo.stays (reservation_id, room_id, checked_in_at_utc, status)
    VALUES (@res2, (SELECT room_id FROM dbo.reservations WHERE id = @res2), DATEADD(hour, -8, GETDATE()), 'IN_HOUSE');

GO

/**********************************************************************************************
 * 9. TẠO FOLIO CHARGES (phát sinh phí) - Phòng + Dịch vụ
 **********************************************************************************************/
-- Tiền phòng cho các stay đang ở
IF @res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.folio_charges WHERE reservation_id = @res1 AND category = 'ROOM')
    INSERT INTO dbo.folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
    VALUES (@res1, NULL, 'ROOM', N'Tiền phòng 3 đêm', 3, 650000, 1950000, DATEADD(hour, -12, GETDATE()));

IF @res2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.folio_charges WHERE reservation_id = @res2 AND category = 'ROOM')
    INSERT INTO dbo.folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
    VALUES (@res2, NULL, 'ROOM', N'Tiền phòng 3 đêm', 3, 950000, 2850000, DATEADD(hour, -6, GETDATE()));

-- Dịch vụ minibar cho khách đang ở
DECLARE @svc_water_hanoi INT = (SELECT id FROM dbo.service_items WHERE property_id = @p_hanoi AND code = 'MIN-WATER');
DECLARE @svc_soda_hanoi INT = (SELECT id FROM dbo.service_items WHERE property_id = @p_hanoi AND code = 'MIN-SODA');

IF @res1 IS NOT NULL AND @svc_water_hanoi IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.folio_charges WHERE reservation_id = @res1 AND description LIKE N'%Nước suối%')
    INSERT INTO dbo.folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
    VALUES (@res1, @svc_water_hanoi, 'SERVICE', N'Nước suối minibar', 2, 15000, 30000, DATEADD(hour, -10, GETDATE()));

IF @res1 IS NOT NULL AND @svc_soda_hanoi IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.folio_charges WHERE reservation_id = @res1 AND description LIKE N'%Nước ngọt%')
    INSERT INTO dbo.folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
    VALUES (@res1, @svc_soda_hanoi, 'SERVICE', N'Nước ngọt minibar', 1, 25000, 25000, DATEADD(hour, -8, GETDATE()));

IF @res2 IS NOT NULL AND @svc_water_hanoi IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.folio_charges WHERE reservation_id = @res2 AND description LIKE N'%Nước suối%')
    INSERT INTO dbo.folio_charges (reservation_id, service_item_id, category, description, quantity, unit_price, amount, posted_at_utc)
    VALUES (@res2, @svc_water_hanoi, 'SERVICE', N'Nước suối minibar', 3, 15000, 45000, DATEADD(hour, -5, GETDATE()));

GO
/**********************************************************************************************
 * 10. TẠO PAYMENTS (thanh toán) - Có idempotency key
 **********************************************************************************************/
-- Thanh toán cho BK-TEST-001 (cách tạo UUID tĩnh để test idempotency)
IF @res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.payments WHERE reservation_id = @res1 AND kind = 'PAYMENT')
    INSERT INTO dbo.payments (reservation_id, shift_id, amount, method, kind, reference, idempotency_key, paid_at_utc)
    VALUES (@res1, NULL, 1000000, 'TRANSFER', 'PAYMENT', 'TEST-PAY-001', '11111111-1111-1111-1111-111111111101', DATEADD(hour, -10, GETDATE()));

-- Thanh toán phần còn lại cho BK-TEST-001
IF @res1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.payments WHERE reservation_id = @res1 AND kind = 'PAYMENT' AND reference = 'TEST-PAY-002')
    INSERT INTO dbo.payments (reservation_id, shift_id, amount, method, kind, reference, idempotency_key, paid_at_utc)
    VALUES (@res1, NULL, 950000, 'CASH', 'PAYMENT', 'TEST-PAY-002', '11111111-1111-1111-1111-111111111102', DATEADD(hour, -2, GETDATE()));

GO

/**********************************************************************************************
 * 11. TẠO CASH SHIFTS (ca thu ngân)
 **********************************************************************************************/
-- Lấy ID user reception (seed từ DemoSeeder)
DECLARE @u_reception INT = (SELECT id FROM dbo.app_users WHERE email = 'reception@pms.local');

IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.cash_shifts WHERE property_id = @p_hanoi AND user_id = @u_reception AND status = 'OPEN')
    INSERT INTO dbo.cash_shifts (property_id, user_id, status, opening_amount, opened_at_utc, note)
    VALUES (@p_hanoi, @u_reception, 'OPEN', 2000000, DATEADD(day, -1, GETDATE()), N'Ca mở sáng - test data');

-- Ca đã đóng
IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.cash_shifts WHERE property_id = @p_hanoi AND user_id = @u_reception AND status = 'CLOSED' AND opening_amount = 1500000)
    INSERT INTO dbo.cash_shifts (property_id, user_id, status, opening_amount, closing_amount, opened_at_utc, closed_at_utc, note)
    VALUES (@p_hanoi, @u_reception, 'CLOSED', 1500000, 3200000, DATEADD(day, -3, GETDATE()), DATEADD(day, -2, GETDATE()), N'Ca đã đóng - test data');

GO

/**********************************************************************************************
 * 12. TẠO PHIẾU THU / CHI (CASH VOUCHERS)
 **********************************************************************************************/
IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.cash_vouchers WHERE code = 'VCH-TEST-001')
    INSERT INTO dbo.cash_vouchers (property_id, code, type, method, category, description, amount, actor_user_id, created_at_utc)
    VALUES (@p_hanoi, 'VCH-TEST-001', 'RECEIPT', 'CASH', 'OTHER', N'Thu phi phát sinh khác', 500000, @u_reception, DATEADD(day, -1, GETDATE()));

IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.cash_vouchers WHERE code = 'VCH-TEST-002')
    INSERT INTO dbo.cash_vouchers (property_id, code, type, method, category, description, amount, actor_user_id, created_at_utc)
    VALUES (@p_hanoi, 'VCH-TEST-002', 'EXPENSE', 'TRANSFER', 'OTHER', N'Chi tiền điện nước tháng 2', 2500000, @u_reception, DATEADD(day, -1, GETDATE()));

GO
/**********************************************************************************************
 * 13. TẠO CÔNG NỢ (DEBT RECEIVABLES) - Checkout ghi nợ
 **********************************************************************************************/
DECLARE @res6 INT = (SELECT id FROM dbo.reservations WHERE code = 'BK-TEST-006');
DECLARE @res7 INT = (SELECT id FROM dbo.reservations WHERE code = 'BK-TEST-007');

IF @res6 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.debt_receivables WHERE reservation_id = @res6)
    INSERT INTO dbo.debt_receivables (property_id, reservation_id, original_amount, outstanding_amount, status, note, created_at_utc)
    VALUES (@p_hanoi, @res6, 500000, 500000, 'OPEN', N'Còn không đủ tiền mặt lúc checkout', DATEADD(day, -7, GETDATE()));

IF @res7 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.debt_receivables WHERE reservation_id = @res7)
    INSERT INTO dbo.debt_receivables (property_id, reservation_id, original_amount, outstanding_amount, status, note, created_at_utc)
    VALUES (@p_hanoi, @res7, 200000, 100000, 'OPEN', N'Đã thu một lần còn còn lại', DATEADD(day, -5, GETDATE()));

GO

/**********************************************************************************************
 * 14. TẠO TÀI SẢN KHÁCH (GUEST ASSETS)
 **********************************************************************************************/
DECLARE @stay1 INT = (SELECT id FROM dbo.stays WHERE reservation_id = @res1);

IF @stay1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.guest_assets WHERE stay_id = @stay1)
    INSERT INTO dbo.guest_assets (property_id, stay_id, description, quantity, note, received_at_utc)
    VALUES (@p_hanoi, @stay1, N'Vali đen 20 inch', 1, N'Khách gửi khi đi tham quan thành phố', DATEADD(hour, -10, GETDATE()));

IF @stay1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.guest_assets WHERE stay_id = @stay1 AND description LIKE N'%Mũ%')
    INSERT INTO dbo.guest_assets (property_id, stay_id, description, quantity, note, received_at_utc)
    VALUES (@p_hanoi, @stay1, N'Mũ bảo tay', 1, NULL, DATEADD(hour, -8, GETDATE()));

GO

/**********************************************************************************************
 * 15. TẠO STOCK MOVEMENTS (nhập xuất tồn kho)
 **********************************************************************************************/
-- Nhập kho ban đầu cho nước suối
IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.stock_movements WHERE reference = 'STOCK-INIT-001')
    INSERT INTO dbo.stock_movements (property_id, service_item_id, type, quantity_change, balance_after, unit_cost, reference, actor_user_id, created_at_utc)
    VALUES (@p_hanoi, @svc_water_hanoi, 'IN', 120, 120, 8000, 'STOCK-INIT-001', @u_reception, DATEADD(day, -10, GETDATE()));

-- Xuất kho (consumption) cho minibar
IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.stock_movements WHERE reference = 'STOCK-CONS-001')
    INSERT INTO dbo.stock_movements (property_id, service_item_id, type, quantity_change, balance_after, unit_cost, reference, actor_user_id, created_at_utc)
    VALUES (@p_hanoi, @svc_water_hanoi, 'CONSUMPTION', -5, 115, 8000, 'STOCK-CONS-001', @u_reception, DATEADD(day, -2, GETDATE()));

IF @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.stock_movements WHERE reference = 'STOCK-CONS-002')
    INSERT INTO dbo.stock_movements (property_id, service_item_id, type, quantity_change, balance_after, unit_cost, reference, actor_user_id, created_at_utc)
    VALUES (@p_hanoi, @svc_soda_hanoi, 'CONSUMPTION', -3, 77, 12000, 'STOCK-CONS-002', @u_reception, DATEADD(day, -1, GETDATE()));

GO
/**********************************************************************************************
 * 16. TẠO RATE PLANS (bảng giá V2)
 **********************************************************************************************/
IF NOT EXISTS (SELECT 1 FROM dbo.rate_plans WHERE property_id = @p_hanoi AND code = 'NIGHT-STD')
    INSERT INTO dbo.rate_plans (property_id, code, name, mode, base_price, included_hours, extra_hour_price, is_active)
    VALUES (@p_hanoi, 'NIGHT-STD', N'Qua đêm tiêu chuẩn', 'OVERNIGHT', 650000, 24, 75000, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.rate_plans WHERE property_id = @p_hanoi AND code = 'DAY-STD')
    INSERT INTO dbo.rate_plans (property_id, code, name, mode, base_price, included_hours, extra_hour_price, is_active)
    VALUES (@p_hanoi, 'DAY-STD', N'Nghỉ ngày', 'DAY_USE', 450000, 8, 80000, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.rate_plans WHERE property_id = @p_hanoi AND code = 'HOUR-STD')
    INSERT INTO dbo.rate_plans (property_id, code, name, mode, base_price, included_hours, extra_hour_price, is_active)
    VALUES (@p_hanoi, 'HOUR-STD', N'Nghỉ giờ 2h', 'HOURLY', 250000, 2, 75000, 1);

GO

/**********************************************************************************************
 * 17. TẠO BUILDINGS (tòa nhà)
 **********************************************************************************************/
IF NOT EXISTS (SELECT 1 FROM dbo.hotel_buildings WHERE property_id = @p_hanoi AND code = 'MAIN')
    INSERT INTO dbo.hotel_buildings (property_id, code, name) VALUES (@p_hanoi, 'MAIN', N'Tòa nhà chính');

IF NOT EXISTS (SELECT 1 FROM dbo.hotel_buildings WHERE property_id = @p_hanoi AND code = 'ANNEX')
    INSERT INTO dbo.hotel_buildings (property_id, code, name) VALUES (@p_hanoi, 'ANNEX', N'Tòa nhà phụ');

GO

/**********************************************************************************************
 * 18. TẠO WAITLIST ENTRIES (phòng hàng đợi)
 **********************************************************************************************/
DECLARE @rt_std_hanoi INT = (SELECT id FROM dbo.room_types WHERE property_id = @p_hanoi AND code = 'STD');

IF NOT EXISTS (SELECT 1 FROM dbo.waitlist_entries WHERE guest_name = N'Nguyễn Test Hàng Đợi')
    INSERT INTO dbo.waitlist_entries (property_id, room_type_id, guest_name, phone, check_in_date, check_out_date, status, note, created_at_utc)
    VALUES (@p_hanoi, @rt_std_hanoi, N'Nguyễn Test Hàng Đợi', '0901999999', DATEADD(day, 1, @today), DATEADD(day, 2, @today), 'WAITING', N'Muốn phòng tầng cao', DATEADD(day, -1, GETDATE()));

IF NOT EXISTS (SELECT 1 FROM dbo.waitlist_entries WHERE guest_name = N'Trần Test Đã Gán Phòng')
    INSERT INTO dbo.waitlist_entries (property_id, room_type_id, guest_name, phone, check_in_date, check_out_date, status, note, created_at_utc)
    VALUES (@p_hanoi, @rt_std_hanoi, N'Trần Test Đã Gán Phòng', '0901999998', DATEADD(day, 2, @today), DATEADD(day, 4, @today), 'ASSIGNED', N'Đã gán phòng 205', DATEADD(day, -1, GETDATE()));

GO

/**********************************************************************************************
 * 19. TẠO DAILY CLOSE (snapshot chốt ngày)
 **********************************************************************************************/
DECLARE @u_manager INT = (SELECT id FROM dbo.app_users WHERE email = 'manager@pms.local');

IF @u_manager IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.daily_closes WHERE property_id = @p_hanoi AND business_date = DATEADD(day, -1, @today))
    INSERT INTO dbo.daily_closes (property_id, business_date, room_charges, service_charges, received_amount, actor_user_id, created_at_utc)
    VALUES (@p_hanoi, DATEADD(day, -1, @today), 4800000, 75000, 4000000, @u_manager, DATEADD(day, -1, GETDATE()));

GO
/**********************************************************************************************
 * 20. TẠO SUPPLIERS (nhà cung cấp)
 **********************************************************************************************/
IF NOT EXISTS (SELECT 1 FROM dbo.suppliers WHERE property_id = @p_hanoi AND code = 'SUP-001')
    INSERT INTO dbo.suppliers (property_id, code, name, phone, address)
    VALUES (@p_hanoi, 'SUP-001', N'Công ty Thực phẩm Minh Châu', '024-2222-1111', N'Hà Nội');

IF NOT EXISTS (SELECT 1 FROM dbo.suppliers WHERE property_id = @p_hanoi AND code = 'SUP-002')
    INSERT INTO dbo.suppliers (property_id, code, name, phone, address)
    VALUES (@p_hanoi, 'SUP-002', N'Công ty Nước giải khát Hà Đông', '024-2222-2222', N'Hà Đông, Hà Nội');

GO

/**********************************************************************************************
 * 21. TẠO RETAIL SALES (bán hàng lẻ ngoài phòng)
 **********************************************************************************************/
DECLARE @shift_open INT = (SELECT TOP 1 id FROM dbo.cash_shifts WHERE property_id = @p_hanoi AND status = 'OPEN');

IF @shift_open IS NOT NULL AND @svc_water_hanoi IS NOT NULL AND @u_reception IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.retail_sales WHERE service_item_id = @svc_water_hanoi AND sold_at_utc >= DATEADD(day, -1, GETDATE()))
    INSERT INTO dbo.retail_sales (property_id, shift_id, service_item_id, quantity, unit_price, total_amount, payment_method, actor_user_id, sold_at_utc)
    VALUES (@p_hanoi, @shift_open, @svc_water_hanoi, 10, 15000, 150000, 'CASH', @u_reception, DATEADD(hour, -5, GETDATE()));

GO

PRINT 'SEED TEST DATA COMPLETE - Đã chèn dữ liệu test thành công!';
GO