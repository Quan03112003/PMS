# Đặc tả tham chiếu HB Soft Hotel → LOTUS PMS

| Mục | Nội dung |
|-----|----------|
| Nguồn tham chiếu chính | `https://ohn.hbsofthotel.vn/register/signin.aspx` (tenant **A25 Hà Nội – Office / A25 HOTEL**) |
| Nguồn bổ sung (công khai) | [hbsoft.vn/product.html](https://hbsoft.vn/product.html), [hbsoft.vn/specialized.html](https://hbsoft.vn/specialized.html), [hotel84.com – HBSOFT HOTEL](https://hotel84.com/spks/phan-mem-quan-ly-khach-san.html), PDF nghiệp vụ VTOS trên hbsoft.vn |
| Ngày khảo sát | 10/11/2026 |
| Mục đích | Tách nghiệp vụ/tính năng PMS kiểu Việt Nam (3★ trở xuống, chuỗi nhỏ–vừa) để đặc tả phát triển LOTUS PMS V5+ |
| Phạm vi quan sát trực tiếp | Đăng nhập **chỉ đọc** ngày 10/11/2026: trang chủ, tape chart đặt phòng, sơ đồ nhận phòng, danh sách đoàn, danh sách dọn phòng, màn thống kê chuỗi. Không bấm Lưu / Thêm / Sửa / Tìm / Excel / check-in / thanh toán. |

> **Giới hạn:** Tài liệu này **không** là dump nội bộ hệ thống A25. Phần bên trong (menu, phím tắt, báo cáo cụ thể) được suy luận từ catalog sản phẩm HB Soft Hotel + chuẩn VTOS mà HB Soft tuyên bố tuân thủ, rồi map sang LOTUS. Khi có tài khoản demo/read-only, cần bổ sung phụ lục “Xác minh live” cho từng màn hình.

---

## 1. Quan sát trực tiếp trang đăng nhập

### 1.1. Nhận diện hệ thống

| Hạng mục | Quan sát |
|----------|----------|
| URL | `/register/signin.aspx` — kiến trúc **ASP.NET WebForms** (`.aspx`) |
| Title | `A25HàNội-Office` |
| Branding | Logo **A25 HOTEL** (góc trái), form song ngữ **VI / EN** |
| Loading | Overlay “Đang xử lý dữ liệu… / Information is checking…” trước khi hiện form |
| Form | Tên đăng nhập, mật khẩu, nút **Đăng nhập \| OK** (icon chìa khóa) |
| Phụ | Link **FORUM** (cộng đồng/hỗ trợ người dùng) |
| UI | Admin cổ điển: header xanh dương, nút đỏ, nền trắng — ưu tiên thao tác nhanh, không marketing |

### 1.2. Ý nghĩa nghiệp vụ từ màn đăng nhập

1. Mỗi khách sạn/chuỗi là **tenant riêng** (A25), cùng nền tảng HB Soft Hotel.
2. Người dùng vận hành đăng nhập theo **username** (không bắt buộc email).
3. Hệ thống kiểm tra phiên/thông tin trước khi vào console — gợi ý session server-side + có thể khóa IP / 1 lễ tân tại một thời điểm (theo catalog).
4. Song ngữ VI–EN trên control chính — LOTUS nên giữ nhãn nghiệp vụ tiếng Việt rõ, có thể i18n sau.

### 1.3. Việc cần làm khi có tài khoản

Checklist xác minh live (điền lại bảng này):

| # | Màn hình / luồng | Đã xem | Ghi chú ảnh/chụp |
|---|------------------|--------|------------------|
| 1 | Room map / sơ đồ phòng màu | ☐ | |
| 2 | Timeline đặt phòng (room plan) | ☐ | |
| 3 | Đặt phòng lẻ / đoàn / đại lý | ☐ | |
| 4 | Check-in / check-out / đổi phòng | ☐ | |
| 5 | Folio + minibar + thu/chi | ☐ | |
| 6 | Giao ca | ☐ | |
| 7 | Tạm trú / PA72 | ☐ | |
| 8 | Báo cáo Excel/PDF | ☐ | |
| 9 | Phân quyền menu | ☐ | |
| 10 | Module PBX / nhà hàng / spa (nếu bật) | ☐ | |

---

## 2. Định vị sản phẩm HB Soft Hotel (tham chiếu)

### 2.1. Phân khúc

- Web online, một KS hoặc **chuỗi nhiều KS**.
- Mục tiêu: KS / nhà nghỉ / căn hộ cho thuê, khoảng **10–100 phòng**, **≤ 3 sao**.
- Chuẩn nghiệp vụ tuyên bố: **VTOS** (lễ tân, buồng, đặt giữ buồng, quản lý KS nhỏ…).
- Mở rộng: nhà hàng, spa/massage, **cước tổng đài (PBX)**, chấm công vân tay.
- Gói cơ bản gồm: Đặt phòng · Lễ tân · Buồng phòng · Kho dịch vụ · Kho vật tư · Đại lý · Báo cáo.

### 2.2. Nguyên tắc thiết kế UX/NV cần học cho LOTUS

| Nguyên tắc HB Soft | Đề xuất LOTUS |
|--------------------|---------------|
| “Đơn giản tối thiểu, hiệu quả tối đa” | Ưu tiên thao tác ca: 1–2 click từ sơ đồ → nghiệp vụ |
| Phím tắt, phù hợp nhiều độ tuổi | Shortcut F-keys / hotkey trên room map & folio |
| Màu trạng thái phòng trực quan | Palette cố định + legend luôn hiện |
| Tham số cho phép/cấm sửa giá | Feature flag theo property + role |
| Phân quyền “thấy gì làm nấy” | RBAC action-level (đã có trong roadmap V5) |
| Báo cáo xuất Excel/PDF | Chuẩn hóa export mọi báo cáo P1 |

---

## 3. Bản đồ phân hệ chức năng (Functional map)

Mã yêu cầu đề xuất cho LOTUS: `HB-xx-nn` (tham chiếu), map sang `YC-*` / module V5 khi triển khai.

### 3.1. HB-A — Đặt phòng (Reservation / Booking)

| ID | Tính năng | Mô tả nghiệp vụ | Actor | LOTUS hiện trạng (ước lượng) |
|----|-----------|-----------------|-------|------------------------------|
| HB-A-01 | Đặt phòng khách lẻ | Chọn ngày đến/đi, hạng/phòng, giá, khách, cọc | Lễ tân, Sales | Có (bookings) |
| HB-A-02 | Đặt phòng khách đoàn | Master đoàn + nhiều phòng, quản lý trên sơ đồ | Lễ tân, Sales | Có một phần (groups) |
| HB-A-03 | Đặt theo đại lý / tour | Gắn agency, % hoa hồng / công nợ | Sales | Có agencies cơ bản |
| HB-A-04 | Kiểm tra phòng trống | Availability theo khoảng ngày + hạng | Mọi role đặt phòng | Có (available rooms) |
| HB-A-05 | Room plan / timeline | Trạng thái đặt trực quan trên trục thời gian | Lễ tân | Thiếu / yếu so với tape chart |
| HB-A-06 | Room map ngày đến | Đến ngày nhận → tự hiện trên sơ đồ | Hệ thống | Có arrivals KPI; cần rule tự highlight |
| HB-A-07 | Pre-charge dịch vụ vào booking | Gắn minibar/DV trước khi nhận phòng | Lễ tân | Có một phần (services) |
| HB-A-08 | DS đặt phòng + DT dự kiến | In/xuất danh sách, ước tính doanh thu | Quản lý, Sales | Báo cáo cơ bản |
| HB-A-09 | Thống kê tần suất phòng | Occupancy / tần suất theo phòng | Quản lý | Một phần reports |
| HB-A-10 | Waitlist khi hết phòng | Hàng đợi, gán khi có phòng | Lễ tân | Có waitlist |

**Luồng chuẩn đặt phòng lẻ**

```text
Yêu cầu khách
  → Chọn property / ngày đến-đi / hình thức nghỉ (giờ|ngày|qua đêm)
  → Check availability (không overbook trừ khi policy cho phép)
  → Chọn hạng phòng (± gán số phòng hoặc allotment)
  → Nhập khách + CCCD/passport + SĐT + nguồn (walk-in|OTA|đại lý|web)
  → Chốt giá (rate plan + override nếu được phép) + cọc (nếu có)
  → Xác nhận → in/ gửi xác nhận đặt phòng
  → Ngày đến: hiện trên room map / arrivals list → check-in
```

**Quy tắc nghiệp vụ bắt buộc**

1. Không cho hai booking CONFIRMED chồng lịch cùng phòng (trừ trạng thái overlapping được duyệt).
2. Giá sau xác nhận phải **snapshot** — đổi bảng giá sau không làm lệch folio cũ.
3. Hủy / no-show: policy giữ cọc / hoàn cọc theo rule property.
4. Đoàn: có master bill + split bill từng phòng.

---

### 3.2. HB-B — Lễ tân / Front Office (In-house)

| ID | Tính năng | Mô tả | LOTUS |
|----|-----------|-------|-------|
| HB-B-01 | Nhận phòng lẻ/đoàn | Check-in, gán phòng, mở folio | Có |
| HB-B-02 | Trả phòng | Checkout, thu đủ, đóng folio, phòng → DIRTY | Có |
| HB-B-03 | Sơ đồ màu + thống kê | Vacant / Occupied / Dirty / Arrival / Maintenance… | Có roommap |
| HB-B-04 | Tự gán giá khi nhận | Lấy từ rate plan / booking | Có; cần snapshot V5 |
| HB-B-05 | Sửa giá (tham số) | Cho phép hoặc khóa theo config | Cần flag |
| HB-B-06 | Hình thức nghỉ | DAY_USE / HOURLY / OVERNIGHT | Có pricing modes |
| HB-B-07 | Nhiều khách / phòng | Occupants, phụ thu người thêm | Có occupants cơ bản |
| HB-B-08 | Tạm trú theo mẫu | Form khai báo; xuất PA72 / cổng CA | Có registration nháp; cần chuẩn CA |
| HB-B-09 | Tài sản khách gửi | Lưu đồ / safe deposit | Có guest assets |
| HB-B-10 | Thu / chi tại quầy | Phiếu thu-chi gắn ca | Có vouchers |
| HB-B-11 | Minibar / dịch vụ | Post charge + trừ kho | Có |
| HB-B-12 | Giao ca | Mở/đóng ca, bàn giao quỹ | Có shifts + reconcile cơ bản |
| HB-B-13 | Đổi phòng | Chuyển DV kèm theo | Có transfer cơ bản; thiếu lịch sử giá |
| HB-B-14 | Nhập/tách đoàn | Move room in/out group | Cần làm rõ |
| HB-B-15 | Thanh toán hộ | Pay giúp khách khác / master | Cần đặc tả V5 |
| HB-B-16 | Kiểm tra dọn phòng | Trạng thái HK trước sell | Có HK status; thiếu checklist |

**Luồng check-in**

```text
Arrivals hôm nay / chọn booking
  → Xác minh giấy tờ + cập nhật occupants
  → Kiểm tra phòng CLEAN/INSPECTED
  → Gán/đổi số phòng nếu cần
  → Mở stay + folio; ghi cọc đã thu
  → (Tuỳ policy) post room charge ngay HOẶC để Night Audit post theo đêm
  → In registration card / key card
  → Cập nhật room map → OCCUPIED
```

**Luồng check-out**

```text
Chọn stay
  → Post DV/minibar còn thiếu + phone (nếu có)
  → Xem folio balance
  → Thu tiền (TM cần ca mở) / công nợ đại lý / thẻ
  → In bill; đóng stay
  → Phòng → DIRTY; tạo task HK
  → Cập nhật tạm trú / danh sách khách đi
```

**Trạng thái phòng đề xuất (màu cố định LOTUS)**

| Trạng thái | Ý nghĩa vận hành | Gợi ý màu |
|------------|------------------|-----------|
| VACANT_CLEAN | Sẵn sàng bán | Xanh lá |
| VACANT_DIRTY | Trống, chờ dọn | Cam |
| VACANT_INSPECTED | Đã kiểm tra | Xanh dương nhạt |
| OCCUPIED | Có khách | Xanh dương |
| ARRIVAL | Có booking đến hôm nay | Tím |
| DUE_OUT | Trả hôm nay | Vàng đậm |
| MAINTENANCE / OOO | Bảo trì | Xám |
| OUT_OF_SERVICE | Ngưng bán | Đen/xám đậm |

---

### 3.3. HB-C — Buồng phòng (Housekeeping)

| ID | Tính năng | Mô tả | LOTUS |
|----|-----------|-------|-------|
| HB-C-01 | Cập nhật tình trạng vệ sinh | Dirty / Clean / Inspected | Có |
| HB-C-02 | Kiểm tra dọn trước nhận | Chặn check-in nếu Dirty (policy) | Nên bắt buộc V5 |
| HB-C-03 | Minibar trong phòng | Định mức + kiểm kê | Một phần |
| HB-C-04 | Tài sản phòng | Checklist đồ phòng | Thiếu |
| HB-C-05 | Giao việc / ảnh chứng từ | Task + assignee + photo | Roadmap V5 |

---

### 3.4. HB-D — Dịch vụ & kho minibar

| ID | Tính năng | Mô tả | LOTUS |
|----|-----------|-------|-------|
| HB-D-01 | Chuyển DV giữa phòng | Toàn bộ / một phần | Có transfers |
| HB-D-02 | Thanh toán DV trước / từng phần / hết | Split payment trên charge | Một phần folio pay |
| HB-D-03 | Xuất kho tự động khi bán | Post service → stock out | Có |
| HB-D-04 | Trả lại → nhập kho | Void/return service | Cần rõ rule |
| HB-D-05 | Bán ngoài (walk-in / nội bộ) | Retail không gắn phòng | Có retail |
| HB-D-06 | Thẻ kho minibar | Card theo mặt hàng | stock ledger |

---

### 3.5. HB-E — Kho vật tư & NCC

| ID | Tính năng | LOTUS |
|----|-----------|-------|
| HB-E-01 | Danh mục hàng hóa | Có |
| HB-E-02 | Nhà cung cấp | Có suppliers |
| HB-E-03 | Nhập / xuất / tồn | Có stockledger |
| HB-E-04 | Báo cáo XNT | Một phần |

---

### 3.6. HB-F — Đại lý du lịch

| ID | Tính năng | LOTUS |
|----|-----------|-------|
| HB-F-01 | Danh mục đại lý / lữ hành | Có |
| HB-F-02 | % hoa hồng / đối chiếu công nợ | Cơ bản; thiếu đối soát đầy đủ |
| HB-F-03 | Thống kê nguồn khách theo đại lý | agencyReports cơ bản |
| HB-F-04 | Master account / credit limit | Cần V5 |

---

### 3.7. HB-G — Cước điện thoại (PBX) — module mở rộng

| ID | Tính năng | LOTUS |
|----|-----------|-------|
| HB-G-01 | Gắn cuộc gọi theo phòng | Chưa |
| HB-G-02 | Đầu số trong nước / QT + bảng giá | Chưa |
| HB-G-03 | Nhiều tổng đài → 1 trung tâm | Chưa |
| HB-G-04 | Post cước vào folio tự động | Chưa |
| HB-G-05 | Chuyển cước lễ tân ↔ phòng | Chưa |

> Đề xuất LOTUS: **V5.x optional connector**, không chặn core PMS. Interface: `PhoneChargePosted { roomId, stayId, amount, startedAt, durationSec, destination }`.

---

### 3.8. HB-H — Quản trị hệ thống & chuỗi

| ID | Tính năng | LOTUS |
|----|-----------|-------|
| HB-H-01 | Multi-property (chuỗi) | Có |
| HB-H-02 | Nhiều tòa nhà / 1 KS | Có buildings |
| HB-H-03 | Guest profile dùng chung chuỗi | Có guests; cần unique identity |
| HB-H-04 | RBAC theo menu/action | Role-based; V5 action-level |
| HB-H-05 | Chặn IP | Chưa |
| HB-H-06 | Chỉ 1 lễ tân active (anti fake) | Chưa — cân nhắc concurrent session policy |
| HB-H-07 | Audit log thao tác | Có audit |
| HB-H-08 | Thời gian làm việc / chấm công | Chưa (module riêng) |

---

### 3.9. HB-I — Báo cáo

| ID | Báo cáo | Ưu tiên LOTUS |
|----|---------|---------------|
| HB-I-01 | Danh sách phiếu thanh toán | P1 |
| HB-I-02 | Công nợ chi tiết | P1 |
| HB-I-03 | DT tổng hợp | P1 |
| HB-I-04 | DT thuê phòng | P1 |
| HB-I-05 | DT dịch vụ | P1 |
| HB-I-06 | DT điện thoại | P3 (khi có PBX) |
| HB-I-07 | XNT minibar / vật tư | P2 |
| HB-I-08 | Chấm công / lương | P3 |
| HB-I-09 | Lượt khách nghỉ | P1 |
| HB-I-10 | Thống kê đại lý | P2 |
| HB-I-11 | Khách lưu trú / tạm trú | P1 |
| HB-I-12 | KQKD / chuỗi multi-property | P1 |
| HB-I-13 | Export Excel + PDF | P1 |

**KPI vận hành ngày (Front Office Manager — theo VTOS/HB Soft)**

1. Báo cáo kiểm toán đêm / chốt ngày hôm trước.
2. Occupancy % và ADR ngày.
3. Arrivals / Departures / Stay-over.
4. Phòng Dirty / OOO.
5. VIP / đặc biệt.
6. Dự báo buồng 7 / 14 / 30 ngày.

---

## 4. Đặc tả nghiệp vụ chi tiết theo vai trò

### 4.1. Lễ tân

| Ca làm việc | Việc phải làm trên PMS |
|-------------|------------------------|
| Đầu ca | Mở ca thu ngân; xem arrivals/departures; kiểm tra phòng Dirty/OOO |
| Trong ca | Đặt phòng walk-in; check-in; post DV; đổi phòng; thu cọc/TT; xử lý khiếu nại nhỏ |
| Cuối ca | Đối soát quỹ; in báo cáo ca; bàn giao ghi chú (VIP, late checkout, unpaid) |

**Acceptance (lễ tân)**

- Từ room map: click phòng → mở folio / check-in / checkout / đổi trạng thái HK trong ≤ 3 thao tác.
- Không thu tiền mặt khi không có ca OPEN.
- Không check-in phòng DIRTY nếu `property.requireInspectedBeforeCheckIn = true`.

### 4.2. Sales / đại lý

- Tạo booking allotment / group tentative → confirm.
- Báo giá (quote) không ghi sổ cho đến khi confirm.
- Theo dõi pickup theo nguồn (OTA, đại lý, web, walk-in).

### 4.3. Buồng phòng

- Nhận list phòng cần dọn theo tầng.
- Cập nhật Clean → chờ Inspected.
- Ghi minibar thiếu → auto charge folio (nếu rule bật).

### 4.4. Kế toán / thu ngân

- Phiếu thu-chi, công nợ đại lý, đối soát ca, điều chỉnh folio (maker-checker).
- Không sửa bút toán đã chốt ngày — chỉ phiếu điều chỉnh.

### 4.5. Quản lý / chủ chuỗi

- Dashboard multi-property.
- Phân quyền, audit, khóa ngày, xem variance ca.
- Báo cáo KQKD / Excel.

---

## 5. Mô hình dữ liệu nghiệp vụ (logic) cần có đủ so với HB Soft

```text
Property 1──* Building 1──* RoomType 1──* Room
Property 1──* RatePlan
Property 1──* Agency
Guest (shared chain) 1──* Reservation
Reservation 1──* Occupant
Reservation 0..1── Stay (in-house)
Stay 1──* FolioCharge
Stay 1──* Payment
Stay 0──* RoomMove (history)
Stay 0──* GuestAsset
ServiceItem 1──* StockLedger
CashShift 1──* Payment/Voucher
DailyClose / NightAudit (business_date)
AuditEvent (immutable)
```

**Hình thức nghỉ (stay mode)** — bắt buộc như HB Soft:

| Mode | Cách tính tiền |
|------|----------------|
| `HOURLY` | Base block + overtime theo giờ |
| `DAY_USE` | Gói trong ngày (cửa sổ giờ) |
| `OVERNIGHT` | Theo đêm; Night Audit post từng đêm **hoặc** prepaid toàn kỳ (chọn 1 policy, không double-post) |

---

## 6. Ma trận đối chiếu LOTUS ↔ HB Soft

| Phân hệ HB Soft | LOTUS V4/V5 | Khoảng trống ưu tiên |
|-----------------|-------------|----------------------|
| Đặt phòng lẻ/đoàn/đại lý | Có | Timeline/tape chart; pre-charge booking |
| Room map màu | Có | Legend + due-out + OOO rõ hơn |
| Check-in/out, đổi phòng | Có | Lịch sử đổi phòng + chênh giá |
| Tạm trú / PA72 | Nháp | Kết nối cổng CA / mẫu in chuẩn |
| Minibar + kho | Có | Định mức phòng + kiểm kê HK |
| Giao ca / đối soát | Cơ bản | Bàn giao điện tử + xử lý variance |
| Đại lý + công nợ | Cơ bản | Credit limit + đối soát kỳ |
| PBX | Không | Connector optional |
| Nhà hàng / Spa | Không | Ngoài scope V5 core (phase sau) |
| Multi-property + RBAC | Có | Action permission + IP/session policy |
| Báo cáo Excel/PDF | Một phần | Chuẩn hóa bộ báo cáo P1 |
| Night audit / chốt ngày | Snapshot dayclose | Night audit thật + khóa sổ |
| OTA channel | Sandbox V5 | HB Soft gần như không nhấn mạnh OTA — LOTUS nên **vượt** bằng channel manager |

> LOTUS có lợi thế: OTA webhook sandbox, voucher điều chỉnh, holiday/tax draft, ArchitectUI shell hiện đại. Mục tiêu không copy UI WebForms, mà **đạt parity nghiệp vụ core + vượt về kênh bán & kiểm soát kế toán**.

---

## 7. Đặc tả yêu cầu chức năng đề xuất cho LOTUS (backlog tham chiếu)

### 7.1. P0 — Parity vận hành ca lễ tân

1. **Room map ca-ready:** click phòng → action sheet (CI/CO/folio/HK/đổi phòng).
2. **Arrivals/Departures board** riêng, filter theo giờ.
3. **Stay mode HOURLY/DAY/OVERNIGHT** tính đúng, không double charge.
4. **Ca thu ngân bắt buộc** với mọi payment CASH.
5. **Snapshot rate** trên reservation (đã có trong V5 spec).

### 7.2. P1 — Nghiệp vụ Việt Nam điển hình (theo HB Soft)

1. Form tạm trú + xuất danh sách lưu trú theo ngày.
2. Tài sản khách gửi + biên bản nhận/trả.
3. Đoàn: master folio, tách/ gộp phòng.
4. Đại lý: % hoa hồng, công nợ, báo cáo nguồn khách.
5. Bộ báo cáo P1 + Excel.
6. Housekeeping checklist + chặn bán phòng Dirty.

### 7.3. P2 — Nâng cấp vượt tham chiếu

1. OTA 2-lớp xác nhận (đã có hướng V5).
2. Phiếu điều chỉnh maker-checker.
3. Holiday rate + tax/service charge trong quote.
4. E-invoice draft.
5. Notification cọc / sắp đến hạn.
6. Tape chart kéo-thả nhiều phòng.

### 7.4. P3 — Module tùy chọn

1. PBX connector.
2. POS nhà hàng / spa.
3. Chấm công.
4. IP allowlist / single active cashier session.

---

## 8. Use case đặc tả mẫu (mức chấp nhận)

### UC-01 — Walk-in nghỉ giờ

**Actor:** Lễ tân  
**Tiền điều kiện:** Có phòng VACANT_CLEAN; ca OPEN nếu thu TM.  
**Luồng chính:**

1. Mở room map → chọn phòng trống.
2. Chọn `HOURLY`, giờ vào, dự kiến giờ ra, số khách.
3. Hệ thống quote giá → xác nhận.
4. Nhập CCCD/SĐT → check-in ngay (hoặc booking + CI).
5. Post DV nếu có → checkout → thu tiền → phòng DIRTY.

**Hậu điều kiện:** Folio đóng balance 0 (hoặc công nợ nếu được phép); audit events đủ; stock đã trừ.

### UC-02 — Đoàn đại lý 5 phòng qua đêm

1. Sales tạo group + agency + rate plan.
2. Allot 5 phòng (hoặc hạng, gán số sau).
3. Thu cọc theo % hợp đồng.
4. Ngày đến: lễ tân CI từng phòng / CI hàng loạt.
5. Trong kỳ: post DV; chuyển 1 phòng ra khỏi đoàn.
6. CO: master pay phần phòng; khách lẻ tự trả minibar.

### UC-03 — Đổi phòng có chênh giá

1. Khách yêu cầu lên hạng.
2. Lễ tân chọn phòng đích CLEAN.
3. Hệ thống hiển thị old rate / new rate / phụ thu.
4. Manager duyệt nếu vượt ngưỡng.
5. Folio ghi điều chỉnh; room move history lưu lý do.

### UC-04 — Giao ca

1. Nhân viên A xem expected cash = opening + room cash + retail − expense.
2. Nhập actual; nếu variance ≠ 0 → bắt buộc lý do.
3. Manager xác nhận; ca CLOSED; nhân viên B mở ca mới.

---

## 9. Phi chức năng học từ mô hình HB Soft

| Hạng mục | Yêu cầu |
|----------|---------|
| Độ trễ thao tác lễ tân | Room map load < 2s cho ≤ 100 phòng |
| Đồng thời | Nhiều user; conflict booking → 409 rõ ràng |
| Audit | Mọi thay đổi tiền/phòng/giá có before/after |
| Offline | Không bắt buộc (HB Soft online); LOTUS có thể cân nhắc queue queue sau |
| Ngôn ngữ | VI trước; EN nhãn chính |
| Bảo mật | JWT/session; rate limit login; không lộ stack trên UI |
| Export | Excel/PDF UTF-8 (tiếng Việt) |

---

## 10. Kết luận & bước tiếp theo

### Đã làm được hôm nay

- Truy cập thành công URL tenant A25; xác nhận đây là **HB Soft Hotel** (ASP.NET `.aspx`), branding A25, form đăng nhập song ngữ.
- Tổng hợp **đầy đủ phân hệ** từ catalog công khai HB Soft Hotel + định hướng VTOS.
- Viết đặc tả chức năng/nghiệp vụ + ma trận gap LOTUS + backlog P0–P3 + use case chấp nhận.

### Chưa làm được (cần tài khoản)

- Vào menu thật, đếm màn hình, phím tắt, mẫu in, tham số cấu hình từng property.
- Chụp room map / folio / báo cáo để append phụ lục UI.

### Đề nghị

1. Cấp **tài khoản demo/read-only** (hoặc quay màn hình thao tác ca) → cập nhật mục 1.3 và bổ sung wireflow từng màn.
2. Chốt với chủ nghiệp vụ: LOTUS nhắm **parity P0–P1** với HB Soft core, **không** làm nhà hàng/spa trong V5.
3. Dùng bảng mục 6–7 làm backlog sprint V5.1 (Front Office parity).

---

## Phụ lục A — Nguồn

1. `https://ohn.hbsofthotel.vn/register/signin.aspx` — quan sát live 10/11/2026  
2. `https://hbsoft.vn/product.html` — tính năng HBSOFT HOTEL  
3. `https://hbsoft.vn/specialized.html` — danh mục nghiệp vụ VTOS  
4. `https://hotel84.com/spks/phan-mem-quan-ly-khach-san.html` — mô tả mở rộng (timeline, PA72, anti fake IP)  
5. `http://www.hbsoft.vn/download/NghiepVuKhachSan.pdf` — tài liệu nghiệp vụ KS (bối cảnh lễ tân / dự báo buồng)  
6. Nội bộ LOTUS: `docs/V5_FEATURE_SPEC.md`, `docs/LOTUS PMS — Rà soát V4 và đặc tả bàn giao phát triển V5.md`

## Phụ lục C — Quan sát live (chỉ đọc, 10/11/2026)

Đăng nhập được vào `default.aspx`. Phiên hiển thị user nội bộ mã 81, nhãn thử việc. Không ghi mật khẩu vào tài liệu này.

### C.1. Menu thật của tài khoản này

| Menu | Mục con đã thấy |
|------|-----------------|
| ĐẶT PHÒNG | Tape chart `booking/reservation.aspx` |
| NHẬN PHÒNG | Sơ đồ `booking/book.aspx` |
| CHUỖI | Báo cáo chuỗi (nhóm), Thống kê chuỗi KS |
| DANH SÁCH | Đại lý (nhóm) → tỷ giá, đổi ngoại tệ; Thông tin dịch vụ → phiếu nhập/xuất kho, NCC; Chứng từ kế toán → phiếu thu, phiếu chi; Thông tin vật tư → phiếu nhập/xuất, hàng hóa, NCC giặt/vật tư |
| BÁO CÁO | Phiếu thanh toán, thu tiền, công nợ (chi tiết, lịch sử, tạm ứng, trả lại), check-in (nhóm), dịch vụ đã checkout, NXT dịch vụ, NXT vật tư |
| Thanh công cụ | Trang chủ, ghi chú nhắc việc, thông tin đoàn, NV dọn phòng, danh sách dọn phòng, đổi mật khẩu, forum |
| THOÁT | Có hộp xác nhận trước khi thoát |

Tài khoản này chỉ thấy **một cơ sở vận hành**: A25 Premium Hotel — 36 Giang Văn Minh. Trang nhận phòng mặc định mở node chuỗi “A25 Hotel Group” (gần như không có phòng). Không đổi bộ lọc khách sạn để tránh postback.

### C.2. Đặt phòng — tape chart

Bộ lọc: từ/đến ngày, số phòng, mã đặt / BookingID đại lý, tên khách tương đối, loại phòng, đại lý, trạng thái phòng, loại đặt phòng.

Loại phòng: Superior, Deluxe, Premium Deluxe, Family, Executive, Phòng chờ.

Trạng thái lọc: Hàng đợi, Có phòng, Khách lẻ, Khách đoàn.

Loại đặt phòng (mã hiển thị → mã hệ thống):

| Hiện trên màn | Mã | Gợi ý nghiệp vụ |
|----------------|----|-----------------|
| 24 | D | Đặt trong ngày / 24h |
| B | F | Đặt ngày đêm |
| B2 | R | Biến thể đặt (cần chủ NV xác nhận) |
| GB | G | Đoàn |

Hình thức nghỉ khi lập phiếu: **Ngày đêm / Qua đêm / Nghỉ giờ**.

Mỗi ô loại phòng gắn bảng giá: cuối tuần, đầu tuần, nghỉ giờ, thêm giờ, giá niêm yết, mô tả phòng (diện tích, giường, view), định mức khách, phụ thu thêm người và mốc phút.

Ô lịch theo ngày: khách đang giữ phòng (ô đỏ), phòng **Đang dọn** (ô nâu), khách giờ. Kênh thấy trên ô: OTA (Agoda, Booking.com, Ctrip, Expedia, Traveloka, Go2Joy, Mytour, Tiket…), Booking Engine, sales, hotline, mạng xã hội.

Thanh tổng trên một khoảng ngày mẫu: khoảng 180 phòng-đặt, tách B / B24 / 24h, và một hàng **Queue**.

Chức năng có trên màn (chỉ đọc nhãn, không bấm): Thêm mới (Alt+N), phòng sẵn / check availability, thống kê, người đặt, room plan ngày đêm, room plan giờ, giá bất thường, in xác nhận (thường / song ngữ / đoàn song ngữ), Excel, dịch vụ đặt trước, thêm khách, xem trạng thái phòng, lưu/đóng.

Hồ sơ khách trên form: danh xưng Mr/Mrs/Miss/Ms, giới tính, CCCD / hộ chiếu / giấy tờ khác, dân tộc, quốc tịch, tôn giáo, mục đích lưu trú (công tác, du lịch, thăm thân, khác), thẻ KHTT.

Thu cọc / thanh toán: tiền mặt, chuyển khoản, cà thẻ, Momo, VNPay, và nhiều ngoại tệ (USD, EUR, CAD, SGD, THB, INR, CNY, AUD, MYR, KRW, khác).

### C.3. Nhận phòng — sơ đồ màu

Chú giải đếm: **Còn trống** (xanh), **Đặt trước** (vàng), **Mới nhận** (hồng), **Đang ở** (đỏ), thêm các màu nâu / xanh dương / tim.

Menu chuột phải theo trạng thái (có trong DOM, không kích hoạt):

| Trạng thái | Việc lễ tân làm được |
|------------|----------------------|
| Trống | Nhận phòng; chuyển “cần dọn”; “cần sửa chữa”; xem thông tin phòng |
| Đang dọn / booking online đang dọn | Đã dọn xong và **rút camera**; cần sửa chữa; thông tin phòng |
| Đang sửa | Đã sửa xong; nội dung sửa; thông tin phòng |
| Có booking đến | Nhận phòng; nhận khách giờ; chuyển phòng; thông tin phòng |
| Đang ở | Thanh toán phòng; danh sách khách; tài sản khách; sửa thông tin; chuyển phòng; thông tin phòng |

Menu phụ HOTEL: danh sách nhận phòng, đặt phòng, đến hạn, sửa chữa, sơ đồ, doanh thu dự kiến, tình trạng phòng, tình trạng booking. Có nhãn “Thanh toán luôn”.

### C.4. Đoàn

Màn “Thông tin đoàn”: tìm số phiếu, tên đoàn, đại lý, trạng thái **Đặt trước & đoàn trống / Đoàn trống / Đặt trước / Đang ở / Đã trả phòng**. Dữ liệu không tự tải cho đến khi bấm Tìm — không bấm.

### C.5. Dọn phòng

Danh sách tự hiện trong ngày. Cột: số phiếu, số phòng, giờ bắt đầu/kết thúc, thời lượng, trạng thái phòng, trạng thái dọn, tối đa 3 nhân viên dọn, người nghiệm thu. Thao tác dòng: In / Xem / Sửa / Cập nhật — không bấm.

Phân loại: **Dọn lưu** (Occupied) và **Dọn checkout**. Trạng thái việc: **Đang dọn → Chờ nghiệm thu → Đã dọn xong**. Có gán nhân viên dọn và người nghiệm thu.

### C.6. Việc cố ý không làm

Không bấm Tìm trên thống kê chuỗi (màn hình cảnh báo báo cáo nặng, tránh giờ cao điểm). Không xuất Excel, không mở form lưu, không đổi trạng thái phòng, không đăng xuất.

## Phụ lục B — Thuật ngữ

| Thuật ngữ | Nghĩa |
|-----------|-------|
| Room map | Sơ đồ phòng theo tầng/trạng thái |
| Room plan / tape chart | Timeline đặt phòng theo trục ngày |
| Folio | Sổ tính tiền của lần lưu trú |
| Night audit | Chốt đêm / post tiền phòng theo business date |
| Allotment | Giữ hạng phòng chưa gán số |
| Master folio | Folio đoàn / công ty trả hộ |
| PA72 | Khai báo tạm trú gửi công an |
| VTOS | Vietnam Tourism Occupational Skills Standards |
