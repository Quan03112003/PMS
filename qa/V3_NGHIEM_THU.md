# BÁO CÁO KIỂM THỬ & NGHIỆM THU LOTUS PMS V3

**Ngày:** 05/11/2026  
**Đối tượng:** LOTUS PMS V3 QA Safety Patch; kế thừa toàn bộ V2 QA Patch  
**Kết luận hiện tại:** **CHƯA ĐỦ ĐIỀU KIỆN NGHIỆM THU E2E / KHÔNG ĐƯỢC TRIỂN KHAI PRODUCTION.**

## 1. Bằng chứng thực thi ở môi trường hiện tại

| Hạng mục | Kết quả | Bằng chứng/giới hạn |
|---|---|---|
| Kiểm thử browser preview HTML V1 | **16 PASS / 0 FAIL** | `qa/preview_results.json`, screenshot. Dùng dữ liệu mock, KHÔNG phải Angular/SQL |
| Đối chiếu URL Angular → C# | **47/47 PASS tĩnh** | `python qa/static_qa_checks.py` (80 route backend được tìm thấy) |
| Hợp đồng mã nguồn V3 | **16/16 PASS tĩnh** | `python qa/test_v3_static_contracts.py`, `qa/v3_source_contract_results.json`; không chứng minh hành vi runtime |
| Phân tích parser TypeScript | **3/3 file `.ts` PASS** | `ts.transpileModule`; chưa typecheck/build Angular |
| Python test runner | **PASS compile** | `py_compile` cho script API và browser |
| `dotnet build` | **BLOCKED** | Không có `dotnet` trong môi trường, xem `qa/evidence/environment_build_attempt.txt` |
| `npm run build` / `ng build` | **BLOCKED** | Thiếu dependencies, npm registry không có trong cache |
| SQL Server & Docker | **BLOCKED** | Không có Docker hoặc SQL Server trong môi trường |
| API smoke test | **BLOCKED tại health** | Kết nối `localhost:5080` bị từ chối. `qa/evidence/api_e2e_attempt.log` |
| API/SQL test đồng thời & hoàn cọc | **NOT RUN** | Đã viết ca test; chưa có API/DB để thực thi |
| Angular UI thật → .NET → SQL | **NOT RUN** | `qa/e2e_angular_browser.py` được chuẩn bị, chưa thể thực thi trong môi trường này |

**Không được hiểu 16/16 preview và 47/47 static là tỷ lệ PASS E2E full-stack.**

## 2. Các bug/rủi ro được xử lý trong mã nhưng phải chạy lại

- **P0-COMPILE** Anonymous C# duplicate `Id` đã sửa ở V2 QA Patch; chưa xác minh bằng compiler .NET.
- **P1-DOUBLE-PAYMENT** Thêm `idempotencyKey` dạng UUID + unique index và khóa reservation; cần chạy test replay & race thật.
- **P1-CANCEL-DEPOSIT** Chặn hủy khi còn cọc; hoàn qua chứng từ âm có mã tham chiếu và phân quyền quản lý; cần đối chiếu số tiền DBeaver.
- **P1-RESTORE** Chặn khôi phục checkout nếu có booking khác trùng khoảng ngày hoặc phòng bảo trì; cần chạy các case âm.
- **P1-INVENTORY** Khóa mặt hàng khi nhập xuất/bán/ghi minibar; cần chạy stress test tồn kho.
- **P2-GROUP-REGISTRATION** Thêm danh sách khách ở thực tế cho từng phòng đoàn. UI đã có form nhập; chưa khóa check-in khi hồ sơ khách thiếu thông tin quy định.
- **P2-FUTURE-ROOM** Cho phép bán phòng DIRTY trong tương lai, vẫn chặn check-in khi chưa duyệt phòng.

## 3. Các điều kiện bắt buộc để ký nghiệm thu **demo tích hợp**

1. Chạy `qa/06_acceptance_build_windows.ps1`: `dotnet restore/build Release`, `npm install`, `npm run build`, `npm run test:types` trả exit code 0.
2. Backup/khởi tạo database test; chạy đúng script V3 trên DBeaver; xác minh 2 cột/bảng mới (`payments.idempotency_key`, `reservation_occupants`) và unique filtered index.
3. Chạy backend `http://localhost:5080/api/health` được 200, đăng nhập JWT admin và lễ tân được.
4. Chạy `qa/07_acceptance_e2e_windows.ps1 -ConfirmDemo`; toàn bộ **case bắt buộc PASS**, không có FAIL/BLOCKED. Đối chiếu output JSON và `qa/05-e2e-verify-dbeaver.sql`.
5. Chạy trực tiếp Angular `http://localhost:4200`, chạy Playwright thật, không có JS exception, kết quả booking/stay/payment/status trên API/SQL khớp UI.
6. Dữ liệu thử chỉ nằm ở database **demo**; khôi phục backup V2 → migrate V3 thử lại, không mất dữ liệu trước nâng cấp.

## 4. Bắt buộc bổ sung trước nghiệm thu **production**

- Night audit/khóa sổ theo ngày kinh doanh, thuế, phụ thu, ghi sổ đúng quy tắc; đối soát chênh lệch tiền mặt.
- Security: RBAC cấp hành động, bảo vệ thông tin khách, quản lý secret, HTTPS, audit không chỉnh sửa, khóa tài khoản, rate limiting, rà soát OWASP.
- Performance: nhiều người cùng đặt phòng, thanh toán, trừ kho; benchmark và xử lý lỗi deadlock SQL có chính sách retry idempotent.
- Backup/restore, disaster recovery, giám sát và cảnh báo, cập nhật schema an toàn, test upgrade/rollback.
- Đối soát với stakeholder khách sạn về nghiệp vụ đặc thù và hồ sơ lưu trú; nghiệm thu UAT ký biên bản.

## 5. Chạy trên máy Windows (bắt buộc dữ liệu mẫu)

1. Cài .NET 10 SDK, Node.js 22, Docker Desktop (SQL Server 2022), Python 3.10+, DBeaver.
2. Tại thư mục dự án: `powershell -ExecutionPolicy Bypass -File qa/06_acceptance_build_windows.ps1`.
3. Nếu đã dùng database V2: backup, mở DBeaver chạy `sql/05-v3-qa-safety-upgrade.sql` dạng **Execute SQL Script**; nếu database mới thì schema sẽ được tạo từ V3 model khi chạy backend ở Development.
4. Mở 3 terminal và chạy `01_START_DATABASE.bat`, `02_START_BACKEND.bat`, `03_START_ANGULAR.bat` (đợi từng dịch vụ khởi động thành công).
5. `py -m pip install playwright` và `py -m playwright install chromium` nếu chưa cài Playwright.
6. Chạy `powershell -ExecutionPolicy Bypass -File qa/07_acceptance_e2e_windows.ps1 -ConfirmDemo`.
7. Đối chiếu `qa/pms_v3_api_e2e_results.json`, `qa/angular_e2e_results.json` và query `qa/05-e2e-verify-dbeaver.sql`.

**Không triển khai gói này vào môi trường khách sạn thật khi chưa hoàn thành các gate trên.**
