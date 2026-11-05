# LOTUS PMS V3 — START HERE

**Không dùng ngay trên hệ thống khách thật.** Bản này là QA Safety Patch; chưa build tích hợp/kiểm thử E2E thật thành công trong môi trường tạo dự án.

- Tính năng V1/V2 và cách chạy chung: `README.md`
- Phần mới & tên biến: `docs/V3_CHANGELOG.md`
- Bảng trạng thái nghiệm thu (PASS/BLOCKED) và cách chạy: `qa/V3_NGHIEM_THU.md`
- Với CSDL V2 sẵn có, **backup trước** rồi chạy `sql/05-v3-qa-safety-upgrade.sql` trong DBeaver.
- Build gate Windows: `powershell -ExecutionPolicy Bypass -File qa/06_acceptance_build_windows.ps1`
- Sau khi bật SQL/API/Angular, test gate: `powershell -ExecutionPolicy Bypass -File qa/07_acceptance_e2e_windows.ps1 -ConfirmDemo`
- Demo preview HTML trong `demo-preview/` là mock offline V1, không chứng minh E2E Angular/.NET/SQL.
