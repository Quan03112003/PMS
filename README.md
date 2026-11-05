# LOTUS PMS V2 — quản lý chuỗi khách sạn (bản demo nghiên cứu nghiệp vụ)

**Công nghệ:** Angular 20 + TypeScript; ASP.NET Core 10 Web API (C#); Entity Framework Core 10; Microsoft SQL Server 2022; DBeaver để xem/chỉnh dữ liệu. **Không sử dụng DBeaver làm database engine.**

**Phiên bản:** V2 mở rộng từ LOTUS PMS V1, lấy tài liệu `docs/PMS_NghiepVu_Goc_V1.docx` làm nền tảng và tham chiếu thông tin công khai HBSOFT HOTEL. Mã và giao diện được thiết kế độc lập; không phải bản sao HBSOFT. **Đây là DEMO, không dùng dữ liệu khách thật / tiền thật / triển khai internet công khai.**

## 1. Điểm mới V2 theo 3 giai đoạn

**Giai đoạn 1 — Lễ tân:** đặt phòng khách đoàn nhiều phòng trong transaction, check-in đoàn, thanh toán hộ phân bổ tiền, checkout đoàn, hàng đợi giữ yêu cầu & gán phòng, chuyển một phần/toàn bộ khoản dịch vụ giữa folio, checkout ghi nợ, thu nợ nhiều lần, khôi phục checkout cùng ngày có kiểm tra điều kiện, lưu tài sản khách gửi, chọn bảng giá/loại lưu trú và đại lý khi đặt phòng. Kế thừa check-in/check-out đơn, sơ đồ phòng, đổi phòng, gia hạn, thanh toán, minibar, buồng phòng, giao ca.

**Giai đoạn 2 — Quản lý:** nhiều tòa trong một cơ sở, gán phòng, cấu hình gói giá nghỉ giờ/ngày/đêm, nhà cung cấp, nhập/xuất/điều chỉnh kho có nhật ký số lượng, bán hàng ngoài phòng, phiếu thu/chi khác, công nợ, thông tin đại lý, người dùng/phạm vi cơ sở, đối soát quỹ tiền mặt ca.

**Giai đoạn 3 — Quản trị chuỗi:** doanh thu ghi sổ theo cơ sở, số dư công nợ đang mở, hàng bán lẻ, tiền folio đã thu, công suất ước tính, hiệu quả đại lý và hoa hồng dự kiến, báo cáo tồn kho, CSV danh sách lưu trú và CSV báo cáo chuỗi, nút in/lưu PDF thông qua trình duyệt, snapshot dữ liệu theo ngày (không khóa sổ), nhật ký thao tác.

Xem `docs/V2_FEATURE_MATRIX.md` để biết chức năng có API/UI thật, giới hạn và phần chưa tích hợp bên ngoài.

## 2. Yêu cầu cài trên Windows

1. .NET SDK 10 (`dotnet --version` phải là 10.x).
2. Node.js phiên bản hỗ trợ Angular 20 (`node -v`, đề xuất 22 LTS) và npm.
3. Docker Desktop để khởi động SQL Server 2022 bằng Compose. Nếu có SQL Server Developer/Express sẵn có thể tự sửa connection string.
4. DBeaver Community để quản trị dữ liệu; Visual Studio / VS Code để sửa và build.
5. Kết nối internet lần đầu để `dotnet restore` và `npm install`, vì máy cần tải package.

## 3. KHỞI ĐỘNG — CSDL MỚI

Từ thư mục dự án đã giải nén mở PowerShell:

```powershell
.\01_START_DATABASE.bat
```

Chờ SQL Server container sẵn sàng (`docker compose ps`). Mở PowerShell thứ hai:

```powershell
.\02_START_BACKEND.bat
```

Backend `Development` tự tạo schema bằng `EnsureCreated()` cho database **mới hoàn toàn** và seed 2 cơ sở, 28 phòng, 3 tài khoản, dịch vụ, đại lý, 3 bảng giá/cơ sở, 1 tòa nhà/cơ sở. Backend: `http://localhost:5080/swagger` và `http://localhost:5080/api/health`.

Mở PowerShell thứ ba:

```powershell
.\03_START_ANGULAR.bat
```

Angular: **http://localhost:4200**. Proxy `/api` → backend `http://localhost:5080`.

**Nếu bạn đã tạo database từ PMS V1:** KHÔNG chạy V2 backend trước khi nâng cấp CSDL. Backup database, trong DBeaver mở `sql/03-v2-upgrade.sql` với connection đến `HotelChainPms` và chạy toàn bộ dạng Script. Sau khi xuất hiện `V2 UPGRADE COMPLETE`, khởi động V2 backend; AdvancedSeeder sẽ tạo dữ liệu mẫu V2 nếu danh mục còn trống. Không xóa database V1 để tránh mất dữ liệu đã nhập. V2 không sử dụng hệ thống migration chuẩn — script này chỉ hỗ trợ nâng cấp **schema V1 mẫu**.

## 4. DBeaver kết nối SQL Server

- Driver: Microsoft SQL Server (DBeaver tự gợi ý tải JDBC driver).
- Host `localhost` ; Port `14333` ; Database `HotelChainPms`.
- Username `sa` ; Password `YourStrong!Passw0rd` (chỉ dành cho demo Docker theo `docker-compose.yml`).
- Nếu có lỗi chứng chỉ tự ký, đặt `trustServerCertificate=true` trong Driver Properties.
- Vào `dbo > Tables`, chọn `hotel_buildings`, `waitlist_entries`, `debt_receivables`, `stock_movements`, `cash_vouchers`, `daily_closes`... để quan sát sau thao tác.
- Chạy `sql/04-check-v2.sql` để xem số bản ghi và tổng tồn/công nợ.

## 5. Tài khoản mẫu

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin toàn chuỗi | `admin@pms.local` | `Demo@12345` |
| Quản lý cơ sở Hà Nội | `manager@pms.local` | `Demo@12345` |
| Lễ tân Hà Nội | `reception@pms.local` | `Demo@12345` |

Tài khoản `admin` chọn được 2 cơ sở, manager/reception chỉ được thao tác cơ sở Hà Nội. Các chức năng quản lý doanh thu/phiếu thu chi giới hạn vai trò backend, không chỉ ẩn nút frontend.

## 6. 4 kịch bản nghiệm thu nhanh

### A. Đặt đoàn → check-in → thu hộ → checkout

Đăng nhập lễ tân → **Khách đoàn** → chọn ngày, đại diện đoàn, 2 phòng khả dụng → Tạo đoàn. Ở bảng đoàn bấm **Thao tác** → **Nhận phòng đoàn**. Mở **Khách đang ở** để xem folio mỗi phòng; tại đoàn nhập tổng số dư và chọn thanh toán chuyển khoản → **Phân bổ thanh toán** → **Trả phòng đoàn**. Kết quả: cả đoàn `CHECKED_OUT`, các phòng `DIRTY` trong buồng phòng.

### B. Hàng đợi → gán phòng

Vào **Phòng hàng đợi**, tạo tên khách/ngày/hạng phòng → **Gán phòng** chọn một phòng khả dụng → **Xác nhận gán**. Kết quả: chờ được chuyển `ASSIGNED`, có reservation liên kết.

### C. Minibar → nhập xuất kho → công nợ

Vào **Nhập xuất tồn kho** → chọn nước suối → nhập số lượng → kiểm tra `stock_movements`; từ folio một khách đang ở thêm minibar → tồn giảm, có movement loại `CONSUMPTION`. Vào **Công nợ / khôi phục**, chọn phòng còn dư → Trả phòng ghi nợ → thanh toán nợ từng lần và kiểm tra số dư giảm.

### D. Thu chi → chuỗi → đối soát

Đăng nhập manager → **Phiếu thu/phiếu chi**, tạo phiếu chi chuyển khoản → xem **Báo cáo chuỗi** và **Hiệu quả đại lý**. Với lễ tân đã mở ca và có thu tiền mặt, manager vào **Đối soát ca**, chọn ca và xem quỹ kỳ vọng/chênh lệch. Vào DBeaver đối chiếu `cash_vouchers`, `payments`, `cash_shifts`, `retail_sales`.

## 7. Các giới hạn phải biết TRƯỚC khi dùng

- **Chưa chạy build .NET/Angular và kiểm thử tích hợp SQL Server trong môi trường tạo mã**: không có dotnet SDK, SQL Server và không truy cập npm registry. Mã TypeScript đã qua kiểm tra parser và script/structure đã qua kiểm tra tĩnh. Bạn phải `dotnet build`, `npm install`, `npm run build` trên Windows trước khi đánh giá chạy thực tế.
- `rate_plans` lưu chính sách giá và khi chọn trong booking sẽ điền giá mẫu. **Không tự động tính giờ phát sinh thực tế, early/late checkin, phụ thu ngày lễ**. `HOURLY`/`DAY_USE` tính một block tại check-in; chưa hỗ trợ luồng thuê giờ chính xác như production.
- Công suất phòng báo cáo là **ước tính theo ngày booking**; doanh thu là charge posted theo timestamp, chưa phải doanh thu phân bổ từng đêm. Hoa hồng đại lý dự kiến, không phải công nợ phải trả chuẩn kế toán.
- `daily_closes` chỉ lưu **snapshot** tại lúc tạo, không khóa sổ/đối soát tự động hoặc backfill doanh thu theo lịch; không phải night audit thực tế.
- `registration-csv` là bảng danh sách khách, không phải biểu mẫu hoặc tích hợp khai báo tạm trú chính thức.
- Chưa tích hợp hóa đơn điện tử, POS restaurant/spa, OTA/channel manager, tổng đài PBX, máy chấm công, thanh toán qua cổng, SMS/email, thuế kế toán kép và tiền lương. Không nên cho tài khoản bên ngoài dùng.
- Booking lưu thông tin khách tối giản, không đáp ứng đầy đủ nghiệp vụ giấy tờ, tuổi, quốc tịch, GDPR/PDPA/Nghị định bảo vệ dữ liệu cá nhân trong môi trường thật.
- V1+V2 chưa chuẩn production về migrations, idempotency, concurrent payment, disaster recovery, phân quyền cấp hành động, PII encryption, test tải, bảo mật. Secret/SA/password mặc định chỉ dùng local.

## 8. Tài liệu & mã nguồn

```text
HotelChainPMS/
  backend/HotelPms.Api/
    Program.cs                 # API V1
    Models.cs                  # CSDL V1 + các DbSet V2
    AdvancedModels.cs          # 10 entity V2
    AdvancedEndpoints.cs       # API 3 giai đoạn
    AdvancedSeeder.cs          # seed dữ liệu V2
  frontend/src/app/
    app.component.*            # Giao diện V1 + menu V2
    advanced.component.*       # Màn hình chức năng V2
    api.service.ts             # HTTP client JWT
  sql/03-v2-upgrade.sql        # Nâng schema từ V1 có sẵn
  sql/04-check-v2.sql          # Kiểm tra dữ liệu trên DBeaver
  docs/V2_FEATURE_MATRIX.md
  docs/V2_FIELD_DICTIONARY.md
  docs/V2_QC_SCENARIOS.md
  docs/V2_APIS.http
  demo-preview/               # Chỉ phản ánh UI offline từ V1, không có tính năng V2/SQL
```

## 9. Tham khảo nghiệp vụ công khai (không sao chép mã)

- https://hbsoft.vn/product.html
- https://hbsoft.vn/help.html

Nếu gặp lỗi đầu tiên khi chạy, gửi log `dotnet build` hoặc `npm run build`; cần xử lý dựa trên lỗi thực tế trước khi coi đây là sản phẩm sẵn sàng vận hành.
