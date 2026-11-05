# Từ điển trường dữ liệu – PMS Demo

Tài liệu Word V1.0 trong `docs/` là gốc nghiệp vụ. Bảng này mô tả chính xác các entity **đã cài đặt** trong API và cách đặt tên FE.

Quy ước: UI tiếng Việt → JSON camelCase → C# PascalCase → SQL Server cột snake_case. `OnModelCreating` đã ánh xạ tên cột trong tất cả entity theo quy tắc snake_case. Tên bảng cũng dùng snake_case. Có thể xem trực tiếp trong DBeaver.

| UI | TypeScript/JSON | C# entity | SQL column | Kiểu | Quy tắc |
|---|---|---|---|---|---|
| Mã khách sạn | code | Property.Code | code | nvarchar(16) | duy nhất toàn hệ thống |
| Tên khách sạn | name | Property.Name | name | nvarchar(200) | không rỗng |
| Mã loại phòng | code | RoomType.Code | code | nvarchar(20) | duy nhất trong property |
| Giá cơ sở | baseRate | RoomType.BaseRate | base_rate | decimal(18,2) | >=0 |
| Mã phòng | number | Room.Number | number | nvarchar(12) | duy nhất trong property |
| Tầng | floor | Room.Floor | floor | int | >=0 |
| Trạng thái vệ sinh | housekeepingStatus | Room.HousekeepingStatus | housekeeping_status | nvarchar(20) | DIRTY/CLEAN/INSPECTED |
| Bảo trì | isOutOfOrder | Room.IsOutOfOrder | is_out_of_order | bit | true thì không bán |
| Mã đơn | code | Reservation.Code | code | nvarchar(30) | duy nhất |
| Ngày nhận | checkInDate | Reservation.CheckInDate | check_in_date | date | ngày nhận < ngày trả |
| Ngày trả | checkOutDate | Reservation.CheckOutDate | check_out_date | date | exclusive |
| Giá/đêm | nightlyRate | Reservation.NightlyRate | nightly_rate | decimal(18,2) | snapshot tại lúc đặt |
| Trạng thái đơn | status | Reservation.Status | status | nvarchar(20) | CONFIRMED/CHECKED_IN/CHECKED_OUT/CANCELLED |
| Tên khách | fullName | Guest.FullName | full_name | nvarchar(150) | không rỗng |
| Phương thức thu | method | Payment.Method | method | nvarchar(20) | CASH/TRANSFER/CARD |
| Số tiền thu | amount | Payment.Amount | amount | decimal(18,2) | >0 |
| Số dư | balance | computed | view | decimal | SUM(charges) - SUM(payments) |
| Giá dịch vụ | price | ServiceItem.Price | price | decimal(18,2) | snapshot thành UnitPrice |
| Tồn kho | stockQuantity | ServiceItem.StockQuantity | stock_quantity | int | không âm |
| Trạng thái ca | status | CashShift.Status | status | nvarchar(20) | OPEN/CLOSED |

**Tách bốn loại trạng thái:** Reservation.Status khác Stay.Status, Room.HousekeepingStatus, Payment.Kind. Không dùng một enum `roomStatus` duy nhất để thay cả bốn.
