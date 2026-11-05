# Trạng thái kiểm tra tại thời điểm bàn giao

- Đã kiểm tra parse cú pháp TypeScript: `frontend/src/main.ts`, `frontend/src/app/api.service.ts`, `frontend/src/app/app.component.ts` bằng TypeScript transpiler.
- Đã kiểm tra JavaScript `demo-preview/preview.js` bằng Node `--check`.
- Đã chạy mô phỏng không DOM thực: 7 màn hình preview tạo nội dung, thực hiện liên tục `booking → check-in → add service → payment → checkout`, xác nhận room DIRTY.
- Đã kiểm tra JSON cấu hình và file Word gốc tồn tại.
- **Chưa thể build Angular bằng Angular CLI** tại môi trường tạo (thiếu `@angular/*` node_modules và không tải được package).
- **Chưa thể chạy/compile ASP.NET Core 10 và kết nối SQL Server** tại môi trường tạo (không có .NET SDK/Docker Engine).
- **Chưa kiểm thử browser screenshot tương tác thật** ở môi trường tạo.

Vì vậy trạng thái là **mã nguồn demo + bản xem trước offline đã smoke test logic**, chưa phải full-stack được kiểm thử E2E. Khi chạy trên Windows, kiểm tra các bước trong `docs/QC_TEST_CASES.md`.
