# LOTUS PMS V2 — QA patch và kiểm thử

## Đã kiểm tra trong môi trường tạo bộ test

- `python qa/test_preview.py`: **16/16 PASS** trên bản xem trước HTML/JS OFFLINE với Chromium (dữ liệu mock trong trình duyệt, không có ASP.NET Core và SQL Server).
- `python qa/static_qa_checks.py`: **PASS** — xác minh 45 tuyến gọi API trực tiếp từ Angular trùng định tuyến C#, không còn trường tên trùng trong anonymous-object đơn giản; 6 file cấu hình JSON hợp lệ.
- TypeScript: 4 file qua kiểm tra cú pháp transpiler. Đây không phải Angular production build.
- Đã sửa **lỗi C# chắn biên dịch** trong `backend/HotelPms.Api/AdvancedEndpoints.cs`: `new { booking.Id, booking.Code, w.Id }` bị trùng tên `Id`; sửa thành `new { bookingId = booking.Id, booking.Code, waitlistId = w.Id }`.
- Không thể build .NET/Angular/SQL Server ở môi trường hiện tại do thiếu SDK/runtime/dependencies. **Chưa có E2E fullstack được xác minh**.

## Chạy E2E API + SQL Server trên máy Windows (database demo)

1. Mở 3 terminal từ root dự án. Chạy `01_START_DATABASE.bat`, `02_START_BACKEND.bat`, `03_START_ANGULAR.bat` theo README chính. Bắt buộc chạy được `http://localhost:5080/api/health` trước.
2. **Chỉ dùng database DEMO**. Kịch bản tạo booking, check-in, thu tiền giả, phiếu chi giả và khách đoàn. Không chạy với database thật hoặc chứa giao dịch của khách thật.
3. Mở PowerShell trong thư mục dự án:

```powershell
py qa\e2e_api_sqlserver.py --confirm-demo
```

4. Xem `pms_v2_api_e2e_results.json` tại thư mục lệnh đang chạy. Kết quả `PASS/FAIL` tự được ghi riêng cho từng case.
5. DBeaver: mở connection đến database `HotelChainPms`, chạy `qa/05-e2e-verify-dbeaver.sql` để đối chiếu booking, stay, công nợ, tồn kho, chứng từ.
6. Nếu có lỗi, gửi `pms_v2_api_e2e_results.json` và log `dotnet build` / `npm run build` để xác định lỗi code/runtime.

**Cấu hình script:** Python 3.10+ (standard library, không cần `pip install`). Mặc định gọi `http://localhost:5080/api`, chỉ cho phép `localhost`/`127.0.0.1`, phải thêm `--confirm-demo`. Mỗi lần chạy sinh `E2E-XXXXXXXX` riêng để không trùng dữ liệu.

## Chạy kiểm thử HTML offline (tùy chọn)

```powershell
py -m pip install playwright
py -m playwright install chromium
py qa\test_preview.py
```

Script tạo `qa/preview_results.json` và `qa/preview_test_screenshot.png`. Kết quả test này **không đại diện Angular + API + SQL Server**.

## Kiểm tra tĩnh độc lập

```powershell
py qa\static_qa_checks.py
```

## Giới hạn chưa được nghiệm thu

- Hoàn tiền khi hủy booking đã nhận cọc, xử lý đồng thời/thử lại thanh toán, kiểm tra đặt chồng phòng khi hoàn tác checkout, chính sách bán phòng tương lai nếu phòng đang DIRTY, lịch sử khách riêng cho từng phòng đoàn, các phần tích hợp bên ngoài.
- Bản snapshot chốt ngày chưa phải night audit kế toán thật.

## BỔ SUNG V3 – acceptance test

Bản V3 có script build `qa/06_acceptance_build_windows.ps1` và test tích hợp `qa/07_acceptance_e2e_windows.ps1`, chạy trên Windows/Docker+SQL Server demo. Kịch bản nâng cấp, migration, phân quyền và giới hạn cụ thể nằm ở `qa/V3_NGHIEM_THU.md`. Mã thanh toán V3 yêu cầu thuộc tính JSON `idempotencyKey` là UUID mới cho mỗi **thao tác thu tiền mới**; gửi lại cùng key sẽ trả giao dịch cũ (không tạo thêm). Điều này áp dụng cho `/deposit`, `/payments`, `/refund-deposit`, `/groups/.../pay`, `/debts/.../collect`.
