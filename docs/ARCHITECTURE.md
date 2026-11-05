# Kiến trúc và luồng dữ liệu

```mermaid
flowchart LR
 A[Angular 20 - localhost:4200] -->|HTTP /api + Bearer JWT| B[ASP.NET Core 10 - localhost:5080]
 B -->|EF Core 10| C[(SQL Server 2022 - localhost:14333)]
 D[DBeaver - JDBC SQL Server] -->|SELECT / SQL Editor| C
```

## Entity quan trọng

```mermaid
erDiagram
  PROPERTIES ||--o{ ROOM_TYPES : provides
  PROPERTIES ||--o{ ROOMS : owns
  ROOM_TYPES ||--o{ ROOMS : classifies
  PROPERTIES ||--o{ RESERVATIONS : handles
  GUESTS ||--o{ RESERVATIONS : primary_guest
  ROOMS ||--o{ RESERVATIONS : booked_room
  RESERVATIONS ||--o| STAYS : checkin
  RESERVATIONS ||--o{ FOLIO_CHARGES : charges
  RESERVATIONS ||--o{ PAYMENTS : settlements
  PROPERTIES ||--o{ SERVICES : sells
  PROPERTIES ||--o{ CASH_SHIFTS : has
  APP_USERS ||--o{ CASH_SHIFTS : opens
  PROPERTIES ||--o{ AGENCIES : contracts
  PROPERTIES ||--o{ AUDIT_EVENTS : records
```

## Các trạng thái thiết kế độc lập

- `reservation.status`: `CONFIRMED`, `CHECKED_IN`, `CHECKED_OUT`, `CANCELLED`.
- `stay.status`: `IN_HOUSE`, `CHECKED_OUT`.
- `room.housekeeping_status`: `DIRTY`, `CLEAN`, `INSPECTED`.
- `room.is_out_of_order`: cờ bảo trì, chặn bán.
- `cash_shift.status`: `OPEN`, `CLOSED`.
- `payment.kind`: `DEPOSIT`, `PAYMENT`.

## Mẫu luồng chuẩn

```mermaid
flowchart TD
  A[Chọn cơ sở và khoảng ngày] --> B[GET /availability]
  B --> C[POST /reservations]
  C --> D{Còn phòng tại lúc ghi?}
  D -->|Không| X[409 Conflict]
  D -->|Có| E[CONFIRMED]
  E --> F[POST /check-in]
  F --> G[IN_HOUSE + Folio tiền phòng]
  G --> H[Thêm dịch vụ / nhận cọc / thanh toán]
  H --> I{Balance = 0?}
  I -->|Không| H
  I -->|Có| J[POST /checkout]
  J --> K[CHECKED_OUT; room DIRTY]
  K --> L[DIRTY -> CLEAN -> INSPECTED]
```

**Lưu ý riêng bản demo:** ghi tiền phòng theo cả kỳ tại check-in; chưa có night audit và doanh thu phân bổ từng ngày. Checkout phải cân bằng tuyệt đối; chưa có refund cho số dư âm. Cần bổ sung các nghiệp vụ này trước khi đưa sản phẩm vào vận hành thật.
