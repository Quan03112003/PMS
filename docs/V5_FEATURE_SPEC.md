# LOTUS PMS V5 — FEATURE SPECIFICATION & REQUIREMENTS

**Mục tiêu:** Nâng từ V4 (QA Safety Patch) lên V5 (Production-Ready).
**Ngày:** 06/11/2026
**Trạng thái:** Draft — chờ nghiem thu

---

## 1. TỔNG QUAN V5

| Mục tiêu V5 | Mô tả |
|-------------|-------|
| Production-ready | Build CI/CD, load test, penetration test, backup/restore, DR |
| Multi-property timezone | Mỗi cơ sở có múi giờ riêng, không cứng UTC+07 |
| Rate plan gán cố định | Reservation lưu `rate_plan_id` Snapshot giá tại booking |
| Phiếu điều chỉnh kế toán | Sửa/hủy phụ thu đã ghi, full audit trail |
| Night audit thực tế | Khóa sổ hằng đêm, room charge phân bổ theo đêm |
| Thuế & khuyến mãi | VAT, service charge, ngày lễ, giảm giá, OTA commission |
| Hóa đơn điện tử | Tích hợp cổng hóa đơn điện tử (API stub) |
| RBAC chi tiết | Phân quyền cấp từng hành động (not just role) |
| PII & Compliance | Mã hóa PII, GDPR/PDPA/Nguyên án bảo vệ dữ liệu cá nhân |
| E2E test automation | Playwright + API + SQL, chạy được trên Windows |

## 2. CHI TIẾT TÍNH NĂNG V5

### 2.1. Rate Plan gán cố định vào Reservation (V5-RP-01)

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `reservation.rate_plan_id` | INT NULL | FK đến `rate_plans.id`, snapshot tại booking |
| `reservation.rate_plan_snapshot` | JSON | Lưu mã, tên, mode, base_price, extra_hour_price lúc booking |
| `rate_plans.property_id` | INT | Có FK, unique (property_id, code) |

**Nghiệp vụ:**
- Khi tạo booking, chọn rate plan → lưu ID + snapshot giá
- Khi check-in, giá phòng dùng snapshot, không bị ảnh hưởng khi đổi bảng giá sau này
- Cho phép override giá theo booking (giá riêng khách verein)

**API mới:**
- `GET /api/reservations/{id}/rate-plan` — xem rate plan của booking
- `PATCH /api/reservations/{id}/rate-plan` — đổi rate plan (chỉ trước check-in, quản lý)

### 2.2. Phiếu điều chỉnh kế toán (V5-AC-01)

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `adjustment_vouchers.id` | INT | PK |
| `adjustment_vouchers.code` | NVARCHAR(30) | Mã chứng từ, tự sinh |
| `adjustment_vouchers.reservation_id` | INT NULL | FK đến reservations |
| `adjustment_vouchers.original_charge_id` | INT NULL | FK đến folio_charges (phụ thu gốc) |
| `adjustment_vouchers.kind` | NVARCHAR(20) | `CORRECTION`, `REVERSAL`, `REFUND` |
| `adjustment_vouchers.amount` | DECIMAL(18,2) | Số tiền điều chỉnh (âm = giảm) |
| `adjustment_vouchers.reason` | NVARCHAR(500) | Lý do điều chỉnh |
| `adjustment_vouchers.approved_by` | INT NULL | User duyệt (manager+) |
| `adjustment_vouchers.approved_at_utc` | DATETIME2 NULL | Thời gian duyệt |
| `adjustment_vouchers.created_by` | INT | User tạo |
| `adjustment_vouchers.created_at_utc` | DATETIME2 | Thời gian tạo |
| `adjustment_vouchers.status` | NVARCHAR(20) | `PENDING`, `APPROVED`, `REJECTED`, `POSTED` |

**Nghiệp vụ:**
- Chỉ quản lý (CHAIN_ADMIN, PROPERTY_MANAGER) có thể tạo/duyệt
- `REVERSAL` ghi âm vào folio (trừ tiền)
- `REFUND` tạo payment âm (hoàn tiền khách)
- Mỗi điều chỉnh ghi `AuditEvent`
- Không được điều chỉnh charge đã checkout (trừ khi manager override)

**API mới:**
- `POST /api/adjustment-vouchers` — tạo phiếu điều chỉnh
- `POST /api/adjustment-vouchers/{id}/approve` — duyệt
- `POST /api/adjustment-vouchers/{id}/post` — ghi vào folio (khi APPROVED)
- `GET /api/adjustment-vouchers?propertyId=&status=` — danh sách
### 2.5. Night Audit Thực Tế (V5-NA-01)

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `night_audit_logs.id` | INT | PK |
| `night_audit_logs.property_id` | INT | FK |
| `night_audit_logs.business_date` | DATE | Ngày kinh doanh |
| `night_audit_logs.status` | NVARCHAR(20) | `PENDING`, `LOCKED`, `REOPENED` |
| `night_audit_logs.room_charges` | DECIMAL(18,2) | Tổng charge phòng hôm đó |
| `night_audit_logs.service_charges` | DECIMAL(18,2) | Tổng charge dịch vụ |
| `night_audit_logs.total_payments` | DECIMAL(18,2) | Tổng thanh toán |
| `night_audit_logs.locked_at_utc` | DATETIME2 NULL | Thời gian khóa |
| `night_audit_logs.locked_by` | INT NULL | User khóa |
| `night_audit_logs.reopened_at_utc` | DATETIME2 NULL | Thời gian mở lại |
| `night_audit_logs.reopened_by` | INT NULL | User mở lại |
| `night_audit_logs.reopen_reason` | NVARCHAR(500) | Lý do mở lại |

**Nghiệp vụ:**
- Chạy tự động mỗi ngày (hoặc thủ động qua API)
- Khi `LOCKED`: không cho sửa charge, payment, checkout của ngày đó
- Phân bổ room charge theo từng đêm (không phải theo ngày ghi charge)
- Tự động tạo `daily_closes` snapshot

**API mới:**
- `POST /api/night-audit/run?propertyId=&businessDate=` — chạy night audit
- `POST /api/night-audit/{id}/lock` — khóa sổ ngày
- `POST /api/night-audit/{id}/reopen` — mở lại sổ (CHAIN_ADMIN, có lý do)
- `GET /api/night-audit/logs?propertyId=&from=&to=` — lịch sử night audit
### 2.6. Hóa đơn điện tử (V5-EI-01)

| Trường | Kiểu | Mô tả |
|--------|------|-------|
| `e_invoices.id` | INT | PK |
| `e_invoices.reservation_id` | INT | FK |
| `e_invoices.invoice_code` | NVARCHAR(50) | Mã hóa đơn (từ cổng EI) |
| `e_invoices.invoice_number` | NVARCHAR(50) | Số hóa đơn |
| `e_invoices.status` | NVARCHAR(20) | `DRAFT`, `SENT`, `ISSUED`, `FAILED`, `CANCELLED` |
| `e_invoices.total_amount` | DECIMAL(18,2) | Tổng tiền |
| `e_invoices.tax_amount` | DECIMAL(18,2) | Tiền thuế |
| `e_invoices.request_payload` | JSON | Gửi đi |
| `e_invoices.response_payload` | JSON | Nhận lại |
| `e_invoices.sent_at_utc` | DATETIME2 NULL | Thời gian gửi |
| `e_invoices.issued_at_utc` | DATETIME2 NULL | Thời gian cổng cấp |

**Nghiệp vụ:**
- Tự động tạo hóa đơn khi checkout (nếu bật cài đặt)
- Gọi API cổng hóa đơn điện tử (stub, có thể config URL)
- Lưu yêu cầu/phản hồi để đối soát
- Cho phép hủy hóa đơn (cần lý do, manager duyệt)

**API mới:**
- `POST /api/e-invoices` — tạo hóa đơn từ reservation/checkout
- `POST /api/e-invoices/{id}/send` — gửi lên cổng EI
- `GET /api/e-invoices?propertyId=&status=` — danh sách
- `POST /api/e-invoices/{id}/cancel` — hủy hóa đơn

### 2.7. RBAC Chi Tiết (V5-RBAC-01)

| Hành động | Vai trò cho phép | Ghi chú |
|-----------|------------------|---------|
| `pricing.quote` | FRONT_DESK, SALES, MANAGER, CHAIN_ADMIN | Chỉ đọc, không ghi CSDL |
| `pricing.adjustment.create` | MANAGER, CHAIN_ADMIN | Tạo phiếu điều chỉnh |
| `pricing.adjustment.approve` | MANAGER, CHAIN_ADMIN | Duyệt phiếu điều chỉnh |
| `night-audit.run` | MANAGER, CHAIN_ADMIN, ACCOUNTING | Chạy night audit |
| `night-audit.lock` | MANAGER, CHAIN_ADMIN | Khóa sổ |
| `night-audit.reopen` | CHAIN_ADMIN | Mở lại sổ |
| `tax-rules.manage` | MANAGER, CHAIN_ADMIN, ACCOUNTING | Quản lý quy tắc thuế |
| `e-invoices.send` | MANAGER, CHAIN_ADMIN | Gửi hóa đơn điện tử |
| `property.timezone.edit` | CHAIN_ADMIN | Đổi múi giờ cơ sở |
| `reservation.rate-plan.change` | MANAGER, CHAIN_ADMIN | Đổi rate plan booking |

**Mã mới:**
- `action_permissions` table: mapping role → action → property scope
- Middleware kiểm tra permission trước mỗi endpoint

### 2.8. PII & Compliance (V5-PI-01)

| Yêu cầu | Triển khai |
|---------|------------|
| Mã hóa PII (full_name, phone, identity_last4) | AES-256 tại application layer, key từ Key Vault / env variable |
| Không lưu số CMND/CCCD đầy đủ | Chỉ lưu 4 ký tự cuối (identity_last4) |
| GDPR/PDPA: quyền xóa dữ liệu | API `DELETE /api/guests/{id}/anonymize` — anonymize PII, giữ reservation cho kế toán |
| Audit trail không được sửa/xóa | Audit events chỉ INSERT, không UPDATE/DELETE |
### 2.9. API Tổng hợp V5

| Phương thức | Đường dẫn | Quyền | Mô tả |
|-------------|-----------|-------|-------|
| POST | `/api/pricing/quote` | User có scope | Báo giá, không ghi CSDL |
| POST | `/api/stays/{stayId}/pricing-adjustments` | MANAGER+ | Ghi phụ thu, khóa reservation |
| GET | `/api/night-audit/precheck` | User có scope | Kiểm tra trước chốt |
| POST | `/api/night-audit/run?propertyId=&businessDate=` | MANAGER+ | Chạy night audit |
| POST | `/api/night-audit/{id}/lock` | MANAGER+ | Khóa sổ ngày |
| POST | `/api/night-audit/{id}/reopen` | CHAIN_ADMIN | Mở lại sổ |
| POST | `/api/adjustment-vouchers` | MANAGER+ | Tạo phiếu điều chỉnh |
| POST | `/api/adjustment-vouchers/{id}/approve` | MANAGER+ | Duyệt phiếu |
| POST | `/api/adjustment-vouchers/{id}/post` | MANAGER+ | Ghi vào folio |
| GET | `/api/adjustment-vouchers?propertyId=&status=` | User có scope | Danh sách phiếu |
| GET | `/api/tax-rules?propertyId=` | User có scope | Danh sách quy tắc thuế |
| POST | `/api/tax-rules` | MANAGER+ | Tạo quy tắc thuế |
| GET | `/api/holiday-rates?propertyId=&from=&to=` | User có scope | Danh sách ngày lễ |
| POST | `/api/holiday-rates` | MANAGER+ | Tạo ngày lễ |
| POST | `/api/e-invoices` | MANAGER+ | Tạo hóa đơn điện tử |
| POST | `/api/e-invoices/{id}/send` | MANAGER+ | Gửi cổng EI |
| GET | `/api/e-invoices?propertyId=&status=` | User có scope | Danh sách hóa đơn |
| POST | `/api/e-invoices/{id}/cancel` | MANAGER+ | Hủy hóa đơn |
| PATCH | `/api/properties/{id}/timezone` | CHAIN_ADMIN | Đổi múi giờ |
| PATCH | `/api/reservations/{id}/rate-plan` | MANAGER+ | Đổi rate plan booking
| Data retention | Configurable (mặc định 7 năm cho hồ sơ khách, 10 năm cho kế toán)
## 3. KẾ HOẢN TEST V5

### 3.1. Static Contract Tests (Python)

| ID | Mô tả | File |
|----|-------|------|
| V5-01 | Program.cs có `MapAdjustmentV4()` | `test_v5_static_contracts.py` |
| V5-02 | Endpoint `/api/adjustment-vouchers` | |
| V5-03 | Endpoint `/api/night-audit/run` | |
| V5-04 | Endpoint `/api/night-audit/{id}/lock` | |
| V5-05 | Endpoint `/api/night-audit/{id}/reopen` | |
| V5-06 | Endpoint `/api/tax-rules` | |
| V5-07 | Endpoint `/api/holiday-rates` | |
| V5-08 | Endpoint `/api/e-invoices` | |
| V5-09 | Endpoint `/api/e-invoices/{id}/send` | |
| V5-10 | Endpoint `/api/e-invoices/{id}/cancel` | |
| V5-11 | Endpoint `/api/properties/{id}/timezone` | |
| V5-12 | Endpoint `/api/reservations/{id}/rate-plan` | |
| V5-13 | Angular menu `adjustmentVouchers`, `nightAudit`, `taxRules`, `holidayRates`, `eInvoices` | |
| V5-14 | Angular gọi API mới | |
| V5-15 | RBAC permission checks trong DepartmentAccess | |

### 3.2. C# Unit Tests (Pricing & Tax)

| ID | Mô tả | Kết quả mong đợi |
|----|-------|----------------|
| V5-TC-01 | Tính VAT 10% trên charge 1,000,000 | 100,000 |
| V5-TC-02 | Tính service charge 5% | 50,000 |
| V5-TC-03 | Ngày lễ phụ thu 20% | 130,000 (cho 650,000) |
| V5-TC-04 | Khuyến mãi 15% trên 1,000,000 | 850,000 |
| V5-TC-05 | OTA commission 15% trên 1,000,000 | 150,000 |
| V5-TC-06 | Conversion timezone UTC+07 sang UTC | Đúng giờ |
| V5-TC-07 | Night audit lock không cho sửa charge | 409 |
| V5-TC-08 | Adjustment reversal giảm folio balance | Balance giảm đúng |
| V5-TC-09 | Duplicate adjustment cùng kind → 409 | 409 |
| V5-TC-10 | E-invoice tạo từ checkout | Status = DRAFT |

### 3.3. API E2E Tests (Python + requests)

| ID | Kịch bản |
|----|----------|
| V5-E2E-01 | Login manager → tạo adjustment voucher → approve → post → kiểm tra folio balance |
| V5-E2E-02 | Login reception → gọi pricing/quote → kiểm tra không ghi CSDL |
| V5-E2E-03 | Login manager → gọi night-audit/precheck → kiểm tra warnings |
| V5-E2E-04 | Login manager → chạy night-audit/run → kiểm tra status |
| V5-E2E-05 | Login manager → lock night audit → thử sửa charge → 409 |
| V5-E2E-06 | Login chain_admin → tạo tax rule VAT 10% → kiểm tra |
| V5-E2E-07 | Login manager → tạo holiday rate → kiểm tra phụ thu ngày lễ |
| V5-E2E-08 | Login manager → tạo e-invoice từ reservation → gửi → kiểm tra status |
| V5-E2E-09 | Login chain_admin → đổi timezone property → kiểm tra API |
### 2.9. API Tổng hợp V5

| Phương thức | Đường dẫn | Quyền | Mô tả |
|-------------|-----------|-------|-------|
| POST | `/api/pricing/quote` | User có scope | Báo giá, không ghi CSDL |
| POST | `/api/stays/{stayId}/pricing-adjustments` | MANAGER+ | Ghi phụ thu, khóa reservation |
| GET | `/api/night-audit/precheck` | User có scope | Kiểm tra trước chốt |
| POST | `/api/night-audit/run?propertyId=&businessDate=` | MANAGER+ | Chạy night audit |
| POST | `/api/night-audit/{id}/lock` | MANAGER+ | Khóa sổ ngày |
| POST | `/api/night-audit/{id}/reopen` | CHAIN_ADMIN | Mở lại sổ |
| POST | `/api/adjustment-vouchers` | MANAGER+ | Tạo phiếu điều chỉnh |
| POST | `/api/adjustment-vouchers/{id}/approve` | MANAGER+ | Duyệt phiếu |
| POST | `/api/adjustment-vouchers/{id}/post` | MANAGER+ | Ghi vào folio |
| GET | `/api/adjustment-vouchers?propertyId=&status=` | User có scope | Danh sách phiếu |
| GET | `/api/tax-rules?propertyId=` | User có scope | Danh sách quy tắc thuế |
| POST | `/api/tax-rules` | MANAGER+ | Tạo quy tắc thuế |
| GET | `/api/holiday-rates?propertyId=&from=&to=` | User có scope | Danh sách ngày lễ |
| POST | `/api/holiday-rates` | MANAGER+ | Tạo ngày lễ |
| POST | `/api/e-invoices` | MANAGER+ | Tạo hóa đơn điện tử |
| POST | `/api/e-invoices/{id}/send` | MANAGER+ | Gửi cổng EI |
| GET | `/api/e-invoices?propertyId=&status=` | User có scope | Danh sách hóa đơn |
| POST | `/api/e-invoices/{id}/cancel` | MANAGER+ | Hủy hóa đơn |
| PATCH | `/api/properties/{id}/timezone` | CHAIN_ADMIN | Đổi múi giờ |
| PATCH | `/api/reservations/{id}/rate-plan` | MANAGER+ | Đổi rate plan booking
| V5-E2E-10 | Login reception → đổi rate-plan trước check-in → kiểm tra |

### 3.4. Angular E2E (Playwright)

| ID | Kịch bản |
|----|----------|
| V5-UI-01 | Đăng nhập manager → vào Báo giá & phụ thu → tính giá thành công |
| V5-UI-02 | Đăng nhập reception → vào Báo giá & phụ thu → không thể POST phụ thu (403) |
| V5-UI-03 | Đăng nhập manager → vào Snapshot chốt ngày → Kiểm tra trước chốt |
| V5-UI-04 | Đăng nhập manager → vào Phiếu điều chỉnh → tạo và duyệt phiếu |
| V5-UI-05 | Đăng nhập chain_admin → vào Cơ sở → đổi múi giờ |
| V5-UI-06 | Đăng nhập manager → vào Thuế/Ngày lễ → tạo quy tắc mới |

---

## 4. NGHIỆM THU V5

| Gate | Điều kiện |
|------|-----------|
| G1 | `dotnet build -c Release` PASS (không warning NU1902/NU1903) |
| G2 | `npm run build` Angular production PASS |
| G3 | `npm run test:types` TypeScript typecheck PASS |
| G4 | Static contracts V5 15/15 PASS |
| G5 | C# unit tests V5 10/10 PASS |
| G6 | API E2E tests 10/10 PASS (trên demo DB) |
| G7 | Angular E2E 6/6 PASS (Playwright) |
| G8 | Backup/restore demo DB thành công |
| G9 | Load test 50 concurrent users, response < 2s |
| G10 | Penetration test không có lỗ hổng OWASP Top 10 |

---

## 5. FILE CẦN TẠO/SỬA

| File | Mô tả |
|------|-------|
| `docs/V5_TEST_PLAN.md` | Kế hoạch test chi tiết (tương tự section 3) |
| `qa/test_v5_static_contracts.py` | Test static contract V5 |
| `qa/v5_unit_tests/Program.cs` | C# unit tests V5 |
| `qa/v5_api_e2e.py` | API E2E test Python |
| `qa/v5_angular_e2e.py` | Angular E2E test Playwright |
| `qa/11_v5_acceptance_windows.ps1` | PowerShell acceptance gate V5 |
| `backend/HotelPms.Api/AdjustmentV4.cs` | Adjustment voucher endpoints |
| `backend/HotelPms.Api/TaxV4.cs` | Tax & holiday endpoints |
| `backend/HotelPms.Api/EInvoiceV4.cs` | E-invoice endpoints |
| `backend/HotelPms.Api/TimezoneV4.cs` | Timezone & rate plan endpoints |
| `backend/HotelPms.Api/AdvancedModels.cs` | Thêm entities: AdjustmentVoucher, TaxRule, HolidayRate, EInvoice, NightAuditLog, ActionPermission |
| `backend/HotelPms.Api/Models.cs` | Thêm DbSet mới |
| `frontend/src/app/app.component.ts` | Thêm menu V5 |
| `frontend/src/app/advanced.component.ts` | Thêm logic V5 |
| `frontend/src/app/advanced.component.html` | Thêm UI V5 |

---

## 6. LỢI ÍCH KHI SANG V5

- ✅ Dữ liệu khách sạn an toàn, tuân thủ GDPR/PDPA
- ✅ Doanh thu được ghi sổ đúng theo đêm (night audit)
- ✅ Có piena audit trail cho mọi điều chỉnh tài chính
- ✅ Hỗ trợ nhiều múi giờ, nhiều quốc gia
- ✅ Hóa đơn điện tử tuân thủ pháp luật Việt Nam
- ✅ Có thể triển khai production với confianza

---

**Kết luận:** V4 đã có cơ bản (báo giá, phụ thu, kiểm tra trước chốt). V5 cần bổ sung đầy đủ các tính năng tài chính, kế toán, compliance và automation test để sẵn sàng production.