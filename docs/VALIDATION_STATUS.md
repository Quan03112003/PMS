# Mức kiểm thử thực tế khi đóng gói LOTUS PMS V2

- Đã đọc cấu trúc V1 ZIP và sửa trên mã V1, không viết mới file dự án trống.
- Đã chạy TypeScript parser của TypeScript global: parse `app.component.ts`, `api.service.ts`, `advanced.component.ts` thành công.
- Đã đối chiếu bằng script tĩnh các API V2, bảng SQL nâng cấp, field dictionary, file Angular và tài liệu.
- Đã kiểm tra file ZIP sau khi đóng gói và toàn bộ đường dẫn có thực.
- **Chưa `dotnet build`**: máy tạo mã không có .NET SDK.
- **Chưa `npm run build`**: máy tạo mã không kết nối npm registry để tải Angular dependencies.
- **Chưa chạy database SQL Server/DBeaver**: môi trường không có Docker/SQL Server.
- **Chưa chạy tích hợp frontend+backend, UAT, penetration test, load test**.

Hướng kiểm thử tại máy người dùng Windows:

1. `dotnet build` trong `backend/HotelPms.Api`. Nếu lỗi, sửa compile trước khi chạy seed.
2. `npm install && npm run build` trong `frontend`. Nếu lỗi template Angular, gửi log build.
3. Chạy `01_START_DATABASE.bat`, `02_START_BACKEND.bat`, `03_START_ANGULAR.bat` theo README.
4. Mở PowerShell chạy `.\05_SMOKE_TEST_V2.ps1` và theo checklist `V2_QC_SCENARIOS.md`.
5. Chạy `sql/04-check-v2.sql` trong DBeaver đối chiếu.

**Trạng thái phát hành:** Code demo chưa nghiệm thu build/integration, không dùng production. Những giới hạn nghiệp vụ cố ý được liệt kê trong `V2_FEATURE_MATRIX.md` và README.
