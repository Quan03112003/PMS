# BÁO CÁO QC — LOTUS PMS V2

**Ngày kiểm tra:** 05/11/2026  
**Phạm vi:** Gói `LOTUS_PMS_V2_FULLSTACK.zip` (54 file gốc), mở rộng 3 giai đoạn PMS  
**Người thực hiện:** Kiểm tra tự động trong môi trường ChatGPT  
**Bản phát hành kiểm tra:** V2 QA PATCH — sửa 1 lỗi C# chắc chắn chặn biên dịch, kèm script E2E trên môi trường Windows.

## 1. Kết luận phát hành

**Chưa đạt tiêu chí nghiệm thu E2E full-stack.** Có mã nguồn frontend Angular, API C# và định nghĩa database SQL Server, nhưng môi trường kiểm thử này không có .NET SDK, Docker/SQL Server và không truy cập npm registry để cài Angular. Vì vậy chưa thể xác nhận luồng **Angular → HTTP API → Entity Framework → SQL Server → Angular**. Không đánh dấu PASS cho phần chưa thực thi.

Đã thực thi được **16/16 kịch bản trên preview HTML OFFLINE** với Chromium (độc lập với hệ thống ASP.NET/SQL Server), **45/45 kiểm tra ánh xạ URL frontend–backend tĩnh**, kiểm tra cấu trúc 75 endpoint và JSON cấu hình. Có phát hiện **một lỗi chắn biên dịch C#** ở gói ban đầu và đã sửa trong gói QA PATCH.

## 2. Bảng kết quả

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| Kiểm tra file ZIP V2 gốc | PASS | Giải nén thành công; đủ 54 file |
| JavaScript preview | PASS | Tương tác Chromium thực tế trên HTML/JS nạp trực tiếp |
| Positive & negative UI tests (preview) | **16 PASS, 0 FAIL** | `qa/preview_results.json`, screenshot |
| Kiểm tra TypeScript parser | PASS | 4 file `.ts` parse được bằng TypeScript transpiler |
| Ánh xạ tuyến gọi API | PASS tĩnh | 45/45 lệnh gọi trực tiếp khớp 75 tuyến khai báo |
| JSON cấu hình | PASS | 6/6 file JSON phân tích thành công |
| Build Angular | **BLOCKED** | `ng: not found`; thiếu node_modules, npm registry không phân giải DNS |
| Build ASP.NET Core | **BLOCKED** | `dotnet: command not found` |
| Khởi chạy SQL Server / DBeaver | **BLOCKED** | Không có SQL Server/Docker trong môi trường chạy thử |
| Giao dịch E2E API với DB thật | **NOT RUN** | Không có backend và CSDL để xử lý request |
| Bảo mật / tải / đồng thời | **NOT RUN** | Chưa có môi trường tích hợp để kiểm thử |

## 3. Các test đã PASS trên preview offline

| ID | Kịch bản | Kết quả |
|---|---|---|
| PV-01 | Khởi tạo dashboard và menu | PASS |
| PV-02 | Dữ liệu mock của cơ sở | PASS |
| PV-03 | Mở form đặt phòng | PASS |
| PV-04 | Tạo đặt phòng mới | PASS |
| PV-05 | Check-in và chuyển trạng thái | PASS |
| PV-06 | Mở folio | PASS |
| PV-07 | Ghi nhận dịch vụ, trừ tồn kho | PASS |
| PV-08 | Thanh toán đủ folio | PASS |
| PV-09 | Checkout, phòng chuyển DIRTY | PASS |
| PV-10 | Dọn phòng: DIRTY → CLEAN | PASS |
| PV-11 | Duyệt phòng: CLEAN → INSPECTED | PASS |
| PV-12 | Chuyển cơ sở mà không lẫn dữ liệu | PASS |
| PV-13 | Chặn đặt phòng trùng lịch | PASS |
| PV-14 | Chặn checkout chưa thanh toán đủ | PASS |
| PV-15 | Chặn dịch vụ vượt tồn kho | PASS |
| PV-16 | Không có exception JS trong các bước | PASS |

**Giới hạn:** Dữ liệu lưu trên browser mock, không có tài khoản JWT, API EF Core hay SQL Server. Do đó các kết quả này **không được cộng vào tỷ lệ PASS E2E full-stack**.

## 4. Bug và rủi ro ghi nhận qua code review

### BUG-001 · P0 — C# không thể biên dịch vì trùng tên property trong anonymous object

- **Nơi phát hiện:** `backend/HotelPms.Api/AdvancedEndpoints.cs`, endpoint `POST /api/waitlist/{id}/assign`, dòng 72 của V2 gốc.
- **Mã gốc:** `new { booking.Id, booking.Code, w.Id }` có hai property suy luận cùng tên `Id`; C# không cho phép anonymous object trùng tên property.
- **Tác động:** Ngăn backend build, ngăn kiểm thử toàn hệ thống.
- **Đã sửa ở QA PATCH:** `new { bookingId = booking.Id, booking.Code, waitlistId = w.Id }`.
- **Xác minh:** Kiểm tra tĩnh vượt qua; **cần `dotnet build` thực tế để khẳng định hết lỗi compile**.

### RISK-002 · P1 — Chưa idempotent và chưa bảo đảm nguyên tử cho ghi nhận thanh toán

- **Vị trí:** `Program.cs`, `Ops.RecordPayment` khoảng dòng 459–475.
- **Quan sát:** Hai request thanh toán gửi lại/đồng thời có thể cùng vượt qua bước kiểm tra số dư trước khi ghi. Endpoint chưa yêu cầu `Idempotency-Key`, chưa khóa transaction cho cặp đọc-số-dư/ghi-thanh-toán.
- **Cần test:** Gửi đồng thời hai request có cùng số tiền khi số dư chỉ đủ một lần; dự kiến chỉ một giao dịch được chấp nhận.
- **Trạng thái:** Chưa kiểm chứng runtime, cần thiết kế chống duplicate và kiểm thử cạnh tranh.

### RISK-003 · P1 — Hủy booking đã có cọc chưa có hoàn tiền/đối soát

- **Vị trí:** `Program.cs`, endpoint `/reservations/{id}/cancel` khoảng dòng 223–229; endpoint nhận cọc khoảng dòng 334–339.
- **Quan sát:** Hủy booking chỉ đổi `Status` thành `CANCELLED`, không có bước hoàn tiền, chuyển cọc, hoặc yêu cầu phê duyệt; khoản thanh toán cọc vẫn nằm trong hệ thống.
- **Kịch bản:** Tạo booking → thu cọc 500.000 → hủy booking → kiểm tra tổng cọc, phiếu hoàn, báo cáo.
- **Trạng thái:** Thiếu luồng nghiệp vụ; chưa có E2E chạy thật.

### RISK-004 · P1 — Khôi phục checkout chưa soát các booking tương lai chồng lịch

- **Vị trí:** `AdvancedEndpoints.cs`, endpoint `/stays/{id}/restore` khoảng dòng 216–229.
- **Quan sát:** Chỉ soát phòng đang có stay `IN_HOUSE`, không kiểm tra reservation `CONFIRMED` trùng lịch và không soát tình trạng bảo trì.
- **Kịch bản:** Checkout → có booking mới nhận phòng cùng ngày → cố khôi phục stay cũ.
- **Trạng thái:** Cần bổ sung kiểm tra và chạy E2E negative case.

### GAP-005 · P2 — Khách đoàn chưa có danh sách người lưu trú theo từng phòng

- **Vị trí:** `AdvancedEndpoints.cs`, `/group-bookings` khoảng dòng 74–89.
- **Quan sát:** Một `GuestId` được dùng chung cho mọi reservation trong đoàn. Đại diện đoàn được ghi, nhưng chưa có người ở thực tế/giấy tờ của từng phòng.
- **Cần bổ sung:** `group_bookings`, `group_members`, `stay_guests`, chia/phân phòng và kiểm tra khai báo lưu trú.

### POLICY-006 · Cần chốt nghiệp vụ — Phòng DIRTY hiện tại không thể đặt trước trong tương lai

- **Vị trí:** `Program.cs`, `/availability`, `/reservations` và `/group-bookings`.
- **Quan sát:** Điều kiện `HousekeepingStatus == "INSPECTED"` áp dụng ngay khi tạo booking, cả khi check-in sau vài ngày/tháng. PMS thường phân biệt trạng thái sẵn sàng check-in hôm nay với tình trạng bán cho ngày tương lai.
- **Cần chốt:** Có cho phép đặt tương lai khi phòng đang DIRTY nhưng không bảo trì hay không? Nếu có, thay đổi validator ở bước booking, giữ điều kiện INSPECTED ở check-in.

### LIMIT-007 · Theo thiết kế — Chốt ngày chưa phải night audit kế toán

- **Vị trí:** `AdvancedEndpoints.cs`, `/daily-closes`.
- **Ghi chú:** API chỉ lưu snapshot; không khóa ghi sau snapshot, không phân bổ doanh thu theo từng đêm, không tự xử lý audit.

## 5. Bộ kiểm thử E2E thật đã chuẩn bị (chưa chạy được ở đây)

File `qa/e2e_api_sqlserver.py` chứa gần 30 ca kiểm tra tích hợp qua HTTP, thao tác trực tiếp vào CSDL demo: đăng nhập JWT, lấy cơ sở, tìm phòng, kiểm tra booking trùng, check-in, ghi minibar, thu tiền, checkout, housekeeping, khách đoàn, hàng đợi, công nợ, phiếu chi, báo cáo chuỗi và phân quyền. Script là **bộ kiểm thử được chuẩn bị**, chưa chứng minh các ca đã PASS.

**Cách chạy Windows:**

```powershell
# Đã mở SQL Server + .NET API + Angular theo README
py qa\static_qa_checks.py
py qa\e2e_api_sqlserver.py --confirm-demo
```

Sau đó xem `pms_v2_api_e2e_results.json`; trong DBeaver chạy `qa/05-e2e-verify-dbeaver.sql` để so sánh dữ liệu. Script chỉ cho phép host localhost và chỉ chạy khi có `--confirm-demo`. **Không sử dụng database khách thật vì kịch bản có ghi dữ liệu.**

## 6. Exit criteria trước khi tuyên bố hoàn thành

1. `dotnet restore && dotnet build` thành công; `npm install && npm run build` thành công.
2. API `/api/health` sẵn sàng, tạo schema SQL Server và seed đúng dữ liệu.
3. Chạy `qa/e2e_api_sqlserver.py --confirm-demo` đạt các kịch bản cơ bản; kết quả đối chiếu SQL trùng expected.
4. Chạy E2E qua **giao diện Angular thật** có browser automation, không chỉ API/preview.
5. Sửa ít nhất BUG-001 và các RISK P1 trước khi cân nhắc pilot nghiệp vụ.
6. Có báo cáo kiểm thử bảo mật, cạnh tranh giao dịch, rollback, log, backup/restore và kiểm thử cập nhật bản V1 → V2.

**Final Verdict hiện tại: NOT READY FOR PRODUCTION — FULLSTACK E2E BLOCKED, OFFLINE PREVIEW PASS.**
