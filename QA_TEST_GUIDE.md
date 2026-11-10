# 🧪 HƯỚNG DẪN KIỂM THỬ LOTUS PMS V3 VỚI DỮ LIỆU LỚN

## 1. Tổng quan

Bạn đã có sẵn mã nguồn đầy đủ của **LOTUS PMS V3** tại:
`c:\Users\Admin\Downloads\LOTUS_PMS_V3_QA_SAFETY_FULLSTACK\HotelChainPMS`

Website trên Vercel (https://lotus-pms-test.vercel.app/) chỉ là **trang landing đơn giản**, không phải ứng dụng PMS đầy đủ. Để kiểm thử phần mềm thật, bạn cần chạy cục bộ:

| Thành phần | Cổng | Mục đích |
|------------|------|----------|
| SQL Server (Docker) | 14333 | Database |
| Backend ASP.NET Core | 5080 | API + Swagger |
| Frontend Angular | 4200 | Giao diện người dùng |

## 2. Chuẩn bị môi trường

### Yêu cầu cài đặt
- Docker Desktop (để chạy SQL Server 2022)
- .NET SDK 10.x (`dotnet --version`)
- Node.js 22 LTS + npm (`node -v`)
- DBeaver Community (để xem/chỉnh dữ liệu SQL)

### Bước 1: Khởi động SQL Server
```powershell
cd c:\Users\Admin\Downloads\LOTUS_PMS_V3_QA_SAFETY_FULLSTACK\HotelChainPMS
.\01_START_DATABASE.bat
```
Chờ container sẵn sàng: `docker compose ps`

### Bước 2: Khởi động Backend
```powershell
.\02_START_BACKEND.bat
```
Backend chạy tại: `http://localhost:5080`
Swagger API: `http://localhost:5080/swagger`
Health check: `http://localhost:5080/api/health`

### Bước 3: Khởi động Frontend (Angular)
```powershell
.\03_START_ANGULAR.bat
```
Frontend chạy tại: `http://localhost:4200`
## 3. Tạo dữ liệu test lớn

### Bước A: Chèn dữ liệu test vào SQL Server

Mở **DBeaver**, kết nối đến SQL Server:
- Host: `localhost`
- Port: `14333`
- Database: `HotelChainPms`
- Username: `sa`
- Password: `YourStrong!Passw0rd`

Mở file `sql/99-seed-test-data.sql` trong DBeaver, chọn database `HotelChainPms`, bật **Alt+X** để chạy toàn bộ script (vì có lệnh `GO`).

Script này sẽ chèn:
- ✅ 2 cơ sở khách sạn mới (HCMC-SIGNATURE, NHA-TRAM)
- ✅ 6 hạng phòng mới
- ✅ 50 phòng mới (32 phòng HCMC + 18 phòng Nha Trang)
- ✅ 50 khách hàng test
- ✅ 10 đại lý mới
- ✅ 20 dịch vụ/minibar mới
- ✅ 10 đặt phòng (mix trạng thái: CHECKED_IN, CONFIRMED, CHECKED_OUT, CANCELLED)
- ✅ 2 stays (check-in)
- ✅ Folio charges (tiền phòng + minibar)
- ✅ Payments (có idempotency key)
- ✅ Cash shifts (mã/đã đóng)
- ✅ Cash vouchers (thu/chi)
- ✅ Debt receivables (công nợ)
- ✅ Guest assets (tài sản khách gửi)
- ✅ Stock movements (nhập xuất kho)
- ✅ Rate plans (bảng giá V2)
- ✅ Buildings (tòa nhà)
- ✅ Waitlist entries (hàng đợi)
- ✅ Daily closes (snapshot ngày)
- ✅ Suppliers (nhà cung cấp)
- ✅ Retail sales (bán hàng lẻ)

### Bước B: Tạo thêm người dùng test qua API

Sau khi Backend đang chạy, mở PowerShell và chạy:

```powershell
cd c:\Users\Admin\Downloads\LOTUS_PMS_V3_QA_SAFETY_FULLSTACK\HotelChainPMS
powershell -ExecutionPolicy Bypass -File qa/99-create-test-users.ps1
```

Tài khoản test sẽ được tạo (mật khẩu tất cả: `Demo@12345`):

| Email | Vai trò | Cơ sở |
|-------|---------|-------|
| admin@pms.local | CHAIN_ADMIN | Toàn chuỗi |
| manager@pms.local | PROPERTY_MANAGER | Hà Nội |
| reception@pms.local | FRONT_DESK_AGENT | Hà Nội |
| frontdesk.hcmc@pms.local | FRONT_DESK_AGENT | HCMC |
| manager.hcmc@pms.local | PROPERTY_MANAGER | HCMC |
| accounting.hcmc@pms.local | ACCOUNTING_AGENT | HCMC |
| housekeeping.hcmc@pms.local | HOUSEKEEPING_AGENT | HCMC |
| maintenance.hcmc@pms.local | MAINTENANCE_AGENT | HCMC |
| frontdesk.dn@pms.local | FRONT_DESK_AGENT | Đà Nẵng |
| manager.dn@pms.local | PROPERTY_MANAGER | Đà Nẵng |
| accounting.dn@pms.local | ACCOUNTING_AGENT | Đà Nẵng |
| frontdesk.nt@pms.local | FRONT_DESK_AGENT | Nha Trang |
| manager.nt@pms.local | PROPERTY_MANAGER | Nha Trang |
| staff.chain@pms.local | CHAIN_ADMIN | Toàn chuỗi |

### Bước C: Kiểm tra dữ liệu đã chèn

Mở file `sql/99-verify-test-data.sql` trong DBeaver, chạy toàn bộ để xem báo cáo dữ liệu theo từng bảng.
## 4. Các kịch bản kiểm thử đề xuất

### A. Lễ tân (FRONT_DESK_AGENT) — đăng nhập với reception@pms.local

1. **Sơ đồ phòng**: Xem trạng thái tất cả phòng (INSPECTED, DIRTY, OCCUPIED, VACANT, MAINTENANCE)
2. **Đặt phòng**: Tạo đặt phòng mới cho khách đã có trong danh sách 50 khách
3. **Check-in**: Mở folio cho đặt phòng CONFIRMED, thực hiện check-in
4. **Thêm dịch vụ**: Thêm minibar (nước suối, nước ngọt) vào folio của khách đang ở
5. **Thanh toán**: Thu tiền phòng/tiền dịch vụ (CASH/TRANSFER/CARD)
6. **Check-out**: Trả phòng, nếu còn nợ sẽ tạo công nợ (DebtReceivable)
7. **Đổi phòng/Gia hạn**: Thao tác trên stay đang ở
8. **Tài sản khách**: Ghi nhận/trả vali, mũ bảo tay
9. **Hàng đợi**: Tạo và gán phòng cho khách chờ

### B. Quản lý khách sạn (PROPERTY_MANAGER) — đăng nhập với manager@pms.local

1. **Quản lý người dùng**: Xem và tạo nhân viên mới
2. **Quản lý phòng & hạng phòng**: Xem danh sách, chỉnh sửa trạng thái
3. **Bảng giá giờ/ngày/đêm**: Xem và tạo rate plans (OVERNIGHT, DAY_USE, HOURLY)
4. **Tòa nhà**: Quản lý multiple tòa, gán phòng cho tòa
5. **Báo cáo doanh thu**: Xem báo cáo tổng hợp theo khoảng ngày
6. **Đối soát ca**: Xem chênh lệch tiền mặt của các ca thu ngân
7. **Công nợ**: Xem danh sách công nợ chưa thu, thu nợ từng lần

### C. Kế toán (ACCOUNTING_AGENT) — đăng nhập với accounting.hcmc@pms.local

1. **Phiếu thu/chi**: Tạo phiếu thu chi ngoài folio
2. **Báo cáo chuỗi**: Xem doanh thu tổng hợp nhiều cơ sở
3. **Hiệu quả đại lý**: Xem commission của Booking.com, Agoda...
4. **Báo cáo tồn kho**: Xem tồn minibar theo từng dịch vụ
5. **Chốt ngày (Daily Close)**: Tạo snapshot doanh thu hằng ngày

### D. Buồng phòng (HOUSEKEEPING_AGENT)

1. Cập nhật trạng thái phòng: INSPECTED → DIRTY → INSPECTED
2. Xem danh sách phòng cần dọn

### E. Bảo trì (MAINTENANCE_AGENT)

1. Khóa/mở phòng bảo trì (Out of Order)
2. Xem danh sách phòng đang bảo trì

### F. Quản trị toàn chuỗi (CHAIN_ADMIN) — đăng nhập với admin@pms.local

1. Xem tất cả cơ sở khách sạn (Hà Nội, Đà Nẵng, HCMC, Nha Trang)
2. Báo cáo chuỗi tổng hợp doanh thu tất cả cơ sở
3. Quản lý người dùng trên toàn hệ thống
## 5. Kiểm thử API qua Swagger

Truy cập `http://localhost:5080/swagger`, đăng nhập với JWT token để test các endpoint:

### Endpoint chính
| Phương thức | Đường dẫn | Mô tả |
|-------------|-----------|-------|
| POST | `/api/auth/login` | Đăng nhập, nhận token |
| GET | `/api/properties` | Danh sách cơ sở |
| GET | `/api/rooms?propertyId=X` | Danh sách phòng |
| GET | `/api/reservations?propertyId=X` | Danh sách đặt phòng |
| POST | `/api/reservations` | Tạo đặt phòng mới |
| GET | `/api/stays?propertyId=X` | Danh sách khách đang ở |
| POST | `/api/stays/{id}/services` | Thêm dịch vụ vào folio |
| POST | `/api/stays/{id}/payments` | Thanh toán folio |
| POST | `/api/stays/{id}/checkout` | Check-out |
| POST | `/api/shifts/open` | Mở ca thu ngân |
| POST | `/api/shifts/{id}/close` | Đóng ca thu ngân |
| POST | `/api/vouchers` | Tạo phiếu thu/chi |
| GET | `/api/reports/summary?propertyId=X&from=...&to=...` | Báo cáo doanh thu |
| GET | `/api/reports/chain?from=...&to=...` | Báo cáo chuỗi |
| GET | `/api/debts?propertyId=X` | Danh sách công nợ |
| GET | `/api/audit?propertyId=X` | Nhật ký thao tác |

## 6. Lưu ý quan trọng

1. **Mật khẩu mặc định**: Tất cả tài khoản test đều có mật khẩu `Demo@12345`
2. **Dữ liệu chỉ dùng để test**: Không sử dụng dữ liệu thật, không triển khai trên internet công khai
3. **V3 là QA Safety Patch**: Một số tính năng đã kiểm tra tĩnh PASS nhưng chưa chạy runtime thực tế (xem `qa/V3_NGHIEM_THU.md`)
4. **Database schema**: Sử dụng snake_case trong SQL, PascalCase trong C#, camelCase trong JSON
5. **Idempotency**: Khi thanh toán qua API, phải gửi `idempotencyKey` (UUID) để tránh ghi trùng giao dịch

## 7. File đã tạo mới

| File | Mục đích |
|------|----------|
| `sql/99-seed-test-data.sql` | Chèn dữ liệu test lớn (50 khách, 50+ phòng, 10 đặt phòng, thanh toán, kho, công nợ...) |
| `sql/99-verify-test-data.sql` | Kiểm tra dữ liệu sau khi chèn |
| `qa/99-create-test-users.ps1` | Tạo thêm 14 người dùng test qua API |
| `QA_TEST_GUIDE.md` | Hướng dẫn này |

---

**Chúc bạn kiểm thử phần mềm thành công!** Nếu gặp lỗi, hãy gửi log `dotnet build` hoặc `npm run build` để được hỗ trợ.
## 8. V4 — TÍNH TOÁN & PHÁT TRIỂN (SO VÀ TỰ ĐỘNG HÓA)

Bạn đã có sẵn V4_Change.md với 13 ca kiểm tra công thức giá + 3 endpoint mới. Điều kiện để V4 sẵn sàng production:

### Gating V4 (cần 10/10 PASS)
- [ ] **C# Unit Tests**: 13/13 CA: `qa/v4_pricing_tests/Program.cs` → `dotnet run -c Release`
- [ ] **Static Contracts**: Python: `qa/test_v4_static_contracts.py` → 16/16 PASS
- [ ] **API Smoke**: `qa/10_v4_pricing_api_smoke.ps1` (Cần backend + SQL Server chạy)
- [ ] **Frontend V4**: `pricingQuote` view đã có trong `advanced.component.ts` + `advanced.component.html`
- [ ] **Angular Build**: `npm run build` PASS
- [ ] **Backend Build**: `dotnet build -c Release` PASS (không warning NU1902/NU1903)

### Những vấn đề cần CẢI THIỆN trước khi production
1. **URLSession không đọc timezone property**: frontend dùng UTC+07 cố định (mobile.timezone). Cần đọc `properties.timezone`
2. **Rate plan chưa gán cố định**: `reservations.rate_plan_id` chưa có; giá phòng thay đổi khi đổi bảng giá
3. **PHI không ghi CRM**: `pricing-adjustments` lưu vào folio nhưng không traceability
4. **Chưa API anguler**: frontend chưa có form nhập `ratePlanId`, `actualCheckInLocal`
5. **Chưa UI cho Holiday rates, Tax rules**: `tax-rules`, `holiday-rates` đơn giản

### SAI CHƯƠNG LỚP VỊ (với form nhưng chưa dùng)
- `AdvancedComponent.advancedViews` đã có `dayclose` nhưng không dùng `night-audit/precheck`
- `form.businessDate` dùng `localDate` (mobile.timezone), phải dùng property timezone

### Lộ trình V4 → V5
- V5-01: Thêm tracking `reservation.rate_plan_id`
- V5-02: Thêm `AdjustmentVoucher` entity
- V5-03: Thêm `TaxRule`, `HolidayRate` entities
- V5-04: Thêm `EInvoice` entity
- V5-05: Thêm `NightAuditLog` entity
- V5-06: API endpoints for all V5 features
- V5-07: Angular UI for new endpoints
- V5-08: RBAC, PII, timezone
- V5-09: Test suite -> PASS

---

## 9. V5 FEATURE SPECIFICATION (CHI TIẾT TÍNH NĂNG)

**Coverage V4 → V5:**

| V4 đã có (PASS) | V5 cần bổ sung (TODO) | File cần sửa |
|-----------------|----------------------|--------------|
| `/api/pricing/quote` | Rate plan gán cố định (V5-RP-01) | `AdvancedModels.cs`, `PricingV4.cs`, `app.component.ts` |
| `/api/stays/{stayId}/pricing-adjustments` | Phiếu điều chỉnh kế toán (V5-AC-01) | `AdvancedModels.cs`, `AdjustmentV4.cs`, `advanced.component.ts` |
| `/api/night-audit/precheck` | Night audit thực tế (V5-NA-01) | `NightAuditV4.cs`, `night-audit/run`, `lock`, `reopen` |
| Tính giá qua đêm/giờ/ngày | Thuế, phụ thu, ngày lễ, khuyến mãi (V5-TAX-01) | `TaxV4.cs`, `TaxRule`, `HolidayRate`, `EInvoice` |
| — | Hóa đơn điện tử (V5-EI-01) | `EInvoiceV4.cs`, `EInvoice` |
| — | Multi-property timezone (V5-TZ-01) | `TimezoneV4.cs`, `properties.timezone` |
| — | RBAC chi tiết (V5-RBAC-01) | `ActionPermission`, middleware |
| — | PII & GDPR/PDPA (V5-PI-01) | `Guest.anonymize()`, encryption |

**DOI CHUYỂN CHI TIẾT:**

| Chức năng | Phiên bản hiện tại | Phiên bản mục tiêu |
|-----------|-------------------|-------------------|
| Báo giá | Tính trên `rate_plan` hiện tại + UTC+07 cứng | Tính trên `rate_plan_snapshot` + múi giờ property |
| Phụ thu | Ghi 1 lần + kiểm tra trùng | Ghi nhiều lần + điều chỉnh được (REVERSAL, REFUND) |
| Kiểm tra trước chốt | Chỉ cảnh báo | Khóa sổ hoàn toàn, không sửa được charge |
| Thuế | Chưa có | Tính VAT 10%, service charge |
| Ngày lễ | Chưa có | Phụ thu ngày lễ 20% (configurable) |
| Khuyến mãi | Chưa có | Giảm % trên tổng hoa hồng |
| Hóa đơn | Chưa có | Tự động khi checkout |
| Timezone | UTC+07 cố định | Múi giờ riêng từng cơ sở |

---

## 10. V5 ROUTE MA TRẬN

```
GET    /api/night-audit/precheck?propertyId=&businessDate=
POST   /api/night-audit/run?propertyId=&businessDate=
POST   /api/night-audit/{id}/lock
POST   /api/night-audit/{id}/reopen

POST   /api/adjustment-vouchers
POST   /api/adjustment-vouchers/{id}/approve
POST   /api/adjustment-vouchers/{id}/post
GET    /api/adjustment-vouchers?propertyId=&status=

GET    /api/tax-rules?propertyId=
POST   /api/tax-rules
GET    /api/holiday-rates?propertyId=&from=&to=
POST   /api/holiday-rates

POST   /api/e-invoices
POST   /api/e-invoices/{id}/send
GET    /api/e-invoices?propertyId=&status=
POST   /api/e-invoices/{id}/cancel

PATCH  /api/properties/{id}/timezone
PATCH  /api/reservations/{id}/rate-plan
```
## 8. Đẩy dữ liệu lên Neon (PostgreSQL)

Vì bạn đang chạy trên web (Vercel frontend + Render backend + Neon database), bạn cần đẩy dữ liệu test lên Neon thay vì SQL Server local.

### Bước 1: Lấy connection string Neon

Từ Neon Console (https://console.neon.tech/), lấy connection string của database `neondb`:

```
postgresql://<user>:<password>@<endpoint>-pooler.<region>.aws.neon.tech/neondb?sslmode=require
```

### Bước 2: Chạy script seed trên Neon

**Cách A: Dùng psql (command line)**

```powershell
psql "$NEON_DATABASE_URL" -f sql/99-seed-test-data-postgres.sql
```

**Cách B: Dùng DBeaver**

1. Mở DBeaver, tạo kết nối mới đến PostgreSQL (Neon)
2. Nhập connection string từ trên
3. Mở file `sql/99-seed-test-data-postgres.sql`
4. Chạy toàn bộ script (Ctrl+Enter hoặc Alt+X)

**Cách C: Dùng pgAdmin**

1. Kết nối đến Neon server
2. Mở Query Tool
3. Paste nội dung file `sql/99-seed-test-data-postgres.sql`
4. Chạy (F5)

### Bước 3: Kiểm tra dữ liệu

Chạy file `sql/99-verify-test-data-postgres.sql` trên Neon để xem báo cáo dữ liệu.

### Bước 4: Tạo người dùng test qua API

Sau khi backend trên Render đang chạy, tạo người dùng qua API:

```powershell
$loginBody = @{ email = "admin@pms.local"; password = "Demo@12345" } | ConvertTo-Json
$auth = Invoke-RestMethod "https://lotus-pms-api.onrender.com/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($auth.token)" }
```

Hoặc tạo thủ công qua Swagger tại `https://lotus-pms-api.onrender.com/swagger`

### Danh sách tài khoản test (mật khẩu: Demo@12345)

| Email | Vai trò | Cơ sở |
|-------|---------|-------|
| admin@pms.local | CHAIN_ADMIN | Toàn chuỗi |
| manager@pms.local | PROPERTY_MANAGER | Hà Nội |
| reception@pms.local | FRONT_DESK_AGENT | Hà Nội |
| frontdesk.hcmc@pms.local | FRONT_DESK_AGENT | HCMC |
| manager.hcmc@pms.local | PROPERTY_MANAGER | HCMC |
| accounting.hcmc@pms.local | ACCOUNTING_AGENT | HCMC |
| frontdesk.dn@pms.local | FRONT_DESK_AGENT | Đà Nẵng |
| manager.dn@pms.local | PROPERTY_MANAGER | Đà Nẵng |
| frontdesk.nt@pms.local | FRONT_DESK_AGENT | Nha Trang |
| manager.nt@pms.local | PROPERTY_MANAGER | Nha Trang |

---

## 9. Các kịch bản kiểm thử đề xuất

(Tương tự như phần trước, áp dụng cho backend trên Render + frontend trên Vercel)
