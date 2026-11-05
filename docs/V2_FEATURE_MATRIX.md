# LOTUS PMS V2 — Ma trận chức năng, mức hoàn thiện

**Quy ước:** `DEMO-CODE` = có mã API và màn hình Angular, chưa chạy integration test Windows; `V1` = kế thừa bản đầu; `PARTIAL` = có phần nghiệp vụ nhưng không trọn quy tắc sản xuất; `NOT-INTEGRATED` = chưa tích hợp vào nhà cung cấp bên ngoài. Trạng thái không đồng nghĩa đã nghiệm thu.

| Giai đoạn | ID | Chức năng | Phạm vi mã nguồn | Mức độ |
|---|---|---|---|---|
| 1 | FO-01 | Sơ đồ phòng, đặt phòng khách lẻ | `Program.cs`, `app.component` | V1 |
| 1 | FO-02 | Khách đoàn 2–30 phòng: tạo, nhận, trả | `AdvancedEndpoints.cs` `/group-bookings`, `/groups/...` | DEMO-CODE |
| 1 | FO-03 | Thanh toán hộ đoàn, chia dư nợ theo từng booking | `/groups/{code}/pay` | DEMO-CODE |
| 1 | FO-04 | Khách hàng đợi: tạo, hủy, chuyển đặt phòng | `/waitlist` | DEMO-CODE |
| 1 | FO-05 | Walk-in (tạo reservation + check-in thủ công) | V1 `reservations`, `check-in` | PARTIAL |
| 1 | FO-06 | Bảng giá nghỉ giờ/ngày/đêm, chốt giá booking | `/rate-plans`, `bookingMode` | PARTIAL: chưa tính giờ tự động |
| 1 | FO-07 | Đổi phòng / gia hạn / buồng phòng | V1 `stays/transfer`, `extend` | V1 |
| 1 | FO-08 | Chuyển dịch vụ partial/full | `/charges/{id}/transfer` | DEMO-CODE |
| 1 | FO-09 | Thu cọc/thu tiền, folio, checkout thanh toán đủ | V1 | V1 |
| 1 | FO-10 | Checkout ghi nợ, thu nợ nhiều lần | `/checkout-debt`, `/debts/collect` | DEMO-CODE |
| 1 | FO-11 | Khôi phục checkout cùng ngày, chưa thu nợ | `/stays/{id}/restore` | PARTIAL: dùng ngày UTC |
| 1 | FO-12 | Nhận/trả tài sản khách lưu trú | `/guest-assets` | DEMO-CODE |
| 1 | FO-13 | Danh sách khai báo lưu trú CSV | `/reports/registration-csv` | PARTIAL: không mẫu pháp lý |
| 2 | MG-01 | Quản lý khách sạn toàn chuỗi | V1 `properties`, scopes | V1 |
| 2 | MG-02 | Tòa nhà, gán phòng | `/buildings`, `rooms/.../building` | DEMO-CODE |
| 2 | MG-03 | Danh mục phòng/hạng phòng/giá | V1, `/rate-plans` | PARTIAL |
| 2 | MG-04 | Nhân viên và phân quyền theo role/cơ sở | V1 `users` JWT | PARTIAL: chưa quyền thao tác mịn |
| 2 | MG-05 | Danh mục dịch vụ / minibar | V1 `services` | V1 |
| 2 | MG-06 | Kho nhập, xuất, trả, điều chỉnh signed ledger | `/stock/movements` + V1 stock hooks | DEMO-CODE |
| 2 | MG-07 | Nhà cung cấp | `/suppliers` | DEMO-CODE |
| 2 | MG-08 | Bán hàng lẻ ngoài phòng | `/retail-sales` | DEMO-CODE |
| 2 | MG-09 | Phiếu thu/chi ngoài folio | `/vouchers` | DEMO-CODE |
| 2 | MG-10 | Ca thu ngân, đối soát tiền mặt | V1 `shifts` + `/shifts/{id}/reconcile` | DEMO-CODE |
| 2 | MG-11 | Đại lý, commission rate | V1 `agencies`, Reservation.AgencyId | PARTIAL |
| 3 | RP-01 | Tổng hợp nhiều cơ sở theo vai trò | `/reports/chain` | DEMO-CODE |
| 3 | RP-02 | Công suất phòng ước tính (room nights) | `/reports/chain` | PARTIAL: không actual nights |
| 3 | RP-03 | Báo cáo hiệu quả đại lý và ước tính hoa hồng | `/reports/agencies` | PARTIAL |
| 3 | RP-04 | Thẻ kho và báo cáo tồn | `/stock/movements`, `/reports/stock` | DEMO-CODE |
| 3 | RP-05 | Chi tiết công nợ và thu nợ | `/debts` | DEMO-CODE |
| 3 | RP-06 | Snapshot tổng hợp theo ngày | `/daily-closes` | PARTIAL: không phải night audit |
| 3 | RP-07 | Xuất bảng CSV / in PDF qua trình duyệt | Angular + csv API | PARTIAL |
| 3 | RP-08 | Nhật ký theo tài khoản/cơ sở | V1 `audit` | V1 |
| ALL | EX-01 | Đồng bộ OTA/channel manager | Không có integration | NOT-INTEGRATED |
| ALL | EX-02 | Tổng đài điện thoại PBX | Không có integration | NOT-INTEGRATED |
| ALL | EX-03 | Hóa đơn điện tử và thuế | Không có integration | NOT-INTEGRATED |
| ALL | EX-04 | Nhà hàng/Spa/POS, chấm công và bảng lương | Không có integration | NOT-INTEGRATED |
| ALL | EX-05 | Revenue accounting khóa đêm & kế toán kép | Không có accounting engine | NOT-INTEGRATED |
| ALL | EX-06 | Hourly stay by minute, overtime, holiday rates | Có config, thiếu billing engine | PARTIAL |
| ALL | EX-07 | Refund, split folio nâng cao, void có duyệt | Không có đầy đủ | NOT-INTEGRATED |
| ALL | EX-08 | PII compliance, eKYC, pháp lý tạm trú | Không đầy đủ | NOT-INTEGRATED |

Tài liệu HBSOFT tham khảo: https://hbsoft.vn/product.html ; https://hbsoft.vn/help.html . Đây là ma trận **của LOTUS PMS** so với nghiệp vụ phổ biến, không đại diện chức năng bên cung cấp.
