# PMS V2 — Dictionary tên trường từ giao diện / JSON / C# / SQL Server

**Quy ước:** label tiếng Việt / JSON `camelCase` / C# `PascalCase` / SQL `snake_case`. Khóa `Id` là identity int, trừ `StockMovement.Id` là bigint. Tiền dùng decimal(18,2), không dùng floating-point. Ngày phòng dùng `DateOnly`→ SQL `date`, giao dịch UTC dùng `datetime2`. Chuỗi dài dùng `nvarchar`. Bảng mới có `PropertyId` để phân vùng quyền tại API.

| Phân hệ | Tên trường trên UI | JSON | C# entity/DTO | SQL column | Kiểu | Ràng buộc nghiệp vụ |
|---|---|---|---|---|---|---|
| Tòa nhà | Mã tòa | `code` | `HotelBuilding.Code` | `hotel_buildings.code` | NVARCHAR(30) | bắt buộc, duy nhất trong cơ sở |
| Tòa nhà | Tên tòa | `name` | `HotelBuilding.Name` | `hotel_buildings.name` | NVARCHAR(160) | bắt buộc |
| Phòng | Mã tòa nhà | `buildingId` | `Room.BuildingId` | `rooms.building_id` | INT NULL | thuộc cùng cơ sở |
| Bảng giá | Mã gói | `code` | `RatePlan.Code` | `rate_plans.code` | NVARCHAR(30) | duy nhất trong cơ sở |
| Bảng giá | Kiểu nghỉ | `mode` | `RatePlan.Mode` | `rate_plans.mode` | NVARCHAR(20) | OVERNIGHT/DAY_USE/HOURLY |
| Bảng giá | Giá cơ bản | `basePrice` | `RatePlan.BasePrice` | `rate_plans.base_price` | DECIMAL(18,2) | ≥0 |
| Bảng giá | Giờ bao gồm | `includedHours` | `RatePlan.IncludedHours` | `rate_plans.included_hours` | INT | ≥1 |
| Bảng giá | Phụ thu mỗi giờ | `extraHourPrice` | `RatePlan.ExtraHourPrice` | `rate_plans.extra_hour_price` | DECIMAL(18,2) | ≥0 (chưa tự tính) |
| Đặt phòng | Đại lý đặt | `agencyId` | `Reservation.AgencyId` | `reservations.agency_id` | INT NULL | thuộc cùng cơ sở |
| Đặt phòng | Loại lưu trú | `bookingMode` | `Reservation.BookingMode` | `reservations.booking_mode` | NVARCHAR(30) | 3 loại đã nêu |
| Hàng đợi | Tên khách | `guestName` | `WaitlistEntry.GuestName` | `waitlist_entries.guest_name` | NVARCHAR(150) | không rỗng |
| Hàng đợi | Ngày nhận/trả | `checkInDate` / `checkOutDate` | `CheckInDate` / `CheckOutDate` | `check_in_date` / `check_out_date` | DATE | ngày trả > ngày nhận |
| Hàng đợi | Hạng phòng | `roomTypeId` | `WaitlistEntry.RoomTypeId` | `waitlist_entries.room_type_id` | INT NULL | thuộc cùng cơ sở |
| Hàng đợi | Trạng thái | `status` | `WaitlistEntry.Status` | `waitlist_entries.status` | NVARCHAR(20) | WAITING/ASSIGNED/CANCELLED |
| Đoàn | Mã đoàn | `groupCode` | `Reservation.GroupCode` | `reservations.group_code` | NVARCHAR(30) | dùng chung cho 2–30 phòng |
| Chuyển dịch vụ | Charge nguồn | route `{id}` | `FolioCharge.Id` | `folio_charges.id` | INT | category SERVICE |
| Chuyển dịch vụ | Stay đích | `targetStayId` | `TransferChargeInput.TargetStayId` | `stays.id` | INT | đang lưu trú, cùng cơ sở |
| Dịch vụ | Hàng hóa gốc | `serviceItemId` | `FolioCharge.ServiceItemId` | `folio_charges.service_item_id` | INT NULL | nullable do dữ liệu V1 |
| NCC | Mã nhà cung cấp | `code` | `Supplier.Code` | `suppliers.code` | NVARCHAR(30) | duy nhất tại cơ sở |
| Kho | Hàng hóa | `serviceItemId` | `StockMovement.ServiceItemId` | `stock_movements.service_item_id` | INT | phải trackInventory |
| Kho | Loại phiếu | `type` | `StockMovement.Type` | `stock_movements.type` | NVARCHAR(24) | IN/OUT/RETURN/ADJUST/SALE/CONSUMPTION |
| Kho | Lượng tăng giảm | `quantityChange` | `StockMovement.QuantityChange` | `stock_movements.quantity_change` | INT | signed, ≠0, không âm tồn |
| Kho | Tồn sau phiếu | `balanceAfter` | `StockMovement.BalanceAfter` | `stock_movements.balance_after` | INT | backend tính |
| Kho | Giá vốn đơn vị | `unitCost` | `StockMovement.UnitCost` | `stock_movements.unit_cost` | DECIMAL(18,2) | ≥0 |
| Bán lẻ | Tổng bán | `totalAmount` | `RetailSale.TotalAmount` | `retail_sales.total_amount` | DECIMAL(18,2) | snapshot giá×SL |
| Bán lẻ | Ca thu | `shiftId` | `RetailSale.ShiftId` | `retail_sales.shift_id` | INT NULL | khi nhận CASH |
| Thu chi | Mã chứng từ | `code` | `CashVoucher.Code` | `cash_vouchers.code` | NVARCHAR(30) | backend sinh |
| Thu chi | Loại chứng từ | `type` | `CashVoucher.Type` | `cash_vouchers.type` | NVARCHAR(16) | RECEIPT / EXPENSE |
| Thu chi | Phương thức | `method` | `CashVoucher.Method` | `cash_vouchers.method` | NVARCHAR(20) | CASH/TRANSFER/CARD |
| Thu chi | Số tiền | `amount` | `CashVoucher.Amount` | `cash_vouchers.amount` | DECIMAL(18,2) | >0 |
| Công nợ | Nợ ban đầu | `originalAmount` | `DebtReceivable.OriginalAmount` | `debt_receivables.original_amount` | DECIMAL(18,2) | khoản còn thiếu khi checkout |
| Công nợ | Dư nợ | `outstandingAmount` | `DebtReceivable.OutstandingAmount` | `debt_receivables.outstanding_amount` | DECIMAL(18,2) | giảm khi thu nợ |
| Công nợ | Trạng thái | `status` | `DebtReceivable.Status` | `debt_receivables.status` | NVARCHAR(20) | OPEN/SETTLED/REVERSED |
| Tài sản | Nội dung tài sản | `description` | `GuestAsset.Description` | `guest_assets.description` | NVARCHAR(200) | bắt buộc |
| Tài sản | Thời điểm trả | `returnedAtUtc` | `GuestAsset.ReturnedAtUtc` | `guest_assets.returned_at_utc` | DATETIME2 NULL | chỉ xác nhận một lần |
| Snapshot | Ngày nghiệp vụ | `businessDate` | `DailyClose.BusinessDate` | `daily_closes.business_date` | DATE | duy nhất trong cơ sở |
| Snapshot | Tổng charge phòng | `roomCharges` | `DailyClose.RoomCharges` | `daily_closes.room_charges` | DECIMAL(18,2) | tính từ lịch sử ghi charge |

**Quan trọng:** `RatePlan` chưa được liên kết bằng FK đến Reservation; booking lấy giá snapshot và `bookingMode`. V2 không tự đối soát phát sinh vượt giờ. `DailyClose` không khóa hóa đơn hay bookings. Không xem các bảng này là thiết kế kế toán sản xuất.
