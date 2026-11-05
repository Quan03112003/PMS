# Bộ test smoke & kiểm thử nghiệp vụ đầu tiên

**Dữ liệu seed:** admin@pms.local / manager@pms.local / reception@pms.local, mật khẩu Demo@12345. Các ngày demo tạo tương đối theo ngày máy chủ lúc seed.

| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| TC-01 | Lễ tân đăng nhập hợp lệ | 200 + JWT, mở dashboard cơ sở được cấp |
| TC-02 | Sai mật khẩu | 401 |
| TC-03 | Lễ tân gọi API property khác | 403, không lộ dữ liệu |
| TC-04 | Lễ tân tạo room type | 403 |
| TC-05 | Admin xem 2 cơ sở | 200, danh sách 2 property |
| TC-06 | Mở sơ đồ phòng | Trạng thái có khách khác trạng thái vệ sinh |
| TC-07 | Tìm phòng [05/11, 07/11] | Không trả phòng giao với booking tồn tại |
| TC-08 | Ngày trả <= ngày nhận | 400 |
| TC-09 | Tạo booking hợp lệ | 200 + mã BK-*, lưu Guest + Reservation |
| TC-10 | Tạo trùng phòng, trùng ngày | 409, không phát sinh booking thứ hai |
| TC-11 | Đặt phòng bảo trì | 409 |
| TC-12 | Check-in với phòng DIRTY | 409 |
| TC-13 | Check-in CONFIRMED, INSPECTED | Status CHECKED_IN, tạo Stay + room charge |
| TC-14 | Check-in lặp lại | 409 |
| TC-15 | Thêm 2 nước suối | Folio +30.000, stock -2 |
| TC-16 | Minibar stock không đủ | 409, không trừ kho |
| TC-17 | Thanh toán 0 hoặc âm | 400 |
| TC-18 | Thu tiền mặt không mở ca | 409 |
| TC-19 | Thanh toán chia nhiều lần | Tổng Paid cộng dồn và Balance giảm |
| TC-20 | Thanh toán vượt số dư | 409 |
| TC-21 | Checkout khi balance >0 | 409, khách vẫn IN_HOUSE |
| TC-22 | Checkout balance=0 | Stay CHECKED_OUT, Reservation CHECKED_OUT, room DIRTY |
| TC-23 | Checkout lặp | 409 |
| TC-24 | Housekeeping DIRTY -> CLEAN -> INSPECTED | Dọn riêng, không tự thay occupancy |
| TC-25 | Đổi phòng đang ở -> phòng trống đã duyệt | Phòng cũ DIRTY, stay giữ nguyên ID |
| TC-26 | Đổi phòng vào phòng đang ở | 409 |
| TC-27 | Gia hạn trùng booking tương lai | 409 |
| TC-28 | Gia hạn hợp lệ | Ngày trả thay đổi và charge tăng tương ứng |
| TC-29 | Hủy CONFIRMED | Status CANCELLED; tồn phòng được giải phóng |
| TC-30 | Gọi API không bearer token | 401 |

**Chưa thử tích hợp thực tế trong môi trường này:** chưa có SQL Server/.NET SDK/Angular node_modules khả dụng để chạy build end-to-end. Chạy các test trên máy Windows theo README trước khi coi là nghiệm thu.
