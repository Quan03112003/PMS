# Ma trận chức năng theo Word V1.0 + HBSOFT HOTEL

Ký hiệu: ✅ có API và UI; ◐ có một phần; ○ backlog – chưa cài đặt. Không coi phần backlog là đã hoàn thành.

| Chức năng | PMS demo | Ghi chú |
|---|:---:|---|
| Dashboard lễ tân / thống kê phòng | ✅ | Thống kê hiện tại, không có BI |
| Dashboard đa cơ sở | ◐ | Đổi cơ sở bằng dropdown, chưa có biểu đồ hợp nhất chuỗi |
| Sơ đồ phòng theo màu / tầng | ✅ | Phân biệt occupancy / vệ sinh / bảo trì |
| Tìm phòng trống theo ngày | ✅ | Query overlap `[startDate, endDate)` |
| Đặt phòng khách lẻ | ✅ | Một booking / một phòng |
| Đặt phòng đoàn, rooming list | ◐ | Có `groupCode` ở data, chưa có wizard đoàn |
| Đặt phòng hàng đợi (queue) | ○ | Chưa có hàng đợi |
| Check-in có đặt trước | ✅ | Cần INSPECTED |
| Check-in walk-in / thuê theo giờ | ○ | Chưa có tính giá giờ và check-in walk-in một bước |
| Danh sách khách đang ở | ✅ | Có hồ sơ và công nợ |
| Hồ sơ khách | ◐ | Thông tin cơ bản, chưa khai báo tạm trú |
| Đổi phòng | ✅ | Chuyển room và đánh dirty phòng cũ |
| Gia hạn lưu trú | ✅ | Kiểm tra xung đột tồn phòng |
| Chuyển một phần dịch vụ sang phòng khác | ○ | Cần folio multi-payer |
| Minibar / dịch vụ | ✅ | Ghi charge, trừ kho cơ bản |
| Nhập kho dịch vụ | ◐ | Cộng tồn; chưa nhập phiếu chi tiết nhà cung cấp |
| Quản lý nhà cung cấp, kho vật tư | ○ | Mở rộng inventory module |
| Phiếu thanh toán folio | ✅ | Ghi charge, deposit, payment |
| Thanh toán một phần | ✅ | Chặn âm/thu vượt số dư |
| Hoàn tiền, bút toán đảo | ○ | Không chỉnh sửa payment gốc |
| Check-out chuyển DIRTY | ✅ | Phải cân bằng folio |
| Thu/chi ngoài phòng | ○ | Chưa có sổ quỹ đối ứng |
| Mở ca/đóng ca | ✅ | Tiền mặt yêu cầu ca mở; chưa kiểm chênh lệch tự động |
| Cài đặt bảng giá lịch ngày | ◐ | BaseRate và override giá từng booking; chưa rate calendar |
| Nguồn booking/OTA | ◐ | Lưu mã nguồn, chưa đồng bộ kênh |
| Đại lý hoa hồng | ◐ | Danh mục + % hoa hồng, chưa đối soát công nợ |
| Báo cáo doanh thu / thu tiền | ◐ | Theo ngày ghi phát sinh, chưa night audit |
| Night audit, khóa ngày | ○ | Không ghi room charge hàng đêm theo audit |
| Tài khoản, phân quyền | ◐ | JWT + roles + property scope, chưa UI quản lý nhân sự |
| Nhật ký thao tác | ✅ | Các nghiệp vụ chính có audit |
| Hóa đơn điện tử | ○ | Cần nhà cung cấp hóa đơn hợp lệ |
| Tổng đài/PBX, cước điện thoại | ○ | Phụ thuộc phần cứng và hợp đồng PBX |
| Nhà hàng, spa, massage | ○ | Tích hợp sau |
| Quản lý nhiều khách sạn | ✅ | 2 cơ sở seed, lọc dữ liệu theo quyền |

Nguồn tính năng HBSOFT được dùng để lập backlog (không sao chép mã nguồn hay giao diện):
- https://hbsoft.vn/product.html
- https://hbsoft.vn/help.html
