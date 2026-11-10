# LOTUS PMS — ROADMAP PHÁT TRIỂN V5 ĐỂ ĐỦ ĐIỀU KIỆN CHUYỂN V6

**Repository:** `Quan03112003/PMS`  
**Nhánh chuẩn đang rà soát:** `main`  
**Commit đã kiểm tra:** `1e433d46b4266d929675e2882667482ef285efb3`  
**Ngày rà soát:** 06/11/2026  
**Mục đích:** Tài liệu giao việc cho Developer/Tech Lead/QA và làm căn cứ nghiệm thu V5 trước khi mở V6.

---

## 0. Kết luận điều hành

Không nên hiểu V5 là “thêm vài menu mới”, cũng không nên chuyển thẳng sang V6 chỉ vì đã có nhiều màn hình.

- **V4** là nền tảng PMS hiện tại: quản lý khách sạn, phòng, booking, khách lưu trú, folio, thanh toán, ca thu ngân, kho cơ bản, báo cáo cơ bản, hàng đợi/khách đoàn và báo giá/phụ thu.
- **V5** phải hoàn thiện nghiệp vụ vận hành và kiểm soát: giá theo snapshot, giữ phòng/cọc, lịch sử thay đổi, buồng phòng có giao việc/nghiệm thu, kế toán có chứng từ điều chỉnh, Night Audit thật, báo cáo chỉ số chuẩn, phân quyền chi tiết, migration và kiểm thử Production.
- **V6** tập trung mở rộng hệ sinh thái: Hotelink/OTA/Booking Engine, hóa đơn điện tử thực tế, POS/nhà hàng/spa/PBX, CRM/loyalty, revenue management và quản trị chuỗi nâng cao.

**Đề xuất:** Chia V5 thành 4 gói: V5.0 nền tảng kỹ thuật; V5.1 Booking & Front Desk; V5.2 Housekeeping & Inventory; V5.3 Finance, Night Audit & Reporting. Chỉ mở V6 khi tất cả cổng nghiệm thu V5 đạt.

### Trạng thái thực tế cần nhớ

Tại commit nêu trên:

- Đã có source Angular 20 và ASP.NET Core 10.
- Backend có cấu hình chọn `SqlServer` hoặc `PostgreSQL` qua `Database:Provider`; Render blueprint hiện đặt PostgreSQL.
- V4 đã có API/UI hàng đợi và một số API khách đoàn; vì vậy không viết lại các chức năng này. Cần hoàn thiện trải nghiệm wizard/rooming list nếu nghiệp vụ yêu cầu.
- V4 có `POST /api/pricing/quote`, `POST /api/stays/{stayId}/pricing-adjustments` và `GET /api/night-audit/precheck`.
- Night Audit hiện chỉ là precheck/snapshot; **chưa phải quy trình tính doanh thu theo đêm và khóa sổ thật**.
- Chuyển phòng hiện cập nhật `Stay.RoomId` và `Reservation.RoomId`, đánh phòng cũ thành `DIRTY`, ghi audit event; chưa có bảng lịch sử chuyển phòng chuyên biệt với lý do, giá trước/sau và phê duyệt.
- Housekeeping hiện chủ yếu đổi `housekeeping_status`; chưa có task assignment, kiểm tra, nghiệm thu, ảnh minibar và lịch sử kiểm tra đầy đủ.
- `Reservation` chưa có `RatePlanId`/snapshot bảng giá riêng.
- V5 spec mới là Draft; một số phần đặc tả bị thiếu/không nhất quán; test V5 mới chỉ là static contract ban đầu.
- Chưa thấy EF Core migrations, workflow GitHub Actions hay các module V5 như Adjustment Voucher, Tax Rule, Holiday Rate, E-Invoice, Night Audit Log trong source tại commit đã kiểm tra.
- `package-lock.json` hiện có. Không cần tạo lại nếu không đổi dependency.

Tài liệu nguồn trong repo:
- `docs/V5_FEATURE_SPEC.md`
- `docs/V5_TEST_PLAN.md`
- `docs/FEATURE_MATRIX.md`
- `docs/V4_Change.md`
- `docs/ARCHITECTURE.md`
- `docs/TEN_TRUONG_DU_LIEU.md`
- `docs/QC_TEST_CASES.md`
- `render.yaml`
- `backend/HotelPms.Api/Program.cs`
- `backend/HotelPms.Api/Models.cs`
- `backend/HotelPms.Api/AdvancedEndpoints.cs`
- `backend/HotelPms.Api/PricingV4.cs`
- `backend/HotelPms.Api/NightAuditV4.cs`
- `frontend/src/app/app.component.ts`
- `frontend/src/app/advanced.component.ts`

---

# PHẦN A — NGUYÊN TẮC PHÁT TRIỂN BẮT BUỘC

## A1. Không làm lại chức năng V4 đã có

Trước khi thêm module, Developer phải:
1. Đọc API hiện có.
2. Đọc Entity/DbSet và cấu hình EF Core.
3. Đọc component Angular và menu.
4. Kiểm tra test hiện có.
5. Ghi rõ chức năng đang có, phần cần mở rộng, phần tạo mới.

Không tạo endpoint hoặc bảng thứ hai làm trùng chức năng V4.

Ví dụ:
- Hàng đợi đã có `waitlist` API/UI; chỉ bổ sung luồng hết hạn, thông báo, phân công và trạng thái nếu thiếu.
- Khách đoàn đã có API nhóm, occupant, check-in/pay/checkout cơ bản; chỉ làm wizard, rooming list, tách folio và đối soát nếu nghiệp vụ yêu cầu.
- Chuyển phòng đã có API; bổ sung lịch sử và kiểm soát giá, không tạo endpoint đổi phòng thứ hai.
- Báo giá đã có; bổ sung snapshot, thuế, lịch giá và quy tắc phê duyệt.

## A2. Không coi có màn hình là hoàn thành

Một chức năng chỉ được đánh dấu **Done** khi đủ 7 lớp:
1. Đặc tả nghiệp vụ và trạng thái.
2. Database entity, migration, FK, unique/check index.
3. API request/response, validation, phân quyền.
4. Angular UI, loading/empty/error/permission state.
5. Audit log và xử lý giao dịch.
6. Unit + integration + API + UI E2E test.
7. Bằng chứng nghiệm thu, kết quả SQL đối chiếu.

## A3. Kiến trúc dữ liệu và môi trường

Mục tiêu triển khai đang dùng:
- Frontend: Angular trên Vercel.
- Backend: ASP.NET Core 10 trên Render.
- Database: PostgreSQL trên Neon.
- DBeaver: công cụ quản trị/đối chiếu database.

Quyết định kỹ thuật cần chốt:
- Nếu Neon PostgreSQL là database chuẩn của môi trường test/production, PostgreSQL phải là provider chính được kiểm thử và migration.
- SQL Server có thể duy trì làm provider phụ nếu công ty thật sự cần; khi đó mọi migration, index, transaction lock, raw SQL và test phải chạy được trên cả hai provider. Nếu không có nhu cầu, không nên giữ hai provider chỉ để “có vẻ linh hoạt”.
- Không sử dụng cú pháp SQL Server trong migration PostgreSQL.
- Không dùng `EnsureCreated()` để nâng cấp database đã có dữ liệu.
- Production không được tự seed tài khoản, khách, booking hoặc dữ liệu demo khi backend khởi động.

## A4. Quy ước đặt tên

| Lớp | Quy ước | Ví dụ |
|---|---|---|
| Angular/TypeScript | camelCase | `ratePlanId` |
| JSON API | camelCase | `depositDueAtUtc` |
| C# class/property | PascalCase | `DepositDueAtUtc` |
| C# method | PascalCase | `CreateReservationAsync()` |
| PostgreSQL table | snake_case, số nhiều | `reservation_holds` |
| PostgreSQL column | snake_case | `hold_expires_at_utc` |
| API path | kebab-case | `/reservation-holds` |
| Enum/status | UPPER_SNAKE_CASE | `PENDING_INSPECTION` |
| ID | UUID hoặc bigint/int thống nhất | Không đổi kiểu ID tuỳ tiện giữa các bảng |

Tiền dùng `numeric(18,2)`/`decimal(18,2)`, không dùng `float/double`.
Thời điểm hệ thống lưu UTC; ngày kinh doanh tính theo timezone của từng cơ sở.
Ngày ở dùng `date`; thời gian địa phương phải được chuyển đổi rõ ràng với timezone IANA, không cộng cứng 7 giờ trong toàn bộ code.

---

# PHẦN B — V5.0: ỔN ĐỊNH NỀN TẢNG TRƯỚC KHI THÊM NGHIỆP VỤ

## B1. Database migration

**Hiện trạng:** Chưa thấy thư mục EF Core Migrations trong source tại commit rà soát.

Developer cần:
- Tạo migrations theo provider PostgreSQL.
- Tạo baseline cho schema hiện tại; không tạo database trống đè dữ liệu hiện hữu.
- Có migration từ schema V4 → V5, giữ nguyên PK/FK và dữ liệu booking/khách/folio/payment.
- Kiểm thử trên Neon staging branch hoặc project test riêng.
- Có script backup/restore và xác nhận restore thành công.
- Mọi migration phải review SQL sinh ra trước khi chạy production.

Lệnh tham khảo sau khi chuẩn hoá project:
```powershell
dotnet ef migrations add V5_Initial `
  --project .\backend\HotelPms.Api `
  --startup-project .\backend\HotelPms.Api

dotnet ef migrations script `
  --project .\backend\HotelPms.Api `
  --startup-project .\backend\HotelPms.Api `
  --output .\artifacts\v5-migration.sql
```

Không chạy `database update` production tự động trong bước đầu. Chỉ áp dụng sau khi migration script đã được review và test restore.

## B2. Tách DemoSeeder khỏi Production

Hiện `render.yaml` đặt `Database__InitializeForDemo: "true"`. Cấu hình này không phù hợp cho Production.

Yêu cầu:
- `Production`: `Database__InitializeForDemo=false`.
- `Staging/Test`: chỉ seed bằng lệnh/job chủ động, có xác nhận đúng database.
- Seed phải idempotent, dùng code cố định và không tạo lại dữ liệu trùng.
- Không lưu mật khẩu Neon, JWT secret, tài khoản SMTP hoặc API key trong Git.
- Không trả connection string trong log.
- Tài khoản demo phải bị vô hiệu hoá hoặc đổi mật khẩu trước khi đưa dữ liệu thật vào hệ thống.

Việc credential test đã từng xuất hiện trong repo không làm thay đổi ưu tiên nghiệp vụ V5 nếu project thực sự chỉ chứa dữ liệu giả lập; tuy nhiên credential đã public nên vẫn nên thay tại kỳ bảo trì gần nhất và tuyệt đối không dùng lại cho production.

## B3. Sửa tương thích PostgreSQL

Cần kiểm tra toàn bộ:
- Filtered/partial unique index.
- Raw SQL và khóa đồng thời.
- `DateOnly`, `TimeOnly`, `timestamp with time zone`.
- Identity/sequence.
- JSONB mapping.
- Case-sensitive table/column.
- Numeric precision/rounding.
- Transaction isolation và `FOR UPDATE`.

Ví dụ cụ thể trong `Models.cs`:
```csharp
.HasFilter("[idempotency_key] IS NOT NULL")
```
là cú pháp filter kiểu SQL Server; phải cấu hình tương thích theo provider, kiểm tra SQL sinh ra trên PostgreSQL.

Không kết luận chỉ từ compile; phải chạy migration và integration test thật trên PostgreSQL.

## B4. Health check và quan sát hệ thống

Bổ sung:
- `/health/live`: tiến trình API còn sống.
- `/health/ready`: API kết nối được database, schema ở đúng phiên bản và dependency bắt buộc sẵn sàng.
- Structured logging có `correlationId`, `propertyId`, `actorUserId`, endpoint và duration.
- Không log mật khẩu, JWT, số giấy tờ hoặc dữ liệu khách đầy đủ.
- Theo dõi lỗi 5xx, DB connection pool, thời gian truy vấn, CPU/RAM và job bị lỗi.
- Cảnh báo khi backup thất bại hoặc job Night Audit không chạy.
- Trang quản trị xem trạng thái tích hợp và job nền.

## B5. CI/CD

Tạo `.github/workflows/ci.yml` tối thiểu:
- Restore + `dotnet build -c Release`.
- `dotnet test` toàn bộ unit/integration test.
- `npm ci`.
- `npm run test:types`.
- `npm run build`.
- Static security/dependency scan.
- Chặn merge khi test đỏ.
- Vercel Preview cho PR.
- Render deploy sau CI pass.
- Production deploy yêu cầu approval.
- Migration Production là bước riêng, có backup và approval.

**Đầu ra V5.0:** build ổn định, PostgreSQL migration có thể lặp lại, test chạy tự động, production không tự seed demo, có health/log/backup/restore.

---

# PHẦN C — V5.1: BOOKING VÀ LỄ TÂN

## C1. Rate Plan Snapshot tại thời điểm đặt phòng

**Vấn đề V4:** `Reservation` hiện có `NightlyRate`, nhưng chưa gắn `RatePlanId` và snapshot chính sách giá đầy đủ. Đổi bảng giá sau này có thể khiến việc giải thích lịch sử giá không rõ ràng.

### Database đề xuất

Bổ sung vào `reservations`:
| Column | Type | Ý nghĩa |
|---|---|---|
| `rate_plan_id` | bigint/int nullable FK | Bảng giá được chọn |
| `rate_plan_snapshot` | jsonb | Snapshot chính sách tại thời điểm đặt |
| `rate_plan_version` | integer | Version snapshot |
| `price_override_amount` | numeric(18,2) nullable | Giá override nếu được duyệt |
| `price_override_reason` | varchar(500) nullable | Lý do override |
| `price_override_by` | bigint/int nullable | Người duyệt |
| `price_override_at_utc` | timestamptz nullable | Thời điểm duyệt |

Snapshot JSON phải có tối thiểu:
```json
{
  "ratePlanId": 1,
  "code": "NIGHT-STD",
  "name": "Qua đêm tiêu chuẩn",
  "mode": "OVERNIGHT",
  "currency": "VND",
  "basePrice": 650000,
  "includedHours": 24,
  "extraHourPrice": 75000,
  "checkInTime": "14:00",
  "checkOutTime": "12:00",
  "version": 1,
  "capturedAtUtc": "2026-11-06T08:00:00Z"
}
```

Quy tắc:
- Khi tạo booking phải chốt rate plan và snapshot.
- Khi bảng giá được sửa, booking cũ không đổi.
- Chỉ quản lý được đổi rate plan trước check-in; lưu audit old/new snapshot và lý do.
- Override giá cần quyền riêng, lý do bắt buộc, giới hạn theo policy và có lịch sử duyệt.
- Không đọc giá hiện tại để tính lại booking lịch sử nếu snapshot đã tồn tại.

### API đề xuất
- `GET /api/reservations/{id}/rate-plan`
- `PATCH /api/reservations/{id}/rate-plan`
- `POST /api/reservations/{id}/price-override`
- `GET /api/reservations/{id}/price-history`

### Tiêu chí nghiệm thu
- Tạo booking ở giá 650.000.
- Quản lý đổi bảng giá mặc định lên 700.000.
- Booking cũ vẫn giữ snapshot 650.000.
- Booking mới dùng snapshot 700.000.
- Người không có quyền không thể override.
- Mọi thay đổi có actor, thời gian, lý do và before/after.

## C2. Booking Note dạng danh mục

Không chỉ lưu một ô text dài.

### Database đề xuất
- `booking_note_types`: `id`, `property_id` nullable (dùng chung toàn chuỗi nếu null), `code`, `name`, `description`, `is_active`, `sort_order`, `requires_text`.
- `reservation_booking_notes`: `id`, `reservation_id`, `note_type_id`, `note_text`, `created_by`, `created_at_utc`.

Ví dụ danh mục có thể cấu hình:
- Khách VIP.
- Khách công tác.
- Khách gia đình.
- Yêu cầu tầng cao.
- Không hút thuốc.
- Giường phụ.
- Đón sân bay.
- Dị ứng/thực phẩm đặc biệt.
- Kỷ niệm/sinh nhật.

Không hardcode danh mục nếu quản lý cần tuỳ chỉnh theo cơ sở.

### Nghiệp vụ
- Có thể chọn nhiều note.
- Note quan trọng hiển thị ở màn hình lễ tân, buồng phòng và chi tiết booking.
- Note nhạy cảm về sức khoẻ chỉ hiển thị cho role có quyền.
- Lưu lịch sử thêm/sửa/ẩn note; không âm thầm ghi đè.
- Có thể đánh dấu note cần xác nhận khi check-in.

### API
- `GET/POST/PATCH /api/booking-note-types`
- `GET/POST /api/reservations/{id}/booking-notes`
- `DELETE /api/reservations/{id}/booking-notes/{noteId}` (soft delete/audit theo policy)

## C3. Giữ phòng và tự nhả khi hết hạn

### Nghiệp vụ
- Nhân viên tạo booking ở trạng thái chờ giữ phòng.
- Bắt buộc có `holdExpiresAtUtc`, tiền cọc yêu cầu, thời hạn nộp cọc.
- Màn hình hiển thị countdown theo thời gian server.
- Khi khách nộp cọc đủ: chuyển hold sang confirmed.
- Khi hết hạn mà chưa đạt điều kiện: job nền chuyển hold `EXPIRED` và giải phóng tồn phòng.
- Nếu nhân viên gia hạn hold: yêu cầu lý do, quyền phù hợp và log.
- Không để 2 booking cùng giữ một phòng trong khoảng ngày.
- Countdown ở browser chỉ để hiển thị; backend job/DB mới là nguồn quyết định hết hạn.

### Table đề xuất `reservation_holds`
| Column | Type |
|---|---|
| `id` | bigint |
| `reservation_id` | bigint FK unique |
| `status` | varchar(20): `ACTIVE`, `CONFIRMED`, `EXPIRED`, `RELEASED` |
| `hold_expires_at_utc` | timestamptz |
| `deposit_required_amount` | numeric(18,2) |
| `deposit_received_amount` | numeric(18,2) |
| `deposit_due_at_utc` | timestamptz nullable |
| `released_at_utc` | timestamptz nullable |
| `release_reason` | varchar(500) nullable |
| `created_by` | bigint |
| `created_at_utc` | timestamptz |

### API
- `POST /api/reservations/{id}/hold`
- `POST /api/reservation-holds/{id}/extend`
- `POST /api/reservation-holds/{id}/release`
- `GET /api/reservation-holds?propertyId=&status=`
- Job nội bộ `ExpireReservationHolds`.

## C4. Cảnh báo hạn đặt cọc

- Cảnh báo trước hạn 7 ngày, 3 ngày và đến hạn (theo yêu cầu nghiệp vụ A25).
- Cấu hình được mốc nhắc, không hardcode trong source.
- Mỗi mốc chỉ gửi một lần, tránh spam.
- Khi booking đã đủ cọc/hủy/expire: dừng nhắc.
- Cho phép đánh dấu đã liên hệ khách, ghi kết quả và người xử lý.
- V5 nên có in-app notification; email/SMS/Zalo là adapter cấu hình sau.

Bảng đề xuất:
- `notification_rules`
- `notification_events`
- `reservation_followups`

## C5. Chuyển phòng có lịch sử và kiểm soát giá

V4 có API chuyển phòng nhưng chưa có lịch sử nghiệp vụ chuyên biệt.

### Bảng `room_transfer_histories`
- `id`
- `property_id`
- `reservation_id`
- `stay_id`
- `from_room_id`
- `to_room_id`
- `reason_code`
- `reason_text`
- `old_rate_amount`
- `new_rate_amount`
- `price_difference_amount`
- `effective_at_utc`
- `created_by`
- `approved_by`
- `created_at_utc`

### Nghiệp vụ
- Phòng đích phải cùng cơ sở, không OOO, sạch và đã nghiệm thu.
- Kiểm tra booking overlap trong thời gian còn lại.
- Phòng cũ chuyển `DIRTY` sau khi giao phòng.
- Stay hiện tại giữ nguyên ID.
- Không tự đổi giá chỉ vì đổi phòng; nếu thay đổi giá phải có chính sách, lý do và phê duyệt.
- Có thể ghi chênh lệch vào folio dưới một charge riêng.
- Chống gửi lặp bằng idempotency key.
- Không xóa lịch sử chuyển phòng.

### API
- Giữ `POST /api/stays/{stayId}/transfer` hiện có và mở rộng request.
- `GET /api/stays/{stayId}/room-transfer-history`
- `GET /api/reservations/{id}/room-transfer-history`

Request bổ sung:
```json
{
  "newRoomId": 205,
  "reasonCode": "ROOM_UPGRADE",
  "reasonText": "Khách yêu cầu nâng hạng",
  "applyRateChange": false,
  "idempotencyKey": "UUID"
}
```

## C6. Khách đoàn và Rooming List

V4 có nhóm booking/occupants cơ bản, V5 cần hoàn thiện trải nghiệm:
- Tạo group master, nhiều reservation con trong một transaction hợp lý.
- Rooming list import Excel/CSV có preview, validate và báo lỗi theo dòng.
- Gán khách từng phòng, người đại diện, trẻ em, quốc tịch và thông tin đăng ký.
- Check-in đoàn từng phòng hoặc theo lô; lỗi một phòng không làm hỏng toàn bộ đoàn nếu chế độ cho phép.
- Tách/ghép folio theo phòng, người thanh toán hoặc công ty.
- Báo cáo phòng đã gán/chưa gán, khách đã đến/chưa đến.

## C7. Walk-in, thuê giờ/ngày và Front Desk

V4 đã có `BookingMode` và báo giá V4, nhưng check-in/charge hiện còn giới hạn.

V5 cần:
- Một luồng walk-in: tìm phòng → chọn gói giờ/ngày/đêm → nhập khách → xác nhận giá → cọc/thanh toán → check-in.
- Lưu giờ bắt đầu và giờ hết hạn thực tế cho thuê giờ/ngày.
- Tính overtime theo rate plan snapshot.
- Cảnh báo sắp hết giờ.
- Gia hạn theo gói và kiểm tra booking kế tiếp.
- Không ghi trước toàn bộ doanh thu nhiều đêm nếu Night Audit được chọn làm nguồn ghi nhận doanh thu theo ngày; tránh ghi trùng.

## C8. Folio nâng cao / Tách thanh toán

- Một reservation có nhiều người trả tiền.
- Tách charge theo khách, phòng, công ty hoặc nhóm.
- Chuyển một phần charge sang folio khác phải lưu nguồn/đích, người thực hiện và lý do.
- Cho phép thanh toán nhiều phương thức.
- Balance không được âm ngoài nghiệp vụ credit/refund đã duyệt.
- Không sửa/xóa payment gốc; mọi correction là ledger entry mới.

---

# PHẦN D — V5.2: HOUSEKEEPING VÀ INVENTORY

## D1. Housekeeping Task Management

Hiện trạng V4 mới cập nhật `housekeeping_status`. V5 cần quản lý công việc thật.

### Trạng thái phòng đề xuất
- `DIRTY`
- `CLEANING`
- `CLEAN`
- `PENDING_INSPECTION`
- `INSPECTED`
- `OUT_OF_ORDER`
- `OUT_OF_SERVICE`

Không dùng một status duy nhất trộn occupancy, housekeeping và maintenance:
- Occupancy: vacant / reserved / occupied.
- Housekeeping: dirty / cleaning / clean / pending inspection / inspected.
- Maintenance: available / out of order / out of service.

### Table `housekeeping_tasks`
- `id`, `property_id`, `room_id`
- `task_type`: `CHECKOUT_CLEAN`, `STAYOVER_CLEAN`, `DEEP_CLEAN`, `MINIBAR_CHECK`, `INSPECTION`
- `priority`
- `status`: `PENDING`, `ASSIGNED`, `IN_PROGRESS`, `DONE`, `PENDING_INSPECTION`, `APPROVED`, `REJECTED`
- `assigned_to`, `assigned_by`
- `due_at_utc`, `started_at_utc`, `completed_at_utc`
- `inspected_by`, `inspected_at_utc`
- `rejection_reason`
- `created_at_utc`, `updated_at_utc`

### Table `housekeeping_inspections`
- `id`, `task_id`, `room_id`
- `inspection_result`
- `checklist_json`
- `minibar_expected_amount`
- `minibar_actual_amount`
- `minibar_variance_amount`
- `note`
- `inspected_by`, `inspected_at_utc`

### Table `housekeeping_media`
- `id`, `task_id`, `inspection_id`
- `file_url`, `storage_provider`, `content_type`, `file_size`, `sha256`
- `captured_at_utc`, `uploaded_by`

Ảnh nên lưu object storage; database chỉ lưu metadata/URL. Không lưu file ảnh lớn trực tiếp vào bảng SQL.

### Luồng
1. Checkout tạo task checkout-clean.
2. Trưởng buồng phân nhân viên.
3. Nhân viên nhận việc, bắt đầu dọn.
4. Hoàn tất checklist, nhập minibar, đính kèm ảnh theo quy định.
5. Chuyển chờ nghiệm thu.
6. Giám sát kiểm tra, approve hoặc reject kèm lý do.
7. Chỉ sau `APPROVED` phòng mới thành `INSPECTED` và sẵn sàng bán.
8. Mọi thay đổi lưu lịch sử.

### API
- `GET /api/housekeeping/tasks`
- `POST /api/housekeeping/tasks`
- `PATCH /api/housekeeping/tasks/{id}/assign`
- `POST /api/housekeeping/tasks/{id}/start`
- `POST /api/housekeeping/tasks/{id}/complete`
- `POST /api/housekeeping/tasks/{id}/inspect`
- `POST /api/housekeeping/tasks/{id}/media/upload-url`
- `GET /api/rooms/{id}/housekeeping-history`

## D2. Minibar và đối soát

- Checklist minibar theo loại phòng.
- Số đầu kỳ, số tiêu thụ, số còn lại.
- Ghi thiếu/hỏng/miễn phí/khách sử dụng.
- Ảnh làm bằng chứng nếu cấu hình yêu cầu.
- Chênh lệch minibar cần xác nhận trước khi post folio.
- Không tự tạo charge hai lần khi task được gửi lại.
- Tồn kho chỉ bị trừ khi giao dịch được xác nhận, có audit.

## D3. Inventory hoàn thiện

V4 đã có `services`, `stock_movements`, `suppliers`, `retail_sales`; V5 không viết lại các bảng này.

Bổ sung:
- Phiếu nhập hàng theo nhà cung cấp.
- Phiếu xuất dùng nội bộ.
- Phiếu kiểm kê và điều chỉnh tồn.
- Điều chuyển kho giữa cơ sở/kho.
- Định mức tồn tối thiểu.
- Cảnh báo tồn kho thấp.
- Giá vốn và lịch sử thay đổi giá mua.
- Quy trình duyệt điều chỉnh tồn.
- Đối chiếu tổng tồn với ledger movement.

Bảng có thể cần:
- `inventory_locations`
- `purchase_orders`
- `purchase_order_lines`
- `goods_receipts`
- `goods_receipt_lines`
- `stocktakes`
- `stocktake_lines`
- `stock_transfers`
- `stock_transfer_lines`
- `inventory_alerts`

Không cập nhật `stock_quantity` tuỳ tiện mà không có `stock_movement` tương ứng. Mọi thay đổi tồn phải có nguồn chứng từ.

---

# PHẦN E — V5.3: TÀI CHÍNH, NIGHT AUDIT VÀ BÁO CÁO

## E1. Adjustment Voucher / Phiếu điều chỉnh kế toán

Đặc tả V5 hiện có đã đề cập nhưng cần làm rõ:
- Phiếu `CORRECTION`, `REVERSAL`, `REFUND`.
- Trạng thái `PENDING`, `APPROVED`, `REJECTED`, `POSTED`.
- Cần thêm endpoint reject và lý do từ chối.
- Không cho người tạo tự duyệt phiếu của chính mình nếu policy yêu cầu segregation of duties.
- Phiếu đã POST không được sửa/xóa; correction phải tạo voucher mới.
- Có giới hạn số tiền cần cấp duyệt cao hơn.
- Gắn chứng từ nguồn và dòng folio/payment bị điều chỉnh.
- Chống gửi lặp với idempotency key.

### Bảng `adjustment_vouchers`
Các trường tối thiểu:
- `id`, `code`, `property_id`, `reservation_id`, `stay_id`
- `original_charge_id`, `original_payment_id`
- `kind`, `amount`, `currency`
- `reason_code`, `reason_text`
- `status`
- `created_by`, `created_at_utc`
- `approved_by`, `approved_at_utc`
- `rejected_by`, `rejected_at_utc`, `rejection_reason`
- `posted_at_utc`, `idempotency_key`

API:
- `POST /api/adjustment-vouchers`
- `POST /api/adjustment-vouchers/{id}/approve`
- `POST /api/adjustment-vouchers/{id}/reject`
- `POST /api/adjustment-vouchers/{id}/post`
- `GET /api/adjustment-vouchers?propertyId=&status=`
- `GET /api/adjustment-vouchers/{id}`

## E2. Refund và Payment Ledger

V4 đã có hoàn cọc trước check-in dạng payment âm. V5 cần quy trình đầy đủ hơn:
- Hoàn một phần/toàn bộ theo số tiền còn được hoàn.
- Có reason, reference, payment method, approver.
- Refund sau checkout cần chính sách riêng và phê duyệt.
- Refund tiền mặt cần ca phù hợp; refund chuyển khoản phải lưu mã giao dịch.
- Chống refund trùng bằng idempotency.
- Payment gốc là bất biến.
- Báo cáo tổng thu, tổng hoàn, net collected.
- Reconcile payment với shift, bank/payment reference và folio balance.

## E3. Tax Rules và Holiday Rates

V5 spec hiện liệt kê Tax/Holiday nhưng chưa có đặc tả bảng và thứ tự tính chi tiết.

Không hardcode VAT 10%, service charge 5% hoặc holiday surcharge 20% từ ví dụ test. Các mức này phải là cấu hình có hiệu lực theo ngày và được bộ phận kế toán/pháp chế xác nhận.

### Bảng `tax_rules`
- `id`
- `property_id` (nullable nếu áp dụng toàn chuỗi)
- `code`, `name`
- `tax_type`: `VAT`, `SERVICE_CHARGE`, `LOCAL_TAX`, ...
- `rate_percent`
- `calculation_method`: `INCLUSIVE`/`EXCLUSIVE`
- `applies_to`: `ROOM`, `SERVICE`, `MINIBAR`, `ALL`
- `effective_from`, `effective_to`
- `priority`
- `is_active`
- `created_by`, `approved_by`

### Bảng `holiday_rates`
- `id`, `property_id`
- `holiday_code`, `name`
- `date_from`, `date_to`
- `adjustment_type`: percent/fixed
- `adjustment_value`
- `applies_to_rate_plan_id` nullable
- `priority`, `is_active`

Phải định nghĩa:
- Thứ tự áp dụng giá cơ bản → holiday → promotion → discount → service charge/tax.
- Thuế tính trước hay sau giảm giá.
- Làm tròn đến đơn vị nào.
- Thuế/charge inclusive hay exclusive.
- Nhiều chính sách cùng match thì ưu tiên thế nào.
- Snapshot quy tắc tại booking/charge để lịch sử không thay đổi khi tax rule được cập nhật.

## E4. Night Audit thực sự

Đây là một trong các hạng mục bắt buộc của V5.

### Bảng `night_audit_logs`
- `id`, `property_id`, `business_date`
- `status`: `PENDING`, `RUNNING`, `COMPLETED`, `LOCKED`, `REOPENED`, `FAILED`
- `started_at_utc`, `completed_at_utc`, `locked_at_utc`
- `started_by`, `locked_by`, `reopened_by`
- `reopen_reason`
- `room_charges`, `service_charges`, `tax_amount`, `discount_amount`, `total_payments`
- `exception_count`, `result_json`
- unique `(property_id, business_date)`

### Bảng chi tiết
`night_audit_items`:
- `id`, `night_audit_id`
- `reservation_id`, `stay_id`, `charge_id`
- `business_date`, `charge_type`, `amount`
- `source_amount`, `calculation_snapshot`
- unique key để một stay/đêm/loại charge không bị post lặp.

### Luồng chuẩn
1. Xác định business date theo timezone của cơ sở.
2. Kiểm tra ca thu ngân, booking chưa xử lý, khách đến hạn, khách trả hạn, folio lỗi, phòng OOO, giao dịch pending.
3. Hiển thị danh sách ngoại lệ và lý do.
4. Quản lý/kế toán xác nhận chạy.
5. Tính room revenue theo từng business night, không ghi toàn bộ nhiều đêm vào ngày check-in.
6. Tính tax/discount/service charges từ snapshot policy.
7. Ghi ledger trong transaction, dùng idempotency.
8. Đối chiếu folio/payment/shift.
9. Tạo daily close snapshot.
10. Khóa ngày; mọi mutation thuộc business date đã khóa bị chặn.
11. Mở lại chỉ Chain Admin, lý do bắt buộc, audit event; sau khi mở phải chạy lại reconciliation.

API:
- `GET /api/night-audit/precheck`
- `POST /api/night-audit/run`
- `POST /api/night-audit/{id}/lock`
- `POST /api/night-audit/{id}/reopen`
- `GET /api/night-audit/logs`
- `GET /api/night-audit/{id}/items`
- `GET /api/night-audit/{id}/exceptions`

Không được chỉ tạo một `daily_closes` snapshot rồi tuyên bố đã Night Audit.

## E5. Ca thu ngân và Sổ giao ca

V5 cần:
- Expected cash = opening cash + cash receipts - cash refunds - cash expenses.
- Actual cash do thu ngân khai báo.
- Difference = actual - expected.
- Bắt buộc giải trình nếu lệch.
- Người giao và người nhận cùng xác nhận.
- Bàn giao ca phải khóa giao dịch ca cũ theo chính sách.
- Giao dịch sau khi đóng ca phải chuyển sang ca mới.
- Báo cáo tiền mặt, chuyển khoản, thẻ, refund riêng.
- Không chỉ hiển thị `CLOSED`; phải có reconciliation result và evidence.

Bảng đề xuất:
- `cash_shift_reconciliations`
- `shift_handover_logs`
- `shift_handover_items`
- `shift_handover_acknowledgements`

## E6. Dashboard và chỉ số quản trị chuỗi

V5 cần báo cáo đủ tin cậy để sang V6.

### Chỉ số
- **OCC:** room nights sold / available sellable room nights.
- **ADR:** room revenue / rooms sold.
- **RevPAR:** room revenue / available sellable room nights.
- Room revenue, service revenue, tax, discount, net revenue.
- Booking pickup, cancellations, no-show.
- Revenue theo property, room type, channel, agency.
- Deposit outstanding và overdue.
- Receivables/aging.
- Occupancy thấp và doanh thu thấp theo ngưỡng cấu hình.

Không tính chỉ số bằng một công thức frontend. Backend query/service phải thống nhất business definition và có test.

### Bảng/config
- `property_kpi_thresholds`
- `report_snapshots` (nếu cần)
- `notification_events`

### API
- `GET /api/reports/chain/overview`
- `GET /api/reports/occupancy`
- `GET /api/reports/revenue`
- `GET /api/reports/adr-revpar`
- `GET /api/reports/channel-performance`
- `GET /api/reports/deposits-overdue`
- `GET /api/reports/receivables-aging`

Dashboard có drill-down từ chuỗi → khách sạn → ngày → phòng/booking, đồng thời filter quyền theo property scope.

## E7. E-Invoice — làm nền tảng trong V5, tích hợp thật ở V6

V5:
- Có entity, invoice draft, invoice lines, tax snapshot, status, audit.
- Adapter interface và mock provider cho test.
- Retry/idempotency, lưu request/response có che thông tin nhạy cảm.
- Quy trình cancel/adjustment có lý do và phê duyệt.
- Không hiển thị “đã phát hành hợp lệ” khi mới gọi mock.

V6:
- Kết nối nhà cung cấp hóa đơn điện tử thật theo hợp đồng/API credential.
- Kiểm thử sandbox, chữ ký, mẫu/số, lỗi từ chối, điều chỉnh/thay thế/hủy theo quy định.
- Đối chiếu trạng thái và lưu chứng từ phản hồi.

---

# PHẦN F — V5.4: QUYỀN, DỮ LIỆU KHÁCH VÀ TUÂN THỦ

## F1. RBAC theo action và property scope

Vai trò hiện có gồm:
- `CHAIN_ADMIN`
- `PROPERTY_MANAGER`
- `FRONT_DESK_AGENT`
- `SALES_AGENT`
- `ACCOUNTING_AGENT`
- `HOUSEKEEPING_AGENT`
- `MAINTENANCE_AGENT`

Cần bổ sung permission/action:
- `reservation.create`, `reservation.cancel`, `reservation.override_price`
- `reservation.deposit.collect`, `reservation.deposit.refund`
- `stay.checkin`, `stay.checkout`, `stay.transfer`, `stay.extend`
- `folio.charge.create`, `folio.adjustment.create`, `folio.adjustment.approve`
- `payment.collect`, `payment.refund`
- `night_audit.run`, `night_audit.lock`, `night_audit.reopen`
- `housekeeping.assign`, `housekeeping.complete`, `housekeeping.inspect`
- `inventory.receive`, `inventory.adjust`, `inventory.approve`
- `report.property.view`, `report.chain.view`
- `guest.pii.view`, `guest.pii.export`, `guest.anonymize`

Quy định:
- Backend là nơi quyết định quyền; ẩn menu trên Angular không phải bảo mật.
- Default deny.
- Mọi endpoint kiểm tra property scope.
- Kiểm thử user khách sạn A không đọc/sửa dữ liệu khách sạn B.
- Tách quyền tạo và duyệt chứng từ tài chính nếu cần.
- Quyền export PII phải riêng.

## F2. PII và hồ sơ khách

Không tuyên bố tuân thủ GDPR/PDPA/chính sách Việt Nam chỉ vì thêm AES-256.

Cần:
- Data classification: public/internal/confidential/restricted.
- Chỉ thu thập trường cần thiết.
- Giới hạn hiển thị CCCD/hộ chiếu.
- Mask dữ liệu trên list/log/export.
- Mã hóa at-rest và in-transit; key quản lý ngoài source, có rotation.
- Audit ai xem/export PII.
- Retention theo loại hồ sơ và căn cứ pháp lý.
- Anonymization có phê duyệt và không phá dữ liệu kế toán cần giữ.
- Có quy trình quyền truy cập/chỉnh sửa dữ liệu cá nhân.
- Không đưa PII thật vào test data hoặc log.

---

# PHẦN G — KIỂM THỬ VÀ NGHIỆM THU V5

## G1. Các luồng E2E bắt buộc

### E2E-01: Booking đến checkout
Tìm phòng → chọn rate plan → tạo booking → snapshot giá → giữ phòng → thu cọc → xác nhận → check-in → charge theo đêm → thêm dịch vụ → thanh toán → checkout → housekeeping task.

### E2E-02: Booking hết hạn giữ phòng
Tạo hold → chưa đủ cọc → job hết hạn → status EXPIRED → phòng được giải phóng → booking mới có thể đặt.

### E2E-03: Chuyển phòng
Khách đang ở → kiểm tra phòng đích → chuyển phòng → phòng cũ DIRTY → lưu history → chênh lệch giá (nếu có) có phê duyệt → audit.

### E2E-04: Housekeeping
Checkout → task tạo tự động → phân công → bắt đầu dọn → minibar checklist/photo → hoàn tất → supervisor reject/approve → phòng INSPECTED.

### E2E-05: Điều chỉnh tài chính
Lễ tân yêu cầu adjustment → manager tạo/duyệt theo policy → post voucher → folio balance thay đổi đúng → audit before/after → không sửa payment gốc.

### E2E-06: Night Audit
Precheck → resolve exceptions → run → room charges/tax/payment reconcile → lock → thử sửa charge bị từ chối → reopen có lý do → rerun/reconcile.

### E2E-07: Tenant/property isolation
User property A thử đọc, sửa, export booking/guest/report property B → phải 403/404 và không lộ dữ liệu.

### E2E-08: Multi-payment/refund
Thu nhiều lần/nhiều phương thức → refund một phần → gửi lại cùng idempotency key → không ghi trùng → đối soát ca.

## G2. Unit và integration test

- Pricing calculation.
- Snapshot không đổi sau khi rate plan thay.
- Date boundary và timezone.
- Occupancy overlap `[checkIn, checkOut)`.
- Hold expiry và concurrent booking.
- Transfer concurrent.
- Housekeeping state transition.
- Tax order/rounding.
- Adjustment approval/reversal.
- Night audit idempotency/reopen.
- Payment/refund idempotency.
- Multi-property scope.

Integration test phải chạy bằng PostgreSQL thực (container hoặc Neon staging), không chỉ mock EF provider.

## G3. UI E2E Playwright

Kiểm tra:
- Login theo role.
- Điều hướng menu đúng role.
- Form validation.
- Empty/loading/error states.
- Booking lifecycle.
- Housekeeping task.
- Adjustment voucher.
- Night Audit.
- Report filter/export.
- Không có console error nghiêm trọng.

## G4. Performance và reliability

Mốc trong `V5_TEST_PLAN.md` hiện đề xuất 50 concurrent users và response dưới 2 giây. Giữ mốc này làm mục tiêu kiểm thử ban đầu nhưng phải đo theo endpoint và môi trường; báo cáo p50/p95/p99, error rate, throughput, DB CPU/connection.

Kiểm tra:
- 50 user đồng thời tối thiểu 5 phút.
- Booking cùng phòng cùng khung giờ.
- Hai cashier đồng thời thu folio.
- Nhiều tác vụ housekeeping.
- Peak-hour report query.
- Connection pool và retry.
- Backup/restore.
- API startup sau deploy.
- Render cold start cần tách khỏi thời gian xử lý API bình thường.

## G5. Security tests

- Authentication 401.
- Role/action 403.
- Cross-property access 403/404.
- SQL injection/XSS/input validation.
- Rate limit login.
- JWT expiry/signature.
- Secret scanning.
- PII leakage in logs/export.
- File upload content-type/size/authorization.
- Audit log tampering.
- Dependency vulnerability scan.

## G6. Cổng nghiệm thu V5

V5 chỉ được ký nghiệm thu khi:

| Gate | Điều kiện |
|---|---|
| G0 | Build .NET Release PASS |
| G1 | Angular production build PASS |
| G2 | TypeScript check PASS |
| G3 | EF migration V4→V5 chạy trên DB bản sao, không mất dữ liệu |
| G4 | Tất cả unit/integration test critical PASS |
| G5 | API E2E các luồng G1 PASS |
| G6 | Playwright E2E critical PASS |
| G7 | Phân quyền cross-property PASS |
| G8 | Night Audit và ledger reconciliation PASS |
| G9 | Backup/restore thực tế PASS |
| G10 | Load test đạt SLO đã duyệt |
| G11 | Không còn bug P0/P1 |
| G12 | UAT đại diện lễ tân, quản lý, kế toán, buồng phòng ký xác nhận |
| G13 | Production config không seed demo; deployment/rollback được diễn tập |

---

# PHẦN H — NHỮNG LỖI/TÀI LIỆU HIỆN CÓ PHẢI SỬA

1. `docs/FEATURE_MATRIX.md` đang ghi hàng đợi chưa có, nhưng source đã có waitlist API/UI; đồng bộ lại ma trận.
2. `docs/ARCHITECTURE.md` và `docs/TEN_TRUONG_DU_LIEU.md` vẫn mô tả SQL Server; cần cập nhật kiến trúc PostgreSQL/Neon hoặc ghi rõ cả hai provider nếu thật sự support.
3. `docs/V5_FEATURE_SPEC.md` bị thiếu mục chi tiết 2.3/2.4, trong khi tax/holiday được nhắc trong API/test; bổ sung data dictionary và business rules.
4. V5 spec có `REJECTED` nhưng chưa định nghĩa endpoint reject và trường rejection reason.
5. V5 Test Plan ghi static 15 test nhưng file static có 26 checks; test thực tế có cú pháp lỗi và thiếu import `json`. Sửa cho test chạy thật, không chỉ sửa số lượng trong tài liệu.
6. Test Plan tham chiếu các file `qa/v5_unit_tests/Program.cs`, `qa/v5_api_e2e.py`, `qa/v5_angular_e2e.py`, `qa/11_v5_acceptance_windows.ps1`, `qa/load_test.js`; cần tạo các file này hoặc sửa tài liệu theo đúng file thực có.
7. `sql/99-seed-test-data-postgres.sql` có cấu trúc DO block không hợp lệ ở các đoạn cuối, tên bảng `service_items` không khớp entity `services`, và có ký tự/cú pháp kiểu SQL Server `N'...'`; không dùng file hiện tại để seed Neon.
8. `sql/99-verify-test-data-postgres.sql` cũng dùng `service_items`, phải sửa theo schema thật.
9. `render.yaml` đang `Database__InitializeForDemo: "true"`; đổi false cho Production, tách seed thành job chủ động ở môi trường test.
10. `Models.cs` có filtered index dùng cú pháp SQL Server `[idempotency_key]`; phải kiểm tra PostgreSQL filter syntax.
11. Chưa có `.github/workflows/ci.yml` tại commit rà soát.
12. Chưa có thư mục EF migrations tại commit rà soát.
13. Không ghi trực tiếp connection string Neon hoặc password vào SQL/README/commit. Nếu test-only và database chỉ chứa dữ liệu giả, đây không phải lý do dừng toàn bộ roadmap, nhưng credential public vẫn nên rotate và không tái sử dụng cho production.
14. Cập nhật footer Angular đang ghi `SQL Server` nếu thực tế môi trường deploy sử dụng PostgreSQL.

---

# PHẦN I — V6 NÊN LÀ GÌ?

V6 không nên là một bản “V5 cộng thêm 20 menu”. V6 là giai đoạn tích hợp hệ sinh thái và quản trị chuỗi nâng cao, trên nền V5 đã ổn định.

## I1. Tích hợp Hotelink / OTA / Channel Manager

Điều kiện đầu vào:
- Có hợp đồng/phạm vi tích hợp.
- Có API documentation và sandbox credentials.
- Có mapping property, room type, rate plan, meal plan, cancellation policy.
- Có đầu mối vận hành và trách nhiệm đối soát.

Chức năng:
- Đồng bộ availability, rate, restrictions.
- Nhận booking mới/thay đổi/hủy từ OTA.
- Đẩy inventory và giá từ PMS ra channel manager.
- Webhook, retry, dead-letter queue.
- Idempotency và chống booking trùng.
- Dashboard log đồng bộ, trạng thái lỗi, retry thủ công.
- Đối soát booking OTA với reservation nội bộ.

Không tích hợp thật bằng API giả hoặc hardcode một nhà cung cấp trong core PMS. Dùng provider adapter.

## I2. Booking Engine trực tiếp

- Website đặt phòng trực tiếp theo tồn phòng và rate plan.
- Kiểm tra giá cuối cùng phía server.
- Tạo booking và giữ phòng.
- Kết nối cổng thanh toán.
- Gửi email xác nhận/hủy.
- Chính sách hủy, no-show, deposit.
- Phân biệt Direct/OTA/Corporate/Walk-in.
- API public có rate limit, anti-bot và payment security.

## I3. Quản trị chuỗi nâng cao

- Centralized master data: room types, service catalog, rate policies.
- Cho phép tập trung hoặc override theo property.
- Báo cáo hợp nhất doanh thu, OCC, ADR, RevPAR, channel mix.
- So sánh forecast/actual/budget.
- Central booking/reservation across properties.
- Corporate account, negotiated rates, company billing.
- Consolidated receivables và reconciliation.
- Quy trình phê duyệt cấp chuỗi.

## I4. Hệ sinh thái dịch vụ

Tích hợp theo adapter:
- POS nhà hàng/bar.
- Spa/massage.
- PBX/hotline và tính cước điện thoại.
- Kiosk/self check-in.
- Payment gateway.
- Accounting/ERP.
- E-invoice provider thật.
- Email/SMS/Zalo notification provider.

Chỉ làm module có yêu cầu, hợp đồng và API thực tế; không giả lập tích hợp rồi coi đã nghiệm thu.

## I5. CRM, khách hàng thân thiết và doanh nghiệp

- Guest profile hợp nhất toàn chuỗi.
- Duplicate guest matching có kiểm soát.
- Loyalty tier/points/redeem.
- Corporate/company accounts.
- Hợp đồng giá riêng và công nợ theo doanh nghiệp.
- Lịch sử lưu trú, consent và privacy settings.
- Segment/marketing chỉ khi có căn cứ sử dụng dữ liệu và quyền phù hợp.

## I6. Revenue Management và BI

- Pickup/booking pace.
- Forecast occupancy.
- Rate recommendation theo demand.
- Channel profitability.
- Cancellation/no-show trend.
- Dashboard điều hành tập trung.
- Export/report scheduling.
- Không tự động đổi giá trên production nếu chưa có policy, approval và audit.

## I7. Mobile operations / Guest experience

- Ứng dụng/task view cho buồng phòng.
- Maintenance work order.
- Mobile approval cho quản lý.
- Guest portal: pre-arrival, thông tin lưu trú, yêu cầu dịch vụ, checkout request.
- Notification push/email/SMS qua provider.

## I8. Scale, reliability và multi-tenant

- Tách background job khỏi HTTP request.
- Queue/event processing khi có nhu cầu đồng bộ cao.
- Cache có invalidation rule.
- Observability tập trung.
- Database connection pooling và read/report workload strategy.
- Backup, point-in-time recovery, disaster recovery.
- Tenant/property isolation.
- SLO, alert và runbook hỗ trợ vận hành.

---

# PHẦN J — THỨ TỰ TRIỂN KHAI ĐỀ XUẤT

| Thứ tự | Gói | Nội dung | Điều kiện hoàn tất |
|---|---|---|---|
| 1 | V5.0-A | Sửa DB/provider, migration, seed, secrets, health, CI | Build + migration PostgreSQL PASS |
| 2 | V5.1-A | Rate snapshot, Booking Note, Hold, Deposit Reminder | E2E booking/hold/cọc PASS |
| 3 | V5.1-B | Room transfer history, group wizard, folio split, walk-in | E2E Front Desk PASS |
| 4 | V5.2-A | Housekeeping task, assignment, inspection, minibar photo | E2E checkout → inspect PASS |
| 5 | V5.2-B | Inventory receive/stocktake/low stock | Ledger tồn khớp PASS |
| 6 | V5.3-A | Adjustment voucher, refund, tax/holiday rules | Financial test PASS |
| 7 | V5.3-B | Night Audit, lock/reopen, daily close | Night Audit reconciliation PASS |
| 8 | V5.3-C | Shift handover, chain dashboard, OCC/ADR/RevPAR | UAT báo cáo PASS |
| 9 | V5.4 | RBAC action, PII, audit, security/performance | Security + restore + UAT PASS |
| 10 | V6.0 | Hotelink/OTA, booking engine, e-invoice provider, POS/CRM/BI | Integration contract tests PASS |

---

# PHẦN K — DEFINITION OF DONE CHO DEVELOPER

Mỗi Jira/GitHub Issue phải có:
- Requirement ID (nếu lấy từ A25).
- Actor/role.
- Preconditions.
- Main flow.
- Alternative/exception flows.
- Business rules.
- State transitions.
- Field dictionary (UI/JSON/C#/SQL).
- API contract.
- Permission matrix.
- Audit event.
- Unit/integration/API/UI test.
- Acceptance criteria.
- Migration/rollback impact.
- Screenshot hoặc test evidence.

Pull Request phải có:
- Liên kết issue.
- Mô tả nghiệp vụ đã làm.
- Danh sách file thay đổi.
- Migration SQL/EF migration.
- Test chạy và kết quả.
- Ảnh giao diện trước/sau.
- Rủi ro và rollback.
- Không commit secret hoặc dữ liệu khách thật.
- Không deploy Production từ feature branch.

Quy tắc Git:
- `main`: bản ổn định đã nghiệm thu.
- `develop`: tích hợp các gói V5.
- `feature/v5-*`: mỗi chức năng/nhóm chức năng.
- Pull Request feature → develop.
- UAT pass mới PR develop → main.
- Tag release `v5.0.0`, `v5.1.0`, ...; V6 bắt đầu bằng branch/release riêng.

---

# PHẦN L — QUYẾT ĐỊNH CHUYỂN V6

Chỉ được bắt đầu V6 khi:
1. V5.0–V5.4 đã có đủ code/API/UI/database/test.
2. Không còn bug P0/P1.
3. E2E booking → check-in → folio → payment → checkout → housekeeping → night audit đạt.
4. Chuyển phòng, hold expiry, deposit alert, refund và adjustment chạy đúng.
5. Migration V4→V5 được diễn tập trên bản sao DB và không mất dữ liệu.
6. Backup/restore đã kiểm chứng.
7. Cross-property permission test đạt.
8. Performance/load test có báo cáo, không chỉ kết luận cảm tính.
9. Render/Vercel deploy đúng commit, health check đạt.
10. UAT có chữ ký đại diện Lễ tân, Quản lý, Kế toán, Buồng phòng và Ban CNTT.
11. Có tài liệu vận hành, rollback, backup và xử lý sự cố.
12. Các tích hợp dự kiến cho V6 có hợp đồng/API/sandbox/đầu mối rõ ràng.

**Kết luận:** V5 phải biến LOTUS PMS từ bản demo có nhiều chức năng thành một PMS có nghiệp vụ, dữ liệu, tài chính và vận hành kiểm soát được. V6 mới mở rộng ra ngoài lõi PMS bằng tích hợp OTA/Hotelink, hệ sinh thái dịch vụ, CRM và quản trị chuỗi nâng cao.
