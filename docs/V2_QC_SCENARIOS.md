# LOTUS PMS V2 — Kịch bản nghiệm thu QC theo ba giai đoạn

Mỗi case phải ghi bằng chứng API status, request/response, thay đổi SQL theo `id` và ảnh màn hình Angular. Bắt đầu từ dữ liệu mẫu mới hoặc dùng bản backup trước mỗi vòng kiểm thử. Không chạy các case phá dữ liệu trên cơ sở thật.

| TC | Phân hệ | Đầu vào/điều kiện | Expected |
|---|---|---|---|
| FO-001 | Khách đoàn | Chọn 2 phòng khả dụng, ngày hợp lệ | 200, 2 reservation cùng `group_code` |
| FO-002 | Khách đoàn | 1 phòng trùng booking | 409, không tạo dở nhóm |
| FO-003 | Khách đoàn | Hai roomIds giống nhau | 400 |
| FO-004 | Khách đoàn | Chọn phòng khác cơ sở | 409, transaction rollback |
| FO-005 | Khách đoàn | Check-in tất cả phòng chưa dọn xong | 409, không check-in một phần |
| FO-006 | Khách đoàn | Nhận đủ phòng hợp lệ | Các stay IN_HOUSE, charge ROOM |
| FO-007 | Thanh toán hộ | Số tiền nhỏ hơn tổng dư nợ | Chia theo booking, tổng payments đúng |
| FO-008 | Thanh toán hộ | Vượt tổng dư nợ | 409, không ghi payment |
| FO-009 | Thanh toán hộ | Tiền mặt không mở ca | 409, cần mở ca |
| FO-010 | Checkout đoàn | Một folio chưa cân bằng | 409, không checkout dở |
| FO-011 | Checkout đoàn | Tất cả folio cân bằng | Checkout, phòng DIRTY |
| FO-012 | Hàng đợi | Tên rỗng, ngày trả ≤ ngày nhận | 400 |
| FO-013 | Hàng đợi | Gán phòng thuộc cơ sở khác | 409 |
| FO-014 | Hàng đợi | Phòng đúng hạng, không trùng | WAITING → ASSIGNED, có reservationId |
| FO-015 | Hàng đợi | Gán lại item ASSIGNED | 409 |
| FO-016 | Hàng đợi | Hủy WAITING hai lần | Lần 1 OK, lần 2 409 |
| FO-017 | Chuyển dịch vụ | Chuyển 1/2 minibar từ A sang B | charge A giảm, charge B tăng, tổng không đổi |
| FO-018 | Chuyển dịch vụ | Chuyển charge ROOM | 409 |
| FO-019 | Chuyển dịch vụ | Chuyển số lượng > source | 400 |
| FO-020 | Chuyển dịch vụ | Chuyển làm folio A < đã thu | 409 |
| FO-021 | Chuyển dịch vụ | A và B khác cơ sở | 403 |
| FO-022 | Công nợ | Checkout nợ từ stay đang ở | Tạo DebtReceivable, phòng DIRTY |
| FO-023 | Công nợ | Checkout ghi nợ với số dư 0 | 409 |
| FO-024 | Công nợ | Thu nợ một phần | `outstandingAmount` giảm, payment Kind DEBT |
| FO-025 | Công nợ | Thu vượt dư nợ | 400, không lưu |
| FO-026 | Công nợ | Thu đủ | Status SETTLED, dư nợ 0 |
| FO-027 | Công nợ | Thu nợ CASH không mở ca | 409 |
| FO-028 | Khôi phục | Checkout trong ngày, phòng chưa có khách, nợ chưa thu | Khôi phục thành IN_HOUSE, nợ REVERSED |
| FO-029 | Khôi phục | Nợ đã thu một phần | 409 |
| FO-030 | Khôi phục | Phòng đã có khách khác | 409 |
| FO-031 | Tài sản | Nhận tài sản cho stay đang ở | Có guest_assets, chưa trả |
| FO-032 | Tài sản | Xác nhận trả lần 2 | 409 |
| FO-033 | Bảng giá | Nhập giá âm, giờ 0 | 400 |
| FO-034 | Bảng giá | Hai gói trùng code trong cơ sở | 409 |
| FO-035 | Bảng giá | Chọn HOURLY trong booking | Ghi bookingMode, charge một block (demo) |
| MG-001 | Tòa nhà | Tạo tòa nhà, gán phòng thuộc cơ sở | Có building_id trong rooms |
| MG-002 | Tòa nhà | Gán phòng vào tòa cơ sở khác | 400 |
| MG-003 | Kho | Nhập 10 chai | Stock +10, ledger change +10 |
| MG-004 | Kho | Xuất 3 chai | Stock -3, ledger change -3 |
| MG-005 | Kho | Xuất > tồn | 409, không ghi ledger |
| MG-006 | Kho | Điều chỉnh signed ±5 | Stock hợp lệ, sổ thẻ kho khớp |
| MG-007 | Kho | Nhập kho với NCC sai cơ sở | 400 |
| MG-008 | Minibar | Ghi 2 nước phòng | Stock -2, movement CONSUMPTION -2, charge |
| MG-009 | Bán lẻ | Bán 1 nước, chuyển khoản | RetailSale, stock -1, movement SALE |
| MG-010 | Bán lẻ | Bán CASH không mở ca | 409 |
| MG-011 | Bán lẻ | Bán quá tồn | 409 |
| MG-012 | Thu chi | Tạo EXPENSE số tiền âm | 400 |
| MG-013 | Thu chi | CASH chưa mở ca | 409 |
| MG-014 | Thu chi | Transfer hợp lệ | Lưu cash_vouchers, audit |
| MG-015 | Phân quyền | Reception tạo voucher | 403 |
| MG-016 | Phân quyền | Manager cơ sở A xem stock B | 403 |
| MG-017 | Đối soát ca | Thu tiền mặt 1 booking + bán lẻ + phiếu chi | Quỹ kỳ vọng khớp công thức |
| RP-001 | Chuỗi | Chain admin xem 2 cơ sở | Response có cả 2 |
| RP-002 | Chuỗi | Manager chỉ thấy cơ sở được cấp | Response chỉ 1 cơ sở |
| RP-003 | Chuỗi | Từ > đến | 400 |
| RP-004 | Chuỗi | Khoảng >366 ngày | 400 |
| RP-005 | Công suất | 1 phòng có 2 room nights trong kỳ 10 phòng × 10 ngày | 2% công suất ước tính |
| RP-006 | Đại lý | Booking gán agency, tạo charge ROOM | Có dự kiến commission theo % |
| RP-007 | Đại lý | Booking trực tiếp không agency | Không tính vào agency report |
| RP-008 | Snapshot | Chốt cùng ngày hai lần | 1 bản, lần 2 409 |
| RP-009 | Snapshot | Chốt ngày tương lai | 400 |
| RP-010 | Xuất CSV | Có ký tự dấu phẩy, dấu ngoặc kép trong tên | CSV escape hợp lệ |
| RP-011 | Nhật ký | Lưu chứng từ | AuditEvent có action, actor, entity |

**Kiểm thử bổ sung bắt buộc trước production:** đồng thời double submit/two reception, giao dịch rollback do SQL timeout/deadlock, idempotency thanh toán, timezone theo cơ sở, protected PII, kiểm tra SQL injection, XSS, auth expired, phân quyền action-level, migration rollback, backup/restore, load/performance, chuẩn pháp lý và reconciliation với payment provider.
