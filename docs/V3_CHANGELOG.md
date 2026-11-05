# LOTUS PMS V3 — QA SAFETY PATCH (05/11/2026)

## Đã sửa/bổ sung trong mã nguồn

| Mã | Module | Thay đổi | SQL migration | Trạng thái kiểm chứng |
|---|---|---|---|---|
| V3-FIN-01 | Thanh toán folio / cọc | UUID `idempotencyKey`, chỉ ghi một lần khi gửi lại; transaction Serializable và khóa theo reservation | `payments.idempotency_key` + unique filtered index | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FIN-02 | Hủy đặt phòng / hoàn cọc | Không được hủy khi còn cọc; API hoàn cọc ghi payment âm `REFUND`, mã tham chiếu bắt buộc, giới hạn manager | dùng cột V3-FIN-01 | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FIN-03 | Thanh toán đoàn | UUID dùng cho lệnh nhiều phòng, khóa reservation theo thứ tự ID, chống ghi trùng từng khoản | dùng cột V3-FIN-01 | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FIN-04 | Thu công nợ | UUID và khóa transaction, cho phép replay mà không trừ nợ lần hai | dùng cột V3-FIN-01 | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FD-01 | Khôi phục checkout | Chặn phòng bảo trì, có khách khác, đặt phòng mới chồng lịch; tái sử dụng hồ sơ nợ `REVERSED` | không | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FD-02 | Bán phòng tương lai | Phòng đang DIRTY vẫn được đặt ngày tương lai, nhưng check-in vẫn đòi INSPECTED | không | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FD-03 | Tranh chấp phòng | Khóa room trước booking, check-in, waitlist, group | không | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-FD-04 | Khách đoàn | `reservation_occupants` lưu khách ở thực tế theo từng booking/phòng; API GET/POST/DELETE và màn hình đăng ký người ở | `reservation_occupants` | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-INV-01 | Kho và minibar | Khóa service trước thay đổi tồn, đảm bảo tính tuần tự khi ghi | không | Kiểm tra tĩnh PASS; runtime BLOCKED |
| V3-QA-01 | Kiểm thử | Bộ test API đồng thời, hoàn tiền, phòng tương lai, khôi phục; trình duyệt Angular thật; static contracts | không | Bộ test được tạo; chưa chạy được với DB thật |

## Tên biến/API bổ sung

| Nhãn nghiệp vụ | JSON | C# | SQL Server |
|---|---|---|---|
| Mã chống gửi lặp thu tiền | `idempotencyKey` | `Payment.IdempotencyKey` | `payments.idempotency_key` |
| Khách ở theo phòng | `occupants[]` | `ReservationOccupant` | `reservation_occupants` |
| ID đặt phòng của khách thực tế | `reservationId` | `ReservationOccupant.ReservationId` | `reservation_occupants.reservation_id` |
| Khách chính | `isPrimary` | `IsPrimary` | `is_primary` |
| 4 ký tự cuối giấy tờ | `identityLast4` | `IdentityLast4` | `identity_last4` |
| Hoàn cọc | `kind: "REFUND"` | `Payment.Kind` | `payments.kind` |

**Không lưu số giấy tờ đầy đủ trong phiên bản demo**. Số giấy tờ 4 ký tự cuối chỉ phục vụ phân biệt đối chiếu mẫu, không phải giải pháp khai báo lưu trú đủ chuẩn.

## Tương thích

- **Database mới**: `EnsureCreated` dùng model V3; seed tạo dữ liệu mẫu trong môi trường Development.
- **Đã có V2**: backup database, chạy `sql/05-v3-qa-safety-upgrade.sql` trên DBeaver **trước** khi khởi động backend V3.
- **Đang V1**: backup, chạy `sql/03-v2-upgrade.sql` rồi `sql/05-v3-qa-safety-upgrade.sql`.
- Không sử dụng scripts trên database production; cần migrations + kế hoạch backup/rollback được nghiệm thu riêng.

## Giới hạn mở — chưa phải production

- Chưa tự động tính phụ thu giờ/đêm/ngày lễ; chưa có night audit/khóa sổ kế toán thật.
- Chưa hoàn thiện tích hợp hóa đơn điện tử, OTA/PBX/SMS, danh sách khai báo lưu trú theo quy định hiện hành.
- Không có kiểm thử tải, penetration test, kiểm tra hiệu năng SQL Server, quy trình backup/restore và DR nghiệm thu.
- Phiên bản demo mới có phân quyền theo vai trò/cơ sở, chưa thiết kế đầy đủ ma trận quyền từng hành động.
- Chưa chạy được build & E2E thật trong môi trường tạo phiên bản. Xem `qa/V3_NGHIEM_THU.md`.
