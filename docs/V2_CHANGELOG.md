# LOTUS PMS V2 — Change log

- Kế thừa toàn bộ source V1 (`Program.cs`, `Models.cs`, Angular app, Docker SQL Server).
- Tạo `AdvancedModels.cs`: Building, RatePlan, WaitlistEntry, Supplier, StockMovement, RetailSale, CashVoucher, DebtReceivable, GuestAsset, DailyClose.
- Tạo `AdvancedEndpoints.cs`: 35+ endpoint GET/POST/PATCH theo các luồng FO, MG, RP.
- `AdvancedSeeder.cs`: khởi tạo danh mục V2 và stock movement OPENING cho tồn kho ban đầu của dữ liệu demo.
- `Models.cs`: 10 DbSet mới và 3 cột nullable (`rooms.building_id`, `reservations.agency_id`, `folio_charges.service_item_id`) cùng quan hệ ràng buộc.
- `Program.cs`: gọi `MapPmsV2()`, bổ sung trường bookingMode/agencyId vào yêu cầu đặt phòng, gắn serviceItemId vào charge, thêm ledger cho minibar và restock.
- Angular: tách `advanced.component.ts/.html/.css`, thêm các menu mở rộng theo ba giai đoạn. Thêm chọn rate plan và agency trong đặt phòng đơn.
- SQL `03-v2-upgrade.sql`: có thể chạy trên database V1 mà không xóa dữ liệu, trước lần chạy backend V2 đầu tiên.
- SQL `04-check-v2.sql`: kiểm số hàng theo table, kiểm công nợ/tồn kho/liên kết phòng với tòa nhà.
- `05_SMOKE_TEST_V2.ps1`: smoke test GET an toàn cho backend và báo cáo.
- Kèm dữ liệu test đầu vào, dictionary, ma trận và >60 QC scenarios.
- **Giới hạn:** chưa build thực tế Angular/.NET, chưa test SQL Server; xem `VALIDATION_STATUS.md`.
