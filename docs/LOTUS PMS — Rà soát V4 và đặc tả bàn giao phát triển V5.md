LOTUS PMS — Rà soát V4 và đặc tả bàn giao phát triển V5
Tài liệu giao việc cho Developer / Technical Lead / QC
Ngày rà soát: 06/11/2026
Repository: Quan03112003/PMS
Nhánh chuẩn đang rà soát: main
Commit HEAD đã kiểm tra: 1e433d46b4266d929675e2882667482ef285efb3
Thông điệp commit: feat: Add V4/V5 testing infrastructure - seed data, test plans, static contracts, QA guide
Mục tiêu: Giữ nguyên V4 làm nền tảng, xử lý các rủi ro còn tồn tại, sau đó phát triển V5 theo nghiệp vụ khách sạn thực tế. Không viết lại hệ thống từ đầu và không làm mất dữ liệu đang có.
0. Cách đọc tài liệu và nguyên tắc đánh giá
Các nhãn được dùng trong tài liệu:
- ĐÃ CÓ TRONG SOURCE: Có entity/API/UI hoặc logic cụ thể trong repository đã rà soát.
- CÓ MỘT PHẦN: Có nền tảng nhưng thiếu một hoặc nhiều quy tắc nghiệp vụ, dữ liệu lịch sử, phân quyền hoặc kiểm thử.
- CHƯA THẤY TRIỂN KHAI: Không tìm thấy entity/API/UI tương ứng trong source tại commit đã nêu.
- CHƯA NGHIỆM THU: Có mã nguồn hoặc tài liệu nhưng chưa có bằng chứng build, runtime, E2E hoặc UAT đủ để xác nhận.
- ĐỀ XUẤT V5: Yêu cầu kỹ thuật/nghiệp vụ bổ sung trong tài liệu này; Developer phải xác nhận với chủ nghiệp vụ trước khi chốt chính sách có ảnh hưởng tiền, đặt phòng hoặc kế toán.
Không được đồng nhất “đã có màn hình”, “đã có API”, “đã build”, “đã test” và “đã nghiệm thu”. Mỗi trạng thái phải có bằng chứng riêng.
Nguồn code được đối chiếu
- Program.cs
- Models.cs
- AdvancedModels.cs
- AdvancedEndpoints.cs
- PricingV4.cs
- NightAuditV4.cs
- app.component.ts
- app.component.html
- advanced.component.ts
- advanced.component.html
- render.yaml
- V4_Change.md
- FEATURE_MATRIX.md
- V5_FEATURE_SPEC.md
- V5_TEST_PLAN.md
PHẦN A — KẾT QUẢ RÀ SOÁT HIỆN TRẠNG V4
1. Kết luận tổng quan
LOTUS PMS hiện có một nền tảng PMS demo/UAT tương đối rộng, gồm Angular 20, ASP.NET Core 10, EF Core 10 và hỗ trợ cấu hình SQL Server hoặc PostgreSQL. Repository đã có nhiều nghiệp vụ đặt phòng, lễ tân, khách đoàn, hàng đợi, folio, thu tiền, kho, công nợ, báo cáo và pricing V4.

Tuy nhiên, tại commit được rà soát:
1. V4 chưa đủ bằng chứng để xác nhận nghiệm thu toàn hệ thống hoặc Production-ready. Tài liệu V4 nêu rõ chưa nghiệm thu E2E SQL Server; các báo cáo QA cũ cũng ghi nhận chưa có đủ build/runtime/browser E2E ở môi trường đóng gói.
2. V5 hiện chủ yếu là đặc tả và kế hoạch kiểm thử. Commit mới nhất chỉ thêm tài liệu, test scaffold và SQL seed/verify; không thêm các module nghiệp vụ V5 vào Backend hoặc Angular.
3. Database đích người dùng đang sử dụng là Neon PostgreSQL. Code đã có nhánh cấu hình Npgsql, nhưng vẫn có một số cấu hình/SQL cần sửa và kiểm thử theo PostgreSQL.
4. Có một bí mật kết nối Neon đã bị đưa vào file SQL trong repository công khai. Phải thu hồi/rotate secret ngay; không được tiếp tục sử dụng credential cũ.
5. Không được chạy seed PostgreSQL hiện tại trên database production. File seed có cấu trúc hỏng và file verify có tên bảng sai.
Trạng thái Git tại thời điểm rà soát
Hạng mục	Kết quả
main	HEAD 1e433d46...
develop	Đang ở commit V4 trước đó 8c062dc..., chậm main một commit
feature/v5.1-booking-housekeeping	Nhánh tồn tại, đang trỏ về commit V4 trước đó 8c062dc...; chưa có phần triển khai V5 mới
Commit mới trên main	Thêm 9 file tài liệu/test/seed/verify; không sửa module nghiệp vụ Backend/Frontend
Check status nhìn thấy trên commit	Có trạng thái Vercel Success; không có bằng chứng Render build/deploy success trong status đã đọc
GitHub Actions workflow	Chưa thấy .github/workflows/ci.yml hoặc workflow CI tương đương
frontend/package-lock.json	Có tồn tại; dùng npm ci trong CI được
EF Core Migrations	Chưa thấy thư mục backend/HotelPms.Api/Migrations


Việc cần làm: Tạo PR V5 từ nhánh feature mới dựa trên HEAD main; không đưa code thử nghiệm trực tiếp vào main.
2. Công nghệ và kiến trúc đang có
Thành phần	Hiện trạng source/deployment	Yêu cầu chuẩn hóa
Frontend	Angular 20, TypeScript 5.9, RxJS 7.8	Giữ Angular; bổ sung typed models, feature structure, UI validation và Playwright
Backend	ASP.NET Core 10, Minimal API, EF Core 10	Giữ .NET 10; tách endpoint/service/domain theo module, tránh tiếp tục phình Program.cs
Database	EF provider có UseSqlServer() và UseNpgsql()	Neon PostgreSQL là database cho môi trường Vercel/Render; hỗ trợ SQL Server chỉ khi có test matrix riêng
Hosting Frontend	Vercel, frontend/vercel.json rewrite /api/* sang Render	Preview deployment cho PR; Production deploy sau CI pass
Hosting Backend	Render Docker service lotus-pms-api	Thêm readiness check thật, không seed demo khi Production khởi động
Database host	Neon PostgreSQL, branch người dùng đang hiển thị là production	Tạo Neon branch/database staging riêng; migration phải test trước production
DB client	DBeaver	Chỉ là công cụ kết nối/xem dữ liệu; không phải DB server
Authentication	JWT + PasswordHasher	Bổ sung policy/action-level permission, rate limit login, lockout/audit


Cấu hình backend hiện có
Program.cs đọc Database:Provider:
- PostgreSQL → UseNpgsql(connection)
- SqlServer → UseSqlServer(connection)
render.yaml đang cấu hình Database__Provider=PostgreSQL, phù hợp với Neon. Tuy nhiên, render.yaml hiện đặt Database__InitializeForDemo="true". Trong Program.cs, khi biến này bật, ứng dụng gọi EnsureCreatedAsync(), DemoSeeder.Seed() và AdvancedSeeder.Seed() kể cả khi ASPNETCORE_ENVIRONMENT=Production.

Yêu cầu: Production không được tự tạo schema/seed demo khi ứng dụng restart. Đổi thành Database__InitializeForDemo=false; chỉ seed dữ liệu demo trong môi trường local/test có kiểm soát.
3. Những module V4 đã có bằng chứng trong source
Models.cs hiện có các DbSet/entity chính sau:

properties, hotel_buildings, rate_plans, waitlist_entries, suppliers, stock_movements, retail_sales, cash_vouchers, debt_receivables, guest_assets, daily_closes, room_types, rooms, guests, reservations, stays, reservation_occupants, services, folio_charges, payments, cash_shifts, agencies, app_users, audit_events.
3.1. Lễ tân / Front Desk
Chức năng	Trạng thái	Bằng chứng/giới hạn
Dashboard lễ tân	Đã có cơ bản	GET /api/dashboard và giao diện dashboard
Sơ đồ phòng, lọc tầng/tìm kiếm	Đã có	Room Map trong app.component.html
Tra cứu phòng trống theo khoảng ngày	Đã có	GET /api/availability, kiểm tra overlap [startDate, endDate)
Đặt phòng khách lẻ	Đã có	POST /api/reservations, transaction Serializable, lock room và kiểm tra trùng lịch
Khách đoàn	Có một phần/khá rõ	AdvancedEndpoints.cs có group-bookings, group occupants, group check-in/pay/checkout; cần E2E để xác nhận toàn luồng
Hàng đợi phòng	Đã có trong source	waitlist_entries, API create/cancel/assign và menu waitlist; FEATURE_MATRIX.md đang ghi ○ nên tài liệu cũ đã lỗi thời
Check-in	Đã có	Yêu cầu booking CONFIRMED, phòng không OOO, housekeeping INSPECTED, không có stay đang hoạt động
Khách đang ở / Folio	Đã có	GET /api/stays, /api/stays/{id}/folio
Thêm dịch vụ/minibar	Đã có cơ bản	Ghi folio charge, trừ tồn kho nếu service theo dõi tồn; có lock service
Thu cọc / thanh toán	Đã có	Deposit và payment sử dụng idempotency key; tiền mặt yêu cầu ca mở
Hoàn cọc trước check-in	Có giới hạn	Có /reservations/{id}/refund-deposit; không đồng nghĩa đã có quy trình hoàn mọi payment sau check-in/checkout
Check-out	Đã có nhưng rule còn cứng	Yêu cầu tổng charge bằng chính xác tổng payment; sau checkout phòng chuyển DIRTY; chưa có quy trình chấp nhận công nợ/hoàn số dư tổng quát trong endpoint checkout thường
Đổi phòng	Có cơ bản	Chuyển room, đánh DIRTY phòng cũ, audit event; chưa có bảng lịch sử chuyển phòng chi tiết và lý do/giá trước-sau
Gia hạn	Đã có	API kiểm tra xung đột phòng; cần test biên thời gian và giá phát sinh
Tài sản khách	Đã có cơ bản	GuestAsset nhận/trả tài sản
Ca thu ngân	Đã có cơ bản	Mở/đóng ca; cần đối soát và giao ca chi tiết


3.2. Quản lý khách sạn
Chức năng	Trạng thái	Giới hạn
Quản lý cơ sở	Đã có	Scope theo property, Chain Admin xem toàn chuỗi
Hạng phòng/phòng/tòa nhà	Đã có	CRUD cơ bản, gán building
Bảng giá giờ/ngày/đêm	Đã có	RatePlan cơ bản; chưa gắn snapshot cố định vào từng reservation
Dịch vụ/minibar	Đã có	Giá và tồn kho cơ bản; thiếu workflow kiểm kê/ảnh/định mức và lịch sử đầy đủ
Nhà cung cấp/kho	Có một phần	Supplier, StockMovement; chưa phải hệ thống mua hàng/phiếu nhập/kho nhiều vị trí hoàn chỉnh
Bán hàng ngoài phòng	Đã có cơ bản	RetailSales
Phiếu thu/chi	Đã có cơ bản	CashVoucher; cần kế toán kiểm tra quy tắc sổ quỹ
Công nợ	Đã có cơ bản	DebtReceivable, thu nợ/checkout-debt/restore; cần test đầy đủ luồng kế toán
Người dùng/vai trò	Có	JWT, role, property scope; chưa đủ permission theo từng hành động
Nhật ký thao tác	Có	AuditEvent cho nghiệp vụ chính; cần chuẩn hóa before/after, correlation ID, tính bất biến


3.3. Báo cáo và chốt ngày
Chức năng	Trạng thái	Giới hạn
Báo cáo doanh thu cơ sở	Có cơ bản	Tách room/service/receipt ở mức hiện tại; chưa xác nhận chuẩn sổ kế toán
Báo cáo chuỗi	Có cơ bản	Có /reports/chain; chưa đủ bộ KPI OCC/ADR/RevPAR có định nghĩa thống nhất
Báo cáo đại lý	Có cơ bản	Commission dự kiến; chưa phải đối soát công nợ đại lý
Báo cáo tồn kho	Có	Cần đối chiếu inventory ledger và tồn thực tế
Đối soát ca	Có cơ bản	Có API reconcile; thiếu bàn giao ca điện tử và quy trình xử lý chênh lệch
Snapshot Daily Close	Có	daily_closes là snapshot tổng hợp
Night Audit	Chưa hoàn chỉnh	NightAuditV4.cs chỉ có GET /night-audit/precheck; chưa có run/lock/reopen, phân bổ charge theo đêm và chặn sửa giao dịch sau khóa


4. Các hành vi nghiệp vụ V4 đang chạy trong source — Developer phải giữ tương thích
4.1. Tạo đặt phòng
Hiện tại POST /api/reservations:
1. Kiểm tra property scope.
2. Kiểm tra ngày nhận nhỏ hơn ngày trả, số người lớn hợp lệ, tên khách không rỗng.
3. Mở transaction Serializable.
4. Khóa room làm điểm tuần tự hóa; kiểm tra room thuộc property, không OOO và đủ trạng thái vệ sinh nếu booking bắt đầu hôm nay/trước hôm nay.
5. Kiểm tra trùng đặt phòng theo khoảng ngày.
6. Tạo Guest và Reservation với trạng thái CONFIRMED.
7. Giá lấy từ request hoặc BaseRate của RoomType.
Thiếu cần xử lý: rate plan chưa gắn cố định vào Reservation; chưa có Booking Note có danh mục; chưa có hold tạm; chưa có deadline/nhắc cọc; request hiện tạo guest mới nên cần thiết kế reuse/merge hồ sơ khách có kiểm soát.
4.2. Check-in
Hiện tại:
- Chỉ nhận booking CONFIRMED.
- Phòng phải không OOO và INSPECTED.
- Không được có stay IN_HOUSE khác trên phòng.
- Tạo Stay và ghi charge phòng vào Folio.
- Với OVERNIGHT, code tính số đêm từ CheckOutDate - CheckInDate và ghi toàn bộ tiền phòng vào folio ngay lúc check-in.
- Với DAY_USE/`HOURLY`, code hiện ghi một gói prepaid; chưa tự tính thời gian thực tế/quá giờ tự động trong luồng check-in.
V5 cần quyết định: Khi triển khai Night Audit thật, không được đồng thời ghi trùng toàn bộ tiền phòng ở check-in và ghi lại charge theo từng đêm. Phải chọn một mô hình kế toán thống nhất: charge theo đêm do Night Audit hoặc charge toàn kỳ theo policy; mọi thay đổi phải có migration và test số dư.
4.3. Thanh toán và check-out
Hiện tại:
- Payment yêu cầu Amount > 0, phương thức CASH/TRANSFER/CARD, idempotency UUID.
- Tiền mặt cần ca thu ngân đang mở.
- Check-out thường yêu cầu SUM(charges) == SUM(payments).
- Khi checkout, Stay và Reservation chuyển CHECKED_OUT, phòng chuyển DIRTY.
V5 cần bổ sung: xử lý thanh toán thừa/thiếu có policy rõ ràng; hoàn tiền có liên kết giao dịch gốc; duyệt hoàn tiền; phiếu điều chỉnh bất biến; không sửa hoặc xóa payment/charge gốc.
4.4. Chuyển phòng
Hiện tại:
- Chỉ chuyển cho Stay đang IN_HOUSE.
- Phòng đích phải cùng property, không OOO, INSPECTED, không trùng lịch.
- Phòng cũ thành DIRTY; Stay và Reservation cập nhật room mới.
- Audit event ghi ID phòng cũ/mới.
V5 cần thêm: lý do chuyển, người yêu cầu/người duyệt, thời điểm hiệu lực, giá trước/sau, phí nâng hạng/hạ hạng, lịch sử không thể sửa/xóa và test concurrency.
4.5. Housekeeping
Hiện tại giao diện cập nhật trạng thái DIRTY → CLEAN → INSPECTED qua PATCH /rooms/{id}/housekeeping.

V5 cần thêm: task giao cho nhân viên; trạng thái công việc; người dọn/người kiểm tra; checklist; thời gian; ảnh bằng chứng minibar; reject/rework; chỉ đưa phòng về INSPECTED sau khi người có quyền duyệt.
4.6. Pricing V4
Hiện có:
- POST /api/pricing/quote: báo giá server-side, không ghi database.
- POST /api/stays/{stayId}/pricing-adjustments: quản lý ghi phụ thu vào folio, có transaction/lock và chặn ghi trùng theo loại phụ thu/reservation.
- GET /api/night-audit/precheck: kiểm tra điều kiện trước snapshot.
Giới hạn được ghi trong V4_Change.md: chưa có tax, service charge, holiday rate, discount, OTA commission trong quote; không có rate plan snapshot ở reservation; một loại phụ thu chỉ ghi một lần cho mỗi reservation; timezone đang quy ước UTC+07, chưa theo từng property.
PHẦN B — BUG/RISK REGISTER PHẢI XỬ LÝ TRƯỚC KHI MỞ RỘNG
5. Danh sách lỗi và rủi ro theo mức độ
ID	Mức	Phát hiện từ source	Yêu cầu Developer
SEC-001	P0	File sql/99-seed-test-data-postgres.sql chứa connection string Neon có thông tin xác thực trong nội dung file	Rotate/revoke credential ngay; thay bằng placeholder/env variable; quét toàn bộ Git history và xử lý lịch sử commit theo quy trình repo owner; kiểm tra audit log Neon
DB-001	P0	render.yaml bật Database__InitializeForDemo=true; Program.cs gọi EnsureCreatedAsync() và seeder khi flag bật dù Production	Tắt demo initialization trên Production; không chạy seeder khi deploy/restart; tách seed demo sang command chỉ dành test
DB-002	P1	Filter unique index IdempotencyKey đang dùng [idempotency_key] IS NOT NULL theo cú pháp SQL Server trong Models.cs	Tạo cấu hình index đúng với PostgreSQL và SQL Server; chạy model creation test trên cả provider được hỗ trợ
DB-003	P1	sql/99-seed-test-data-postgres.sql có DO blocks bị lồng/ghép sai, BEGIN/END không theo cấu trúc hợp lệ và nhiều đoạn bị lặp/lạc vị trí	Không sửa chắp vá; viết lại seed PostgreSQL sạch, idempotent, chạy trên Neon staging mới
DB-004	P1	sql/99-verify-test-data-postgres.sql join service_items, trong khi model map bảng dịch vụ là services	Sửa verify theo schema thực tế; kiểm tra lại mọi table/column bằng information_schema
QA-001	P1	qa/test_v5_static_contracts.py thiếu import json; biểu thức assignment in nightly_fallback := pricing không hợp lệ; nhiều check Tax/Holiday/E-Invoice/Timezone lại tìm trong PricingV4.cs dù module chưa có	Viết lại test hợp lệ; static test chỉ là contract test, không tuyên bố là E2E
QA-002	P1	V5_TEST_PLAN.md tham chiếu qa/v5_unit_tests, v5_api_e2e.py, v5_angular_e2e.py, 11_v5_acceptance_windows.ps1, load_test.js nhưng các file chưa có trong tree đã kiểm tra; tài liệu ghi 15 static checks trong khi file test hiện có 26 check names	Tạo đủ test harness hoặc sửa tài liệu đúng với file thực có; đồng bộ số lượng test; không ghi 10/10 PASS nếu chưa chạy
CI-001	P1	Chưa thấy GitHub Actions workflow trong .github/workflows; commit status thấy Vercel Success nhưng không có bằng chứng Render/DB E2E	Thêm CI workflow cho .NET/Angular/test; Render deploy sau CI pass; production có approval gate
DB-005	P1	Chưa thấy EF Core Migrations trong backend/HotelPms.Api/Migrations	Thiết lập migrations và baseline an toàn cho database đã tạo bằng EnsureCreated; không apply InitialCreate mù lên DB đang có bảng
API-001	P1	/api/health trả status=ok tĩnh; không kiểm tra kết nối DB	Thêm liveness/readiness; readiness phải chạy query DB ngắn và trả unhealthy khi DB lỗi
BIZ-001	P1	Reservation chưa lưu rate_plan_id/snapshot; giá có thể không giữ nguyên khi rate plan thay đổi	Lưu snapshot giá tại thời điểm xác nhận booking; check-in/folio lấy snapshot đã chốt
BIZ-002	P1	Chuyển phòng chưa có bảng room transfer history chi tiết	Lưu old/new room, reason, old/new rate, actor, approver, effective time
BIZ-003	P1	Housekeeping mới cập nhật trạng thái room	Thêm task assignment, checklist, inspection, ảnh minibar và audit
BIZ-004	P1	Night Audit mới có precheck, không có run/lock/reopen	Phát triển quy trình theo business date, chống ghi trùng, phân bổ charge theo đêm và khóa giao dịch
BIZ-005	P1	Có refund-deposit trước check-in nhưng chưa phải refund workflow cho mọi payment/folio	Thêm request/approval/post refund, liên kết payment gốc, idempotency
BIZ-006	P1	DateTime.Today và UTC+07 hardcode trong pricing; Property chưa có timezone	Thêm IANA timezone cho property; phân biệt instant UTC và business date local
DOC-001	P2	FEATURE_MATRIX.md lỗi thời: source có waitlist/group booking nhưng matrix còn đánh dấu backlog; kiến trúc/field dictionary một số chỗ chỉ mô tả SQL Server	Đồng bộ matrix theo source, phân biệt code/UI/test/acceptance và PostgreSQL deployment
DOC-002	P2	QA_TEST_GUIDE.md còn mô tả website Vercel là landing page và quy trình local SQL Server; không còn khớp môi trường người dùng đang triển khai Vercel/Render/Neon	Viết lại hướng dẫn theo môi trường thực tế, có mục riêng local SQL Server nếu còn hỗ trợ
DOC-003	P2	V5_FEATURE_SPEC.md nhảy heading từ 2.2 sang 2.5, thiếu mục 2.3/2.4, lặp heading 2.9; một số field mô tả theo SQL Server (NVARCHAR, DATETIME2, JSON) và tên bảng số ít reservation trong khi DB đích là PostgreSQL và convention thực tế là reservations/snake_case	Chuẩn hóa lại đặc tả V5 theo schema PostgreSQL; chốt naming/types; mỗi feature phải có nghiệp vụ, field dictionary, API và acceptance criteria thống nhất
DOC-004	P2	ARCHITECTURE.md và TEN_TRUONG_DU_LIEU.md còn mô tả chủ yếu SQL Server; FEATURE_MATRIX.md chưa cập nhật waitlist/group endpoints/UI đang có	Cập nhật tài liệu từ code HEAD thực tế; phân biệt có code, có UI, test pass và nghiệm thu
UI-001	P2	Footer/notification còn ghi “Lotus PMS v2.0 Demo”, “SQL Server” trong khi cấu hình production đang chọn PostgreSQL	Đồng bộ branding, version, API/database label; không hiển thị tên DB nội bộ cho người dùng cuối
UI-002	P2	UI còn dùng any nhiều và một số thao tác form nằm trực tiếp trong component lớn	Thêm TypeScript interfaces/DTO, typed services, form validation và tách feature theo module
SEC-002	P1	Demo login/password được hiển thị trong UI/tài liệu test	Chỉ bật trong demo/staging; tuyệt đối không tạo tài khoản/mật khẩu demo trên production thật


5.1. Ghi chú bảo mật khẩn cấp
- Không chép lại connection string Neon đã lộ vào bất kỳ tài liệu, ticket hoặc chat nào.
- Rotate credential ở Neon và cập nhật Render Environment bằng giá trị mới.
- Xóa secret khỏi file hiện tại và xử lý Git history; việc xóa một dòng trong commit mới không thu hồi secret đã xuất hiện ở commit cũ.
- Bật secret scanning (ví dụ Gitleaks) trong CI và pre-commit nếu phù hợp.
- Kiểm tra Neon audit log/connection log để phát hiện truy cập không nhận diện được.
5.2. Ghi chú schema/migration
Hiện tại database có thể đã được tạo bởi EnsureCreatedAsync(). Không được chạy dotnet ef database update với InitialCreate lên database hiện hữu trước khi có kế hoạch baseline và kiểm tra schema.

Developer phải:
1. Tạo Neon branch staging clone từ production hoặc backup có kiểm soát.
2. Dump/introspect schema hiện có; lưu snapshot cấu trúc và số lượng bản ghi các bảng chính.
3. Tạo migration baseline phù hợp với schema hiện hữu; migration baseline phải được review để không tạo lại bảng đã tồn tại.
4. Test upgrade trên bản clone và đối chiếu dữ liệu trước/sau.
5. Chỉ production migration sau khi có backup/restore evidence và phê duyệt.
PHẦN C — QUY ƯỚC KIẾN TRÚC VÀ CODE CHUẨN
6. Quyết định kỹ thuật
1. V4 là baseline. Không xóa module cũ và không đổi hợp đồng API đang được Angular gọi nếu chưa có compatibility plan.
2. Neon PostgreSQL là DB của deployment Vercel/Render hiện tại. Mọi migration V5 phải viết và test cho PostgreSQL. Nếu cần giữ SQL Server local, phải có test matrix riêng cho hai provider.
3. Không dùng EnsureCreated cho production. Dùng EF Core Migrations; demo seed chạy bằng lệnh/flag riêng trong môi trường demo.
4. Không đưa logic nghiệp vụ vào Angular. Angular chỉ nhập liệu, hiển thị và gọi API; giá, quyền, trạng thái, chống trùng, tính tiền phải được Backend kiểm tra.
5. Không update/delete giao dịch tài chính đã ghi sổ. Điều chỉnh bằng bút toán/phiếu đảo có liên kết giao dịch gốc và audit.
6. Mọi thao tác thay đổi trạng thái quan trọng phải transaction + authorization + audit. Những endpoint thu tiền/hoàn tiền phải idempotent.
7. Mọi query nghiệp vụ phải lọc theo property scope ở server. Không tin propertyId do UI gửi nếu user không có scope.
8. Không chạy test ghi dữ liệu trên Neon branch production. Dùng branch staging/`dev` hoặc database test độc lập.
7. Quy ước đặt tên và kiểu dữ liệu
Lớp	Quy ước	Ví dụ
UI label	Tiếng Việt rõ nghĩa	“Ngày nhận phòng”, “Lý do chuyển phòng”
Angular TypeScript	camelCase cho property/biến; PascalCase cho class/interface	checkInDate, ReservationSummary
JSON request/response	camelCase	propertyId, ratePlanSnapshot
C# class/record	PascalCase	Reservation, CreateReservationRequest
C# property	PascalCase	CheckInDate, RatePlanId
Method	PascalCase	CreateReservationAsync
SQL table	snake_case, số nhiều	reservations, housekeeping_tasks
SQL column	snake_case	created_at_utc, deposit_due_at_utc
API route	kebab-case cho resource nhiều từ	/api/night-audit, /api/booking-note-types
Test ID	theo module + thứ tự	BK-V5-TC-001, HK-V5-TC-001


7.1. Kiểu dữ liệu PostgreSQL chuẩn
Nghiệp vụ	PostgreSQL	Ghi chú
Khóa chính	integer (giữ tương thích ID V4)	Chỉ nâng lên bigint nếu có migration plan rõ
Tiền	numeric(18,2)	Không dùng float/double cho tiền
Phần trăm/tỷ lệ	numeric(7,4)	Ví dụ 15.0000%
Ngày nghiệp vụ	date	Ví dụ check_in_date, business_date
Thời điểm thực tế	timestamp with time zone (timestamptz)	Lưu UTC; ở C# ưu tiên DateTimeOffset
Giờ check-in/out chuẩn của khách sạn	time	Kết hợp với properties.time_zone_id
Số lượng kho	numeric(18,3) nếu hỗ trợ số lẻ; nếu chỉ đơn vị nguyên thì integer	Thống nhất với unit của hàng hóa
Trạng thái	varchar(30) + CHECK constraint	Không hardcode enum DB nếu cần mở rộng dễ
Snapshot/payload ngoài lõi	jsonb	Chỉ dùng cho snapshot hoặc payload; trường nghiệp vụ cần query nên có cột riêng
Boolean	boolean	Không dùng 0/1 cho PostgreSQL


Không dùng NVARCHAR, DATETIME2, BIT, IDENTITY, TOP, dbo. hoặc cú pháp SQL Server trong migration PostgreSQL.
8. Cấu trúc source đề xuất
Không bắt buộc refactor toàn bộ V4 ngay lập tức. Với code mới, không tiếp tục nhồi mọi endpoint vào Program.cs/component khổng lồ. Refactor từng module và giữ route cũ tương thích.
Backend đề xuất
backend/HotelPms.Api/
  Api/
    Endpoints/
      ReservationsEndpoints.cs
      ReservationHoldsEndpoints.cs
      RoomTransferEndpoints.cs
      HousekeepingEndpoints.cs
      NightAuditEndpoints.cs
      FinanceEndpoints.cs
      ReportsEndpoints.cs
    Contracts/
      Requests/
      Responses/
  Application/
    Reservations/
    Pricing/
    Housekeeping/
    Finance/
    NightAudit/
    Reporting/
  Domain/
    Entities/
    Enums/
    Policies/
    Services/
  Infrastructure/
    Persistence/
      PmsDbContext.cs
      Configurations/
      Migrations/
      Seed/
    Integrations/
  Common/
    Errors/
    Authorization/
    Observability/
Nếu team chưa muốn tách nhiều project, có thể đặt các folder trên trong một project .csproj; nhưng phải giữ ranh giới Endpoint → Application Service → DbContext/Repository rõ ràng.
Frontend đề xuất
frontend/src/app/
  core/
    auth/
    api/
    guards/
    interceptors/
  shared/
    components/
    models/
    validators/
  features/
    dashboard/
    reservations/
    front-desk/
    housekeeping/
    cashier/
    inventory/
    finance/
    reports/
    administration/
- Dùng interface/type cụ thể, hạn chế any.
- Không tự tính tiền trong template/component.
- Form phải validate cả client và server; server là nguồn quyết định cuối.
- Loading/error/empty state phải có trên tất cả danh sách.
- Các thao tác tài chính cần confirm dialog và hiển thị thông tin giao dịch trước khi submit.
9. Quy tắc API chung
- Giữ /api/... hiện tại để không phá V4; chỉ thêm route mới. Nếu thay đổi contract thì có versioning/compatibility plan.
- Dùng request DTO riêng, response DTO riêng; không trả trực tiếp EF entity chứa trường nội bộ/PII.
- Validate ở server: required, length, range, enum/status, thuộc đúng property, quyền và trạng thái hiện tại.
- Dùng ProblemDetails hoặc error envelope thống nhất, không trả lỗi stack trace cho client.
- Status code tối thiểu: 200/201 thành công, 400 request sai, 401 chưa đăng nhập, 403 không có quyền, 404 không tồn tại, 409 xung đột nghiệp vụ/concurrency, 422 dữ liệu hợp lệ cú pháp nhưng không hợp lệ nghiệp vụ nếu team chọn dùng.
- Danh sách cần page, pageSize, sort, filter; giới hạn pageSize tối đa. Không chỉ Take(250) cố định.
- Write endpoint có tiền hoặc tạo tài nguyên quan trọng cần idempotency key khi client có thể retry.
- Gắn correlationId/trace ID vào log và response để QC/IT truy vết.
- Không ghi password, JWT, connection string, số giấy tờ hoặc dữ liệu PII đầy đủ vào log.
PHẦN D — ĐẶC TẢ NGHIỆP VỤ V5 CẦN DEVELOPER TRIỂN KHAI
10. Module Booking nâng cao
10.1. Booking Note dạng danh mục — ưu tiên V5.1
Mục tiêu: Lễ tân ghi nhận yêu cầu booking có cấu trúc để lọc, thống kê, bàn giao và không phụ thuộc một ô ghi chú tự do duy nhất.

Luồng nghiệp vụ:
1. Lễ tân chọn cơ sở, ngày đến/ngày đi, loại phòng/phòng.
2. Hệ thống kiểm tra phòng khả dụng.
3. Lễ tân chọn Booking Note type từ danh mục được cấu hình; nhập nội dung chi tiết nếu loại note cho phép.
4. Các note bắt buộc phải được nhập trước khi xác nhận booking.
5. Sau khi booking được xác nhận, note được lưu cùng reservation và audit người tạo/thời điểm.
6. Khi chỉnh sửa note, không ghi đè mất lịch sử; lưu phiên bản hoặc audit before/after.
Danh mục gợi ý ban đầu (Manager có thể bật/tắt/đổi tên theo cơ sở):
- BED_PREFERENCE — yêu cầu loại giường.
- ARRIVAL_TIME — giờ dự kiến đến.
- SPECIAL_REQUEST — yêu cầu đặc biệt.
- ACCESSIBILITY — hỗ trợ tiếp cận.
- CHILD_EQUIPMENT — nôi/giường trẻ em.
- FOOD_ALLERGY — lưu ý dị ứng thực phẩm (nếu khách tự cung cấp; phân quyền xem hạn chế).
- COMPANY_REFERENCE — mã công ty/hợp đồng.
- PAYMENT_GUARANTEE — bảo lãnh/thanh toán.
- OTHER — nội dung khác.
Bảng đề xuất:
- booking_note_types: id, property_id nullable (null = danh mục hệ thống), code, name, description, is_required, allow_free_text, is_active, sort_order, created_at_utc, updated_at_utc.
- reservation_notes: id, reservation_id, note_type_id, note_text, created_by_user_id, created_at_utc, updated_by_user_id, updated_at_utc, is_deleted (nếu cần ẩn mềm; vẫn giữ audit).
API đề xuất:
- GET /api/booking-note-types?propertyId={id}
- POST /api/booking-note-types (Manager/Chain Admin)
- PATCH /api/booking-note-types/{id}
- GET /api/reservations/{id}/notes
- POST /api/reservations/{id}/notes
- PATCH /api/reservation-notes/{id}
Acceptance:
- Không xác nhận booking nếu loại note bắt buộc còn thiếu.
- Không cho chọn note type đã inactive.
- User chỉ đọc/ghi booking thuộc property scope.
- Thay đổi note có audit.
- Note type có thể cấu hình khác nhau theo property.
10.2. Giữ phòng tạm và tự nhả khi hết hạn — ưu tiên V5.1
Mục tiêu: Giữ phòng trong một thời hạn; hiển thị countdown; tự nhả nếu không hoàn tất điều kiện cọc/xác nhận theo policy.

Luồng nghiệp vụ:
1. Lễ tân chọn ngày/phòng/khách và nhấn “Giữ phòng”.
2. Backend kiểm tra xung đột trong transaction; tạo hold có expires_at_utc.
3. Hold có trạng thái ACTIVE; phòng bị chặn bán trong khoảng hold còn hiệu lực.
4. Khi khách hoàn tất cọc/xác nhận, hold được convert sang Reservation trong cùng transaction; hold chuyển CONVERTED.
5. Khi hết hạn mà chưa convert, job nền chuyển hold sang EXPIRED; phòng được mở bán trở lại.
6. Nếu khách hủy trước hạn, hold chuyển RELEASED và lưu lý do/người thao tác.
7. Job có thể chạy lặp; cùng một hold chỉ được release một lần.
Bảng đề xuất reservation_holds:
- id, property_id, room_id, guest_id nullable, guest_name, guest_phone, check_in_date, check_out_date.
- status (ACTIVE, CONVERTED, EXPIRED, RELEASED, CANCELLED).
- held_at_utc, expires_at_utc, released_at_utc, release_reason.
- converted_reservation_id nullable, created_by_user_id, created_at_utc, updated_at_utc, idempotency_key.
API đề xuất:
- POST /api/reservation-holds
- GET /api/reservation-holds?propertyId=&status=
- POST /api/reservation-holds/{id}/convert
- POST /api/reservation-holds/{id}/release
Acceptance:
- Hold đang ACTIVE và chưa hết hạn phải được tính là phòng không khả dụng.
- Hold EXPIRED/RELEASED/CONVERTED không tiếp tục block availability.
- Hai lễ tân không thể hold cùng phòng cùng khoảng ngày tại cùng thời điểm.
- Job expire chạy lặp không tạo bản ghi/trạng thái trùng.
- Hiển thị countdown theo thời gian máy chủ, không phụ thuộc đồng hồ trình duyệt.
10.3. Quản lý đặt cọc và cảnh báo hạn cọc — ưu tiên V5.1
Luồng nghiệp vụ:
1. Mỗi booking có deposit_required_amount, deposit_received_amount tính từ payment ledger, deposit_due_at_utc và policy.
2. Màn hình booking hiển thị số tiền phải cọc, đã cọc, còn thiếu, hạn cọc và trạng thái.
3. Job tạo thông báo trước hạn theo cấu hình: ví dụ 7 ngày, 3 ngày và ngày đến hạn; các mốc phải cấu hình theo policy, không hardcode.
4. Gửi thông báo phải có log, retry, trạng thái gửi và idempotency theo booking + mốc + kênh.
5. Quá hạn: hệ thống đánh dấu cảnh báo; tự hủy/nhả phòng chỉ khi policy được chủ nghiệp vụ duyệt. Không tự hủy booking đã xác nhận nếu chưa có rule rõ.
Trường đề xuất bổ sung vào reservations: deposit_required_amount numeric(18,2), deposit_due_at_utc timestamptz, deposit_policy_code varchar(40), deposit_status varchar(20).

Số cọc đã nhận phải tính từ payments loại DEPOSIT/REFUND, không tự lưu số đã nhận thành một giá trị độc lập dễ lệch sổ.

Bảng thông báo đề xuất: notification_dispatches gồm id, property_id, reservation_id, notification_type, scheduled_at_utc, sent_at_utc, channel, status, provider_message_id, attempt_count, last_error, idempotency_key.
10.4. Lịch sử Booking và Room Availability Filter
- Danh sách booking phải lọc theo property, trạng thái, nguồn booking, ngày đến/ngày đi, tên/số điện thoại/mã booking, agency và loại phòng.
- Bộ lọc phòng phải phân biệt: trống sạch, trống chưa dọn, có khách, sắp đến, bảo trì/OOO, đang giữ tạm.
- Có audit lịch sử trạng thái; không chỉ hiển thị trạng thái hiện tại.
- Mọi filter server-side có phân trang và kiểm tra property scope.
11. Module Chuyển phòng — ưu tiên V5.1
Mục tiêu: Lưu lịch sử nghiệp vụ và tiền, không chỉ cập nhật stay.room_id.

Luồng nghiệp vụ:
1. Lễ tân mở Stay và chọn “Chuyển phòng”.
2. Hệ thống tải các phòng trong cùng property còn khả dụng từ thời điểm chuyển đến checkout, không OOO và đã INSPECTED.
3. Nhân viên bắt buộc chọn lý do: khách yêu cầu, nâng hạng, hạ hạng, bảo trì phòng cũ, điều phối vận hành, lý do khác.
4. Backend tính chênh lệch giá theo policy. Nếu không đổi giá, phải lưu rõ old_rate = new_rate và lý do.
5. Nếu thay đổi giá/loại phòng, yêu cầu Manager duyệt theo rule; không âm thầm thay tiền.
6. Trong một transaction: lock reservation và cả hai room theo thứ tự ID cố định; kiểm tra availability; ghi transfer history; cập nhật stay/reservation; ghi adjustment nếu có; đánh old room DIRTY; audit.
7. Màn hình lịch sử hiển thị phòng cũ/mới, thời điểm, lý do, người thao tác, giá trước/sau và khoản điều chỉnh.
Bảng room_transfer_histories:
- id, property_id, reservation_id, stay_id.
- old_room_id, new_room_id.
- reason_code, reason_note.
- old_rate numeric(18,2), new_rate numeric(18,2), rate_delta numeric(18,2).
- effective_at_utc, created_by_user_id, approved_by_user_id nullable, approved_at_utc nullable.
- created_at_utc, correlation_id.
API đề xuất:
- POST /api/stays/{stayId}/transfers
- GET /api/stays/{stayId}/transfers
- GET /api/room-transfers?propertyId=&from=&to=&roomId=
Acceptance:
- Không chuyển sang phòng cùng stay đang ở/OOO/dirty.
- Không tạo overlap booking/stay trong khoảng còn lại.
- Hai request chuyển đồng thời không thể chiếm cùng phòng mới.
- Không xóa lịch sử; điều chỉnh phải thêm event/chứng từ.
- Thao tác thay giá phải có lý do và quyền phù hợp.
12. Module Buồng phòng — ưu tiên V5.1
Mục tiêu: Chuyển từ thay đổi trạng thái phòng thủ công thành quy trình công việc có giao nhiệm vụ và nghiệm thu.

Workflow chuẩn:

DIRTY → TASK_ASSIGNED → IN_PROGRESS → CLEANED → INSPECTION_PENDING → APPROVED → INSPECTED

Nếu kiểm tra không đạt:

INSPECTION_PENDING → REJECTED → IN_PROGRESS → CLEANED → INSPECTION_PENDING

Room vẫn không được bán/nhận khách cho đến khi đạt INSPECTED và không OOO.

Luồng nghiệp vụ:
1. Khi check-out, hệ thống tự tạo Housekeeping Task loại CHECKOUT_CLEANING cho phòng.
2. Trưởng bộ phận/Manager phân công nhân viên; lưu người giao, người nhận và hạn hoàn thành.
3. Nhân viên bắt đầu task, đánh checklist, ghi nhận đồ thất lạc/hư hỏng/minibar.
4. Nhân viên hoàn tất dọn và đính kèm ảnh nếu policy yêu cầu.
5. Supervisor/Inspector kiểm tra. Nếu không đạt, ghi lý do reject và giao làm lại.
6. Chỉ khi approved, Room.housekeeping_status mới chuyển INSPECTED.
7. Tất cả bước có thời gian và actor.
Bảng housekeeping_tasks:
- id, property_id, room_id, stay_id nullable, reservation_id nullable.
- task_type (CHECKOUT_CLEANING, STAYOVER_CLEANING, INSPECTION, MINIBAR_CHECK, MAINTENANCE_CHECK).
- priority, status, assigned_to_user_id, assigned_by_user_id.
- created_at_utc, assigned_at_utc, started_at_utc, cleaned_at_utc, inspected_at_utc, due_at_utc.
- inspected_by_user_id, rejection_reason, note, version.
Bảng housekeeping_task_items: id, task_id, checklist_code, label_snapshot, is_required, result, note, completed_by_user_id, completed_at_utc.

Bảng housekeeping_task_media: id, task_id, object_key, content_type, file_size, checksum, uploaded_by_user_id, uploaded_at_utc, media_type.

Ảnh lưu ở object storage; database chỉ lưu metadata/object key. Không lưu ảnh Base64 trong bảng nghiệp vụ. Kiểm tra loại file, size, quyền truy cập, virus/malware policy nếu hạ tầng hỗ trợ.

Minibar:
- Khi checkout hoặc kiểm phòng cuối, tạo checklist minibar.
- Ghi số lượng ban đầu, số lượng còn, số lượng tiêu thụ, đơn giá snapshot, ảnh bằng chứng theo policy.
- Nhân viên không được tự sửa giá bán; consumption tạo folio_charge và stock_movement trong một transaction.
- Chống ghi minibar hai lần cùng task/item bằng unique/idempotency.
Acceptance:
- Nhân viên housekeeping chỉ cập nhật task thuộc scope/assignment.
- Nhân viên dọn không tự approve task của chính mình nếu policy yêu cầu phân tách nhiệm vụ.
- Không nhận khách khi task chưa approved.
- Ảnh bắt buộc phải được kiểm tra trước khi hoàn tất task thuộc loại yêu cầu ảnh.
- Audit đầy đủ assigned/start/complete/approve/reject.
13. Module Rate Plan, giá và phụ thu — ưu tiên V5.1/V5.2
13.1. Rate Plan snapshot
Khi tạo/xác nhận Reservation phải chốt giá và điều kiện áp dụng tại thời điểm booking. Đề xuất thêm:
- reservations.rate_plan_id nullable FK.
- reservations.rate_plan_snapshot jsonb hoặc các cột snapshot chuẩn hóa.
- reservations.currency_code, pricing_snapshot_version.
- Snapshot gồm ratePlanCode, ratePlanName, bookingMode, basePrice, includedHours, extraHourPrice, taxRuleVersion, capturedAtUtc.
Khi quản lý sửa RatePlan tương lai, booking cũ không đổi giá. Chỉ cho đổi rate plan trước check-in theo quyền và lưu history.
13.2. Pricing rule
- Tính tiền ở Backend; tất cả số tiền tính bằng decimal/`numeric`, không dùng floating point.
- Hỗ trợ mode OVERNIGHT, DAY_USE, HOURLY.
- Quy tắc nhận sớm/trả muộn/quá giờ dùng cấu hình property/rate plan và làm tròn theo rule đã duyệt.
- Giá ngày thường/cuối tuần/ngày lễ, giá theo mùa và occupancy phải là rule có effective date; không ghi cứng tỷ lệ trong code.
- Giá sàn/giá trần và quyền override do Manager/Chain Admin cấu hình; mọi override có lý do, người duyệt và audit.
- Tách room revenue, service charge, tax, discount, OTA commission; không cộng gộp sai vào một field tổng.
Bảng có thể cần: rate_plan_calendar, rate_rules, holiday_rates, tax_rules, discount_rules, pricing_approval_requests.
14. Module Tài chính, folio, hoàn tiền và điều chỉnh — ưu tiên V5.2
14.1. Nguyên tắc sổ cái
- Payment/FolioCharge gốc là append-only; không sửa/xóa giao dịch đã post.
- Sai giao dịch phải lập adjustment/reversal/refund có tham chiếu giao dịch gốc.
- Mọi thao tác tài chính có idempotency_key, created_by, created_at_utc, property_id, reservation_id và correlation ID.
- Số tiền hoàn không vượt số tiền đã thu còn được phép hoàn.
- Refund phải phân biệt: yêu cầu, duyệt, thực chi, thất bại/hủy; không ghi là đã hoàn chỉ vì người dùng nhấn nút.
- Quy tắc duyệt hai cấp theo ngưỡng tiền phải cấu hình và được chủ nghiệp vụ phê duyệt.
14.2. Adjustment Voucher
Bảng adjustment_vouchers:
- id, code, property_id, reservation_id, original_charge_id nullable.
- kind (CORRECTION, REVERSAL, REFUND, WAIVER, MANUAL_ADJUSTMENT).
- amount_delta numeric(18,2), reason_code, reason_text.
- status (PENDING, APPROVED, REJECTED, POSTED, VOIDED).
- created_by_user_id, approved_by_user_id, posted_by_user_id.
- created_at_utc, approved_at_utc, posted_at_utc, idempotency_key.
Luồng: tạo phiếu → validate → người có quyền duyệt → post bút toán ngược/điều chỉnh vào folio → audit. Không được cập nhật charge gốc.
14.3. Payment refund
Đề xuất payment_refunds:
- id, property_id, reservation_id, original_payment_id.
- requested_amount, approved_amount, currency_code, reason_code, reason_text.
- status, requested_by_user_id, approved_by_user_id, processed_by_user_id.
- requested_at_utc, approved_at_utc, processed_at_utc, external_reference, idempotency_key.
Khi refund thành công, tạo ledger entry âm hoặc payment record loại REFUND có liên kết payment gốc. Không sửa số tiền giao dịch cũ.
15. Module Night Audit — ưu tiên V5.2, bắt buộc trước Production
Night Audit không phải snapshot báo cáo. Đây là quy trình chốt ngày kinh doanh và kiểm soát posting.
15.1. Định nghĩa ngày kinh doanh
- Mỗi property có time_zone_id IANA (ví dụ Asia/Ho_Chi_Minh) và business_day_cutoff_time.
- business_date được tính theo timezone/cutoff của property, không lấy DateTime.Today của server.
- Lưu thời điểm sự kiện dưới UTC (timestamptz); lưu ngày kinh doanh bằng date.
15.2. Luồng Night Audit
1. Manager/Accounting chọn property và business date.
2. Chạy precheck: ca đang mở, booking đến chưa nhận, khách đến hạn trả chưa checkout, phòng OOO, folio mất cân bằng, payment pending, charge thiếu, dữ liệu minibar/task tồn.
3. Hiển thị danh sách cảnh báo có link đến đối tượng; không chỉ hiển thị một con số.
4. Người có quyền chạy audit; backend mở transaction/lock cho property + business date.
5. Tính các room-night charge theo rate snapshot cho từng stay thuộc business date.
6. Mỗi dòng charge có nguồn night_audit_run_id, stay_id, business_date, charge type; unique chống chạy trùng.
7. Tính tax/service/discount theo policy effective date.
8. Lưu tổng doanh thu, tiền thu, công nợ, room sold/available, ADR/OCC và snapshot.
9. Sau review, người có quyền lock ngày. Tất cả write endpoint liên quan ngày đã khóa phải trả conflict và không ghi giao dịch.
10. Mở lại ngày chỉ Chain Admin hoặc role được duyệt, bắt buộc lý do; audit đầy đủ; chạy bù/đảo charge theo policy, không xóa run gốc.
Bảng đề xuất:

night_audit_runs: id, property_id, business_date, status (PRECHECKED, RUNNING, COMPLETED, LOCKED, REOPENED, FAILED), started_at_utc, completed_at_utc, locked_at_utc, locked_by_user_id, reopened_at_utc, reopened_by_user_id, reopen_reason, totals, idempotency_key, version.

night_audit_charge_lines: id, night_audit_run_id, property_id, stay_id, reservation_id, room_id, business_date, charge_type, description_snapshot, quantity, unit_price, amount, created_at_utc; unique theo stay_id + business_date + charge_type hoặc key tương đương.
15.3. Quy tắc bắt buộc
- Không double-charge khi job/request retry.
- Không chạy đồng thời hai audit cho cùng property/date.
- Không lock nếu có blocker theo policy; warning và blocker phải phân biệt.
- Không cho chỉnh charge/payment/checkout thuộc business date đã lock, trừ quy trình reopen được duyệt.
- Phải có reconciliation: tổng charge, tax, payment, refund, outstanding balance, room nights.
- Có thể chạy lại precheck không tạo giao dịch.
- Có test dữ liệu qua đêm, timezone/cutoff, checkout đúng ngày, stay kéo dài qua nhiều ngày và reopen.
API đề xuất:
- Giữ GET /api/night-audit/precheck?propertyId=&businessDate= hiện có.
- POST /api/night-audit/run.
- POST /api/night-audit/{id}/lock.
- POST /api/night-audit/{id}/reopen.
- GET /api/night-audit/runs?propertyId=&from=&to=.
- GET /api/night-audit/runs/{id}.
16. Module Quản lý chuỗi và báo cáo — ưu tiên V5.2/V5.3
16.1. KPI định nghĩa thống nhất
- OCC (Occupancy): Room nights sold / Room nights available × 100%; phòng OOO/không kinh doanh phải được xử lý theo một định nghĩa thống nhất.
- ADR: Room revenue / Rooms sold.
- RevPAR: Room revenue / Rooms available.
- Room Revenue: chỉ doanh thu phòng; không cộng dịch vụ/minibar/thuế nếu báo cáo định nghĩa không yêu cầu.
- Cash Collection: tiền thực thu trong kỳ; không đồng nghĩa doanh thu ghi nhận.
- Outstanding Debt: tổng khoản phải thu chưa thu, trừ khoản đã thu/đảo.
Mọi báo cáo phải có ngày kinh doanh/timezone, property scope, nguồn dữ liệu và định nghĩa công thức. Không để UI tính lại KPI độc lập khác Backend.
16.2. Dashboard chuỗi
- Bộ lọc ngày, vùng, property, trạng thái hoạt động.
- KPI tổng chuỗi và drill-down từng khách sạn.
- OCC, ADR, RevPAR, doanh thu phòng, dịch vụ, bán lẻ, tiền thu, công nợ.
- So sánh kỳ trước; cảnh báo occupancy thấp/revenue thấp theo ngưỡng cấu hình.
- Báo cáo khách theo quốc tịch và chi nhánh; chỉ tổng hợp dữ liệu cá nhân theo quyền.
- Export XLSX/PDF phải dùng cùng dữ liệu với dashboard.
16.3. Cảnh báo
- Ngưỡng công suất thấp và doanh thu thấp do Chain Admin/Manager cấu hình theo property và thời gian.
- Cảnh báo tồn kho dưới định mức.
- Cảnh báo booking đến hạn cọc.
- Cảnh báo phòng dirty lâu, task housekeeping quá hạn, room out-of-order kéo dài.
- Có trạng thái NEW/ACKNOWLEDGED/RESOLVED, người nhận, thời gian và lịch sử; tránh gửi lặp vô hạn.
17. Module Giao ca điện tử — ưu tiên V5.2
Bổ sung sổ giao ca gắn với cash shift và từng bộ phận:
- Ca giao, ca nhận, người giao, người nhận, thời điểm.
- Tiền mặt đầu/cuối ca, tiền thực nhận, chênh lệch.
- Booking khách đến/đi chưa xử lý, khách VIP, yêu cầu đặc biệt, phòng bảo trì, task buồng phòng tồn, công nợ/folio chờ xử lý.
- Tệp/ảnh đính kèm nếu cần.
- Người nhận xác nhận; không cho sửa nội dung sau khi đã xác nhận, chỉ thêm bổ sung/audit.
Bảng đề xuất shift_handovers, shift_handover_items, shift_handover_acknowledgements.
18. Tích hợp OTA/Hotelink, Booking và hệ thống ngoài
- Không xây một ứng dụng Booking riêng nếu nghiệp vụ đã định hướng tích hợp Inxify/Hotelink.
- YC-BS-01 phải được hiểu là luồng tích hợp Channel Manager/Hotelink với A25 PMS; không đánh đồng với chức năng đặt phòng nội bộ.
- Chỉ triển khai khi có API specification, sandbox, credentials riêng, mapping property/room type/rate plan, quy tắc tồn phòng, mapping booking status, cancellation, modification, webhook/polling, retry và đối soát.
- Bắt buộc idempotency và external reference để không tạo booking trùng khi OTA gửi lại webhook.
- Không tự giả lập OTA “đã tích hợp” bằng cách chỉ lưu booking_source='OTA'.
- Hóa đơn điện tử chỉ triển khai sau khi chọn nhà cung cấp, có API contract/sandbox và nghiệp vụ thuế được Kế toán xác nhận.
- Hotline/PBX là đầu việc hạ tầng viễn thông riêng; không đưa thành module PMS V5 nếu chưa có yêu cầu tích hợp PBX cụ thể.
PHẦN E — ĐẶC TẢ BẢNG DỮ LIỆU VÀ API V5
19. Bảng dữ liệu mới/điều chỉnh tối thiểu
Đây là đề xuất target schema. Developer cần đối chiếu schema hiện có trên Neon staging trước khi viết migration, tránh tạo bảng trùng.
Bảng	Mục đích	Trường chính cần có
booking_note_types	Danh mục note	id, property_id, code, name, is_required, allow_free_text, is_active, sort_order
reservation_notes	Note theo booking	id, reservation_id, note_type_id, note_text, created_by_user_id, timestamps
reservation_holds	Giữ phòng tạm	id, property_id, room_id, guest info, date range, status, expires_at_utc, converted reservation, idempotency
room_transfer_histories	Lịch sử chuyển phòng	reservation_id, stay_id, old/new room, reason, old/new rate, actor, approver, effective time
housekeeping_tasks	Task dọn/kiểm phòng	property, room, stay, type, priority, status, assignee, timestamps, inspector, rejection reason
housekeeping_task_items	Checklist	task, checklist code, required flag, result, note, actor, completed time
housekeeping_task_media	Ảnh bằng chứng	task, object key, media type, content type, size, checksum, uploader
notification_dispatches	Log nhắc cọc/cảnh báo	property, reservation, type, scheduled/sent time, channel, status, attempts, external ID, idempotency
night_audit_runs	Header Night Audit	property, business date, status, totals, locked/reopen fields, actor, idempotency
night_audit_charge_lines	Charge theo đêm	run, stay, reservation, room, business date, type, price, amount, unique source key
adjustment_vouchers	Phiếu điều chỉnh	code, property, reservation, original charge, kind, signed delta, reason, status, create/approve/post actor/time
payment_refunds	Request hoàn tiền	original payment, reservation, requested/approved amount, reason, status, actors, external reference, idempotency
tax_rules	Quy tắc thuế	property, tax code, rate, calculation mode, effective dates, active
holiday_rates	Ngày lễ/phụ thu	property, date/range, rule type, value, effective status
action_permissions	Quyền theo hành động	role, permission key, property scope, allowed
shift_handovers	Phiếu giao ca	cash shift, property, giver/receiver, status, created/ack times
shift_handover_items	Nội dung bàn giao	handover, category, description, priority, related entity
properties (bổ sung)	Timezone/cutoff	time_zone_id, business_day_cutoff_time, currency, locale
reservations (bổ sung)	Snapshot/rule	rate_plan_id, rate_plan_snapshot, pricing_snapshot_version, deposit rule/due fields
payments (bổ sung)	Liên kết refund	original_payment_id hoặc FK tương đương; không sửa giao dịch gốc


19.1. Constraint/index tối thiểu
- properties.code unique.
- rooms(property_id, number) unique.
- room_types(property_id, code) unique.
- rate_plans(property_id, code) unique.
- app_users.email unique normalized lower-case.
- payments.idempotency_key unique partial index khi not null; cú pháp filter phải phù hợp provider.
- daily_closes(property_id, business_date) unique.
- reservation_holds index theo property_id, room_id, date range, status, expiry.
- room_transfer_histories index theo stay_id, reservation_id, effective_at_utc.
- housekeeping_tasks index theo property_id, room_id, status, assignee, due time.
- night_audit_runs(property_id, business_date) unique.
- night_audit_charge_lines unique theo key chống charge trùng cho stay/business date/type.
- notification_dispatches unique theo reservation + notification type + schedule milestone + channel hoặc idempotency key.
- FK dùng ON DELETE RESTRICT cho giao dịch/booking; không cascade xóa dữ liệu tài chính.
- Check constraint: amount không vô hạn, date range hợp lệ, status nằm trong tập cho phép, due time hợp lý.
20. Contract API chính V5
Các route dưới đây là target; Developer phải giữ route V4 hiện có và không thay body cũ theo cách breaking.
Module	Method	Route	Quyền dự kiến
Booking Note	GET	/api/booking-note-types?propertyId=	User có scope
Booking Note	POST	/api/booking-note-types	Manager/Chain Admin
Booking Note	GET	/api/reservations/{id}/notes	User có scope
Booking Note	POST	/api/reservations/{id}/notes	Lễ tân/Manager có scope
Booking Hold	POST	/api/reservation-holds	Lễ tân/Sales có scope
Booking Hold	GET	/api/reservation-holds?propertyId=&status=	User có scope
Booking Hold	POST	/api/reservation-holds/{id}/convert	Lễ tân/Manager có scope
Booking Hold	POST	/api/reservation-holds/{id}/release	Lễ tân/Manager có scope
Room Transfer	POST	/api/stays/{stayId}/transfers	Lễ tân; Manager approve khi đổi giá
Room Transfer	GET	/api/stays/{stayId}/transfers	User có scope
Housekeeping	GET	/api/housekeeping/tasks?propertyId=&status=&assigneeId=	User có scope
Housekeeping	POST	/api/housekeeping/tasks	System/Manager/authorized staff
Housekeeping	PATCH	/api/housekeeping/tasks/{id}/status	Assignee/Inspector theo state
Housekeeping	POST	/api/housekeeping/tasks/{id}/media	Assignee có scope
Night Audit	GET	/api/night-audit/precheck?propertyId=&businessDate=	Accounting/Manager có scope
Night Audit	POST	/api/night-audit/run	Accounting/Manager có scope
Night Audit	POST	/api/night-audit/{id}/lock	Manager/Chain Admin
Night Audit	POST	/api/night-audit/{id}/reopen	Chain Admin hoặc quyền duyệt riêng
Night Audit	GET	/api/night-audit/runs?propertyId=&from=&to=	Accounting/Manager có scope
Finance	POST	/api/adjustment-vouchers	Finance/Manager theo policy
Finance	POST	/api/adjustment-vouchers/{id}/approve	Approver khác người tạo nếu policy yêu cầu
Finance	POST	/api/adjustment-vouchers/{id}/post	Finance có scope
Finance	POST	/api/payment-refunds	Finance/Manager
Finance	POST	/api/payment-refunds/{id}/approve	Approver
Finance	POST	/api/payment-refunds/{id}/process	Finance
Reports	GET	/api/reports/chain/kpis?from=&to=&propertyId=	Manager/Accounting/Chain Admin
Alerts	GET	/api/alerts?propertyId=&status=	User có scope
Shift Handover	POST	/api/shift-handovers	Người giao ca
Shift Handover	POST	/api/shift-handovers/{id}/acknowledge	Người nhận ca


20.1. Ví dụ Request/Response chuẩn
Tạo Booking Hold — request:
{
  "propertyId": 1,
  "roomId": 101,
  "guestName": "Nguyen Van A",
  "guestPhone": "0900000000",
  "checkInDate": "2026-11-10",
  "checkOutDate": "2026-11-12",
  "expiresAtUtc": "2026-11-06T10:30:00Z",
  "idempotencyKey": "8c12c5a6-31c5-4ab1-bfc5-8f2f0d001111"
}
Kết quả:
{
  "id": 1001,
  "propertyId": 1,
  "roomId": 101,
  "status": "ACTIVE",
  "heldAtUtc": "2026-11-06T10:00:00Z",
  "expiresAtUtc": "2026-11-06T10:30:00Z",
  "remainingSeconds": 1800
}
remainingSeconds chỉ là giá trị hiển thị được tính từ thời gian server; client không được dùng giá trị này làm nguồn xác thực hết hạn.

Tạo Room Transfer — request:
{
  "newRoomId": 205,
  "reasonCode": "GUEST_REQUEST",
  "reasonText": "Khách yêu cầu đổi sang phòng yên tĩnh hơn",
  "effectiveAtUtc": "2026-11-06T09:30:00Z",
  "idempotencyKey": "8c12c5a6-31c5-4ab1-bfc5-8f2f0d001112"
}
Tạo Housekeeping Task — request:
{
  "propertyId": 1,
  "roomId": 101,
  "stayId": 501,
  "taskType": "CHECKOUT_CLEANING",
  "priority": "NORMAL",
  "assignedToUserId": 23,
  "dueAtUtc": "2026-11-06T12:00:00Z"
}
Các request ví dụ chỉ minh họa contract, không dùng ID giả để gửi lên database khách thật.
PHẦN F — PHÂN QUYỀN VÀ BẢO MẬT
21. Ma trận quyền tối thiểu
Hành động	Chain Admin	Property Manager	Front Desk	Sales	Housekeeping	Maintenance	Accounting
Xem toàn chuỗi	✓	—	—	—	—	—	theo scope
Quản lý property/master data	✓	property mình	—	—	—	phòng/bảo trì theo scope	—
Tạo/sửa booking	✓	✓	✓	✓	—	—	xem
Ghi booking note	✓	✓	✓	✓	—	—	xem khi cần
Thu cọc/thanh toán	✓	theo policy	theo ca	—	—	—	✓
Hoàn tiền	✓/approve	approve theo ngưỡng	không mặc định	—	—	—	process theo policy
Chuyển phòng	✓	✓	✓ theo policy	—	—	—	xem
Tạo task housekeeping	✓	✓	tự động sau checkout	—	—	—	xem
Mark cleaned	—	giám sát	—	—	task được giao	—	—
Approve inspected	✓	✓	—	—	không tự approve task của mình nếu policy yêu cầu tách biệt	—	—
Khóa Night Audit	✓	✓	—	—	—	—	theo permission riêng
Reopen Night Audit	✓	chỉ khi được ủy quyền	—	—	—	—	không mặc định
Báo cáo tài chính	✓	property scope	giới hạn	giới hạn	—	—	✓
Quản lý permission	✓	không tự nâng quyền	—	—	—	—	—


Quy tắc:
- Backend luôn validate role + property scope + permission key; ẩn nút trên UI không phải biện pháp bảo mật.
- Không tin propertyId, userId, approvedBy từ body request. Actor phải lấy từ JWT claims.
- Người tạo phiếu điều chỉnh/hoàn tiền không mặc nhiên được tự duyệt nếu policy yêu cầu maker-checker.
- Role Chain Admin mới có quyền vượt phạm vi property.
- Mọi access PII và thao tác tài chính quan trọng phải audit.
22. Bảo mật ứng dụng và dữ liệu
- JWT secret/connection string/object storage key chỉ ở Render/Vercel environment hoặc secret manager; không commit.
- Password hash bằng password hasher mạnh; thêm lockout/rate limit login và reset password flow.
- Bật HTTPS; CORS allowlist chính xác domain; không AllowAnyOrigin với credential.
- Validate input; tránh mass assignment; dùng DTO allowlist.
- Dữ liệu file ảnh upload phải kiểm tra content type, size, quyền, tên/object key; không dùng file name do client quyết định làm path.
- PII: chỉ lưu trường định danh tối thiểu cần thiết; masking ở UI; hạn chế export; mã hóa field nhạy cảm theo key management; audit quyền xem.
- Không log password, token, connection string, thông tin thẻ/giấy tờ.
- Chống CSRF nếu dùng cookie auth; nếu dùng bearer token cần bảo vệ token trong client và XSS prevention.
- Thêm dependency scanning, secret scanning, SAST, container scan và SBOM ở mức phù hợp.
- Có backup/restore và retention policy. Chính sách lưu trữ hồ sơ/thuế phải được Legal/Accounting xác nhận; không tự đặt số năm trong code.
PHẦN G — TEST PLAN, RELEASE GATE VÀ DEFINITION OF DONE
23. Kiểm thử bắt buộc
23.1. Unit Test — Domain/business rules
Ít nhất các nhóm sau:
- Booking date overlap và biên ngày checkout exclusive.
- Booking Note required/optional/inactive.
- Hold ACTIVE/EXPIRED/RELEASED/CONVERTED và expiry idempotent.
- Deposit due/reminder milestones và không gửi trùng.
- Rate snapshot không đổi khi rate plan master thay đổi.
- Transfer room availability, reason required, rate delta, approval.
- Housekeeping transition hợp lệ; reject/rework; photo-required rule.
- Minibar consumption không trừ kho/ghi charge hai lần.
- Night Audit business date, room-night charge, cutoff timezone, retry, lock/reopen.
- Refund không vượt số tiền có thể hoàn và idempotency.
- Tax/holiday/discount effective date và rounding rules.
- OCC/ADR/RevPAR formulas.
23.2. Integration Test với PostgreSQL thật
- Dùng Testcontainers PostgreSQL hoặc Neon staging branch; không chỉ mock EF DbContext.
- Test migration từ schema V4 đã có dữ liệu.
- Test unique index/filter/idempotency.
- Test transaction, SELECT ... FOR UPDATE, concurrency hai request cùng room.
- Test rollback khi một bước trong nghiệp vụ thất bại.
- Test query snake_case/table/column mapping.
23.3. API Test
- 200/201: thành công.
- 400/422: field thiếu/invalid.
- 401: token thiếu/hết hạn.
- 403: sai role hoặc property scope.
- 404: ID không tồn tại.
- 409: room conflict, duplicate request, state transition không hợp lệ, day locked.
- Financial write retry với cùng idempotency key phải trả cùng kết quả hoặc phản hồi an toàn, không tạo giao dịch thứ hai.
23.4. Angular E2E — Playwright
Kịch bản xuyên suốt tối thiểu:
1. Login Chain Admin → chọn property → xem Dashboard.
2. Login Lễ tân → tạo booking có Booking Note → nhận cọc → check-in.
3. Thêm service/minibar → thanh toán → checkout.
4. Checkout tạo housekeeping task → nhân viên được giao dọn → upload ảnh → supervisor approve → room INSPECTED.
5. Chuyển phòng → xác minh history/rate adjustment.
6. Hold phòng → hết hạn → phòng mở bán lại → booking khác có thể đặt.
7. Night Audit precheck → run → lock → thử write bị 409 → reopen có reason.
8. Refund request → approve → process → đối chiếu ledger.
9. Accounting chạy báo cáo chuỗi và kiểm tra KPI.
10. Test role: Housekeeping không xem/chỉnh tài chính; Front Desk không tự duyệt refund; property A không đọc property B.
23.5. Concurrency/Performance/Security
- Hai request tạo booking cùng room/date cùng lúc: chỉ một request thành công.
- Hai request ghi payment cùng idempotency: không ghi trùng.
- Hai request post minibar cùng item/task: không trừ tồn hai lần.
- Hai Night Audit cùng property/date: chỉ một run được thực hiện.
- Load test theo profile người dùng thật; mốc <2s p95 @50 concurrent trong bản kế hoạch cũ chỉ là mục tiêu sơ bộ, phải đo trên Render plan và Neon tier thực tế, không mặc định là SLA.
- Kiểm tra OWASP Top 10, authorization bypass, IDOR/property scope, file upload, token storage, rate limiting.
24. CI/CD và môi trường
Môi trường
Môi trường	Frontend	Backend	Database
Local	localhost:4200	localhost:5080	PostgreSQL local/Testcontainer hoặc SQL Server local nếu team còn duy trì
Staging/UAT	Vercel Preview/Staging	Render Staging service	Neon branch staging
Production demo	Vercel Production	Render Production	Neon production branch


Không dùng dữ liệu khách thật trong shared QA demo. Không dùng seed demo ở Production thật.
Git flow đề xuất
1. main luôn ở trạng thái có thể deploy; protected branch.
2. Developer tạo feature/<module>-<ticket> từ main.
3. Mở Pull Request; yêu cầu review ít nhất 1 người.
4. CI phải pass: dotnet restore/build/test, npm ci, npm run test:types, npm run build, static checks, integration tests.
5. Vercel Preview tự tạo; Render Staging deploy sau CI pass.
6. Migration chạy riêng trên Neon staging; test upgrade/data integrity.
7. QC chạy API/E2E và đính kèm evidence.
8. Merge vào main sau approval; Production deploy có gate.
9. Migration production cần backup, runbook, approval và verify sau deploy.
GitHub Actions tối thiểu
Tạo .github/workflows/ci.yml với các job:
- backend-build-test: .NET 10 restore/build/test.
- frontend-build-test: Node 22, npm ci, npm run test:types, npm run build.
- static-contracts: Python compile/lint/test static contract.
- postgres-integration: PostgreSQL service/Testcontainers, apply migration, integration tests.
- security: secret scan/dependency scan.
Không đặt Neon production credential trong workflow CI cho PR từ fork. Dùng secret staging với quyền tối thiểu.
Health Check
- GET /health/live: chỉ xác nhận tiến trình còn sống.
- GET /health/ready: xác nhận API sẵn sàng nhận traffic; phải kiểm tra DB bằng query nhẹ và kiểm tra dependency cần thiết.
- Render healthCheckPath dùng /health/ready sau khi endpoint đã được triển khai.
- Readiness fail phải không nhận traffic; log lỗi không lộ connection string.
25. Migration và release safety
1. Backup/branch Neon trước thay đổi schema.
2. Migration phải add/alter có kiểm soát; không DROP cột/bảng trong cùng release nếu còn code cũ dùng.
3. Dùng expand-migrate-contract: thêm cột nullable/default → deploy code đọc/ghi tương thích → backfill → xác minh → release sau mới bỏ cột cũ.
4. Migration không được seed user/password mặc định trên production.
5. Migration có thể chạy qua one-off release command/CI manual approval; không chạy đồng thời từ mọi instance app startup.
6. Mỗi migration có file SQL reviewable và rollback/recovery note. Với data migration không thể rollback đơn giản, phải có forward-recovery plan.
7. Sau deploy: kiểm tra health, login, đọc danh sách property/rooms, tạo giao dịch demo trên staging, đối chiếu Neon, kiểm tra logs.
8. Không bao giờ chạy file 99-seed-test-data-postgres.sql hiện tại trước khi được viết lại và xác nhận trên staging.
26. Acceptance Gates — điều kiện được gọi là V5 đạt
Gate	Điều kiện	Bằng chứng bắt buộc
G0 Security	Neon credential đã rotate; không còn secret trong source/history được xử lý; demo auth chỉ trên staging	Secret scan + checklist bảo mật
G1 Backend	.NET Release build/test pass	CI log
G2 Frontend	Angular production build + TypeScript check pass	CI log
G3 Database	Migration V4→V5 chạy trên clone/staging, dữ liệu không mất	Migration log + row counts + schema diff
G4 Static	Static contract pass	JSON report
G5 Unit	Business unit tests pass	Test report
G6 Integration	PostgreSQL integration/concurrency tests pass	Test report
G7 API	API critical flows pass	API collection/report
G8 E2E	Playwright critical flows pass	Screenshots/video/trace
G9 Security	Không còn High/Critical chưa xử lý; authorization tests pass	Security report
G10 Performance	Kết quả load test đạt mục tiêu được chủ hệ thống duyệt	k6 report
G11 Backup/Restore	Restore test thành công trên DB clone	Restore evidence
G12 UAT	Lễ tân, Quản lý, Kế toán/Housekeeping xác nhận theo checklist	UAT sign-off
G13 Production	Deploy đúng commit; health ready; smoke pass; rollback plan sẵn	Release record


Không được ghi “V5 production-ready” nếu còn gate nào chưa chạy hoặc chưa có evidence.
27. Definition of Done cho từng chức năng
Một ticket chỉ được Done khi đầy đủ:
- [ ] Nghiệp vụ đã được mô tả, có actor, pre-condition, main flow, alternative flow, exception flow.
- [ ] Có state transition và rule nghiệp vụ rõ ràng.
- [ ] Có request/response contract và field dictionary.
- [ ] Có entity/configuration/index/FK/check constraint và migration.
- [ ] Backend validate role + property scope + trạng thái; không tin dữ liệu client.
- [ ] Transaction/idempotency/concurrency được xử lý nếu là write nghiệp vụ.
- [ ] Audit trail ghi actor, action, entity, entity ID, time, reason/correlation ID.
- [ ] Angular có loading/error/empty/validation/confirmation state.
- [ ] Có Unit + Integration + API + E2E test phù hợp.
- [ ] Có screenshot/log/test evidence và hướng dẫn QC.
- [ ] Có cập nhật FEATURE_MATRIX, API docs, field dictionary, release notes.
- [ ] Không làm vỡ contract V4 hoặc có compatibility plan.
- [ ] Đã test migration/rollback trên Neon staging; chưa tác động production nếu chưa được duyệt.
PHẦN H — BACKLOG TRIỂN KHAI ĐỀ XUẤT
28. Mapping yêu cầu A25 đã được xác định trong quá trình làm việc
Các mã dưới đây là nhóm backlog cần được Developer đối chiếu lại từng dòng trong file Excel Danh muc yeu cau bản gốc trước khi chốt acceptance. Không tự diễn giải vượt quá phạm vi ghi trong Excel.
Mã yêu cầu	Nghiệp vụ	Hiện trạng theo source V4	Kế hoạch
YC-BS-01	Tích hợp OTA/Hotelink với A25 PMS	Chưa có tích hợp thực tế; mới lưu booking source/agency	V5.3, phụ thuộc hợp đồng, API spec, sandbox và mapping
YC-BK-01	Booking Note theo danh mục	Có Reservation.Note tự do; chưa có dictionary	V5.1
YC-BK-05	Lọc trạng thái phòng khi nhận phòng	Có room map và availability cơ bản; cần hoàn thiện filter theo trạng thái nghiệp vụ	V5.1
YC-BK-07	Lịch sử chuyển phòng	Có transfer và audit event tối giản; thiếu lịch sử giá/lý do chi tiết	V5.1
YC-BK-08	Giữ phòng có thời hạn/tự nhả nếu quá hạn cọc	Chưa thấy reservation hold có expiry	V5.1
YC-BS-10	Nghiệm thu phòng	Có DIRTY/CLEAN/INSPECTED đơn giản	V5.1
YC-BS-11	Sổ giao ca điện tử	Có cash shift open/close và note cơ bản; thiếu bàn giao/xác nhận theo checklist	V5.2
YC-BS-16	Cảnh báo hạn đặt cọc	Chưa thấy notification scheduler/log	V5.1
YC-BS-17	Cảnh báo công suất phòng thấp	Có occupancy cơ bản; chưa có ngưỡng cảnh báo cấu hình	V5.2
YC-BS-18	Cảnh báo doanh thu thấp	Có báo cáo doanh thu cơ bản; chưa có threshold alert	V5.2
YC-BC-01	Dashboard doanh thu chuỗi	Có report chain cơ bản; thiếu KPI/biểu đồ/drill-down đầy đủ	V5.2
YC-BS-23	Báo cáo khách theo quốc tịch và chi nhánh	Có nationality ở Guest/Occupant; chưa có báo cáo chuyên biệt đầy đủ	V5.2
YC-VH-06	Cảnh báo tồn kho dưới định mức	Có stock movement/report; chưa có min stock threshold alert	V5.2
YC-VH-09	Ảnh minibar khi kiểm phòng cuối	Chưa thấy task/photo workflow	V5.1
YC-VH-10	Đối chiếu phòng nghỉ giờ với lượt dọn	Chưa thấy báo cáo đối chiếu riêng	V5.2


Quy tắc phân loại: YC-BS-01 là tích hợp bên thứ ba/Channel Manager, không phải xây một Booking App độc lập. Hotline yếu tại phòng Kinh doanh là sự cố hạ tầng/viễn thông riêng, không phải module PMS.
29. Thứ tự triển khai bắt buộc
Phase 0 — Chặn rủi ro và làm sạch V4 (P0)
1. Rotate Neon credential đã lộ; xử lý Git history và secret scan.
2. Tạo Neon staging branch/DB clone.
3. Tắt demo initialization/seeder trên Render Production.
4. Sửa index filter idempotency cho PostgreSQL.
5. Viết lại seed/verify PostgreSQL; không dùng file hiện tại.
6. Thiết lập EF migrations/baseline an toàn.
7. Bổ sung health ready DB.
8. Tạo GitHub Actions CI và khóa branch main.
9. Chạy build, pricing unit tests, static V4 contracts, API smoke trên staging.
10. Cập nhật feature matrix và deployment guide.
Phase 1 — V5.1 Booking & Housekeeping (P1)
1. Rate Plan snapshot trên Reservation.
2. Booking Note dictionary + required notes.
3. Reservation hold + expiry job.
4. Deposit due date + notification scheduler/log.
5. Room transfer history + reason + old/new rates.
6. Housekeeping task + assignment + checklist + inspection approval.
7. Minibar inspection/photo evidence + inventory transaction.
8. Room status/availability filters hoàn chỉnh.
Phase 2 — V5.2 Finance & Night Audit (P1/P2)
1. Property timezone + business date/cutoff.
2. Night Audit run/lock/reopen và room-night posting.
3. Adjustment voucher + maker-checker approval.
4. Refund workflow liên kết payment gốc.
5. Tax/holiday/discount/pricing rules cấu hình theo ngày hiệu lực.
6. Cash shift reconciliation + handover.
7. Low-stock/low-occupancy/low-revenue alerts.
8. Dashboard OCC/ADR/RevPAR và báo cáo nationality/branch.
Phase 3 — V5.3 Integration & chain operations (phụ thuộc đối tác)
1. Hotelink/OTA connector theo API thực tế.
2. E-invoice provider theo API/sandbox được duyệt.
3. POS/F&B/spa/PBX nếu có yêu cầu và hợp đồng.
4. Customer profile/loyalty/CRM nếu được ưu tiên nghiệp vụ.
PHẦN I — FORMAT GIAO VIỆC CHO DEVELOPER
30. Template mô tả một ticket
Developer phải điền các mục này trước khi code:
Ticket ID:
Tên chức năng:
A25 requirement ID:
Priority:
Module:
Business owner:

1. Mục tiêu nghiệp vụ:
2. Actor/Role:
3. Preconditions:
4. Main flow:
5. Alternative flows:
6. Exception flows:
7. State transitions:
8. Business rules/calculation:
9. Database tables/fields/index/FK:
10. API method/route/request/response/error:
11. Angular screen/form/permission:
12. Audit/idempotency/concurrency:
13. Migration/data backfill:
14. Unit test cases:
15. Integration/API test cases:
16. E2E test cases:
17. Acceptance criteria:
18. Deployment/rollback note:
19. Evidence links:
31. Các yêu cầu Developer không được làm
- Không xóa database Neon production để “làm mới”.
- Không chạy seed/test data trên branch production.
- Không commit .env, connection string, JWT secret, mật khẩu demo hoặc file export có PII.
- Không tự động seed demo account khi app restart trên Production.
- Không dùng EnsureCreated() làm cơ chế nâng cấp schema.
- Không đổi provider connection string từ SQL Server sang PostgreSQL mà không chuyển migration/index/raw SQL tương ứng.
- Không ghi tiền bằng logic Angular hoặc double/float.
- Không UPDATE/DELETE charge/payment đã post để sửa sai; dùng reversal/adjustment.
- Không cho room được bán chỉ vì housekeeping status được đổi bằng một nút; phải kiểm tra task/inspection/OOO/booking conflict.
- Không đánh dấu tích hợp OTA/e-invoice đã xong nếu chỉ tạo UI hoặc mock API.
- Không đưa V5 lên main/Production khi chưa qua CI, migration staging, E2E và approval.
PHẦN J — CHECKLIST CHỦ DỰ ÁN / QC DÙNG ĐỂ NGHIỆM THU
32. Checklist nghiệm thu V4 trước khi merge V5
- [ ] Vercel frontend mở được và gọi đúng Render API.
- [ ] Render API health/ready hoạt động và query được Neon.
- [ ] Đăng nhập đúng role; sai password trả 401.
- [ ] User property A không đọc/ghi property B.
- [ ] Tạo booking và kiểm tra overlap.
- [ ] Hai request đồng thời cùng phòng chỉ một request thành công.
- [ ] Check-in chỉ khi room inspected, không OOO và không có stay active.
- [ ] Booking group/occupants/check-in/pay/checkout có E2E evidence.
- [ ] Waitlist create/assign/cancel có E2E evidence.
- [ ] Folio add service, payment, deposit, refund-deposit và idempotency được kiểm tra.
- [ ] Checkout tính đúng số dư và chuyển phòng DIRTY.
- [ ] Room transfer không tạo overlap và audit được.
- [ ] Inventory stock không âm; hai request đồng thời không trừ tồn hai lần.
- [ ] Ca thu ngân/reconcile đối chiếu được tiền mặt.
- [ ] Pricing quote 13/13 unit tests pass và không ghi DB.
- [ ] Pricing adjustment quyền lễ tân bị 403; quản lý được phép; duplicate bị 409.
- [ ] Night audit precheck khớp dữ liệu; UI ghi rõ chưa phải Night Audit thật.
- [ ] dotnet build -c Release, npm ci, npm run test:types, npm run build đều pass.
- [ ] Neon migration/seed test đã chạy trên staging; có bằng chứng row count và schema.
- [ ] FEATURE_MATRIX.md, architecture và QA guide đồng bộ.
33. Checklist nghiệm thu V5
- [ ] Rate Plan snapshot đúng tại thời điểm booking.
- [ ] Booking Note required/optional hoạt động đúng.
- [ ] Hold countdown dùng server time; expiry job idempotent.
- [ ] Deposit due alert không gửi trùng; policy quá hạn được duyệt.
- [ ] Transfer history đầy đủ reason/rate/actor/approver.
- [ ] Housekeeping task có assigned/clean/inspect/approve/reject và ảnh khi bắt buộc.
- [ ] Minibar photo và stock deduction không ghi trùng.
- [ ] Night Audit tính room-night đúng, không double-charge, khóa/mở lại có audit.
- [ ] Refund/adjustment có approval, ledger link và idempotency.
- [ ] Tax/holiday/discount có effective date, rounding rules được Accounting ký xác nhận.
- [ ] OCC/ADR/RevPAR được đối chiếu số liệu mẫu độc lập.
- [ ] Action-level permission test pass.
- [ ] PostgreSQL integration/concurrency/E2E pass.
- [ ] Backup/restore test pass.
- [ ] UAT có chữ ký/approval các bộ phận liên quan.
KẾT LUẬN GIAO VIỆC
Developer cần thực hiện theo thứ tự:
1. Bảo mật và ổn định V4 trước: rotate secret, tắt demo seed trên Production, sửa PostgreSQL index, viết lại seed/verify, migration baseline, readiness và CI.
2. Không làm lại chức năng V4 đã có: đặc biệt là waitlist, group booking, pricing quote và các nghiệp vụ lễ tân đã có trong source; chỉ bổ sung phần thiếu.
3. Phát triển V5.1 trước: rate snapshot, Booking Note, hold/cọc, lịch sử chuyển phòng, housekeeping task/inspection/minibar evidence.
4. Tiếp theo V5.2: finance/refund/adjustment, Night Audit, timezone, reports/KPI, handover/alerts.
5. Sau cùng V5.3: Hotelink/OTA, e-invoice và tích hợp ngoài khi có API/contract thật.
Điều kiện bàn giao: Mỗi module phải có nghiệp vụ, state transition, field dictionary, SQL migration, API contract, Angular UI, RBAC, audit, unit/integration/E2E tests, evidence và hướng dẫn triển khai. Không nghiệm thu chỉ dựa trên việc đã tạo menu hoặc thêm entity.

Giới hạn của lần rà soát: Đây là kiểm tra tĩnh repository và cấu hình tại commit nêu trên. Chưa chạy trực tiếp dotnet build, Angular production build, API runtime, migration Neon, Playwright E2E, load test hoặc penetration test trong lần rà soát này. Các kết quả runtime phải được Developer/QC chạy trên môi trường staging và đính kèm evidence.