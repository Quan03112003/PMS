LOTUS PMS V4 – Bổ sung tính giá và kiểm tra trước chốt ngày
Trạng thái: Bản mã nguồn nâng cấp để build và nghiệm thu trên máy Windows. Chưa nghiệm thu E2E SQL Server ở môi trường tạo gói.
Nền tảng giữ nguyên: Angular 20, ASP.NET Core / .NET 10, SQL Server, DBeaver.
Không thay schema V3: không cần DROP DATABASE, không cần chạy migration SQL cho phần bổ sung V4.
Đã triển khai
1. Lễ tân → Báo giá & phụ thu: chọn bảng giá hiện có, ngày nhận/trả và giờ thực tế. API tính giá trên máy chủ, không dùng công thức tiền từ giao diện.
2. Giá qua đêm: soDem = checkOutDate - checkInDate; tienPhong = soDem × ratePlan.basePrice; nhận sớm/trả muộn làm tròn lên mỗi giờ vượt chuẩn property.checkInTime / checkOutTime và dùng ratePlan.extraHourPrice.
3. Giá nghỉ giờ/ngày: tienGoi = ratePlan.basePrice; quá includedHours tính thêm ceil(soGioVuot) × ratePlan.extraHourPrice.
4. Ghi phụ thu vào folio: chỉ quản lý được thực hiện; phải xác nhận trên giao diện. API dùng transaction + khóa reservation, tính lại server-side và ngăn ghi trùng một loại phụ thu trong một reservation.
5. Snapshot chốt ngày → Kiểm tra trước chốt: kiểm tra ca còn mở, booking đến hạn chưa nhận, khách đến hạn chưa trả, snapshot ngày đã tồn tại. Đây không phải Night Audit khóa sổ thật.
6. Bộ kiểm thử: 13 ca kiểm tra công thức bằng C# và script API smoke chỉ đọc để xác nhận endpoint báo giá.
Chú ý nghiệp vụ (rất quan trọng)
- rate_plans hiện chưa gắn cố định vào từng reservation. Vì vậy, V4 cho phép lễ tân xem báo giá, nhưng chỉ quản lý có quyền chọn bảng giá để ghi phụ thu. Mã V4 không thay giá phòng đã ghi trong folio V3 khi check-in.
- Bảng giá NIGHT mẫu của V3 có thể có extraHourPrice = 0. Muốn thu phụ phí, hãy vào Quản lý → Bảng giá giờ/ngày/đêm và tạo bảng giá mới phù hợp có đơn giá vượt giờ dương; sau đó chọn bảng giá mới trong V4.
- Một loại phụ thu (EARLY_CHECKIN, LATE_CHECKOUT, OVERTIME) chỉ ghi 1 lần cho mỗi reservation; khi cần điều chỉnh/hủy phải có chức năng phiếu điều chỉnh kế toán riêng (chưa triển khai).
- Với phí nhận sớm, thời gian nhập phải gần giờ check-in thực tế đã lưu (sai lệch không quá 15 phút). Với trả muộn/quá giờ chỉ chấp nhận thời điểm không ở tương lai quá 15 phút.
- Phụ thu nhận sớm/trả muộn trên 24 giờ hoặc vượt gói trên 72 giờ bị từ chối; cần gia hạn/tạo thêm đêm đúng nghiệp vụ.
- Ngày giờ nhập ở dạng giờ địa phương Việt Nam (UTC+07, không kèm Z). Chưa có timezone riêng cho từng cơ sở quốc tế.
- Báo giá chưa tự tính thuế, phí dịch vụ, ngày lễ, khuyến mãi, giảm giá, OTA; dayclose vẫn là snapshot chứ chưa khóa sổ.
- Các dependency warning NU1902/`NU1903` của V3 chưa được khắc phục trong patch V4. Không triển khai sản xuất khi chưa rà soát chúng.
Cài đặt lên bản V3 đang chạy và đang có dữ liệu
1. Sao lưu thư mục dự án V3 và database SQL Server trước khi thay đổi.
2. Dừng 02_START_BACKEND.bat và 03_START_ANGULAR.bat trước khi cập nhật mã nguồn. SQL Server vẫn chạy.
3. Sử dụng gói LOTUS_PMS_V4_PATCH_FILES.zip: giải nén sao cho thư mục backend, frontend, qa của patch trùng với thư mục gốc HotelChainPMS đang chạy. Chọn ghi đè các file trong patch. Không ghi đè appsettings.json / appsettings.Development.json, vì patch không chứa hai file cấu hình này.
4. Trong PowerShell tại thư mục HotelChainPMS, chạy:
   powershell -ExecutionPolicy Bypass -File .\qa\06_acceptance_build_windows.ps1
   powershell -ExecutionPolicy Bypass -File .\qa\09_v4_pricing_test_windows.ps1
5. Nếu cả hai bước trên PASS, khởi động lại backend 02_START_BACKEND.bat, giao diện 03_START_ANGULAR.bat.
6. Mở http://localhost:4200, vào Lễ tân → Báo giá & phụ thu, chọn cơ sở rồi tính thử. Có thể kiểm tra endpoint giá chỉ đọc:
   powershell -ExecutionPolicy Bypass -File .\qa\10_v4_pricing_api_smoke.ps1
7. Xem Quản lý → Snapshot chốt ngày → Kiểm tra trước chốt, đối chiếu số ca / khách đến / khách đi.
8. Trước khi thử thao tác “Ghi phụ thu vào folio”, sử dụng database demo hoặc reservation kiểm thử, sao lưu database rồi kiểm tra số dư folio trước/sau bằng DBeaver.
API mới
Method	Route	Quyền	Ý nghĩa
POST	/api/pricing/quote	User có quyền với property	Báo giá, không ghi CSDL
POST	/api/stays/{stayId}/pricing-adjustments	CHAIN_ADMIN, PROPERTY_MANAGER + thuộc property	Ghi phụ thu thực tế, chống trùng
GET	/api/night-audit/precheck?propertyId=1&businessDate=2026-11-05	User thuộc property	Kiểm tra cảnh báo trước snapshot


Ví dụ body báo giá:
{
  "propertyId": 1,
  "ratePlanId": 1,
  "checkInDate": "2026-11-05",
  "checkOutDate": "2026-11-07",
  "actualCheckInLocal": "2026-11-05T12:30:00",
  "actualCheckOutLocal": "2026-11-07T13:15:00"
}
Ví dụ body ghi phụ thu (chỉ sử dụng khi có dữ liệu demo đang lưu trú):
{
  "kind": "LATE_CHECKOUT",
  "ratePlanId": 1,
  "actualAtLocal": "2026-11-07T13:15:00"
}
Không tự gửi lại ví dụ ở trên với ngày/giờ cố định lên khách thực tế. Giá phụ thu máy chủ tính lại theo hồ sơ lưu trú, chính sách bảng giá và thời gian thực tế.
Kiểm tra trong DBeaver (chỉ đọc)
SELECT TOP (20)
    fc.id, fc.reservation_id, fc.category, fc.description, fc.quantity,
    fc.unit_price, fc.amount, fc.posted_at_utc
FROM dbo.folio_charges AS fc
WHERE fc.category IN ('EARLY_CHECKIN', 'LATE_CHECKOUT', 'OVERTIME')
ORDER BY fc.id DESC;
Tiêu chí nghiệm thu V4
- [ ] Backend Release build PASS.
- [ ] Angular production build + TypeScript typecheck PASS.
- [ ] 09_v4_pricing_test_windows.ps1: 13/13 ca PASS (bộ test mã nguồn C#, không phải E2E).
- [ ] API smoke có token: báo giá thành công, không ghi tiền.
- [ ] Nhân viên lễ tân xem giá được nhưng không POST được phụ thu (403).
- [ ] Quản lý ghi phụ thu vào folio 1 lần; gửi lại cùng loại bị 409; số dư folio tăng đúng.
- [ ] Chọn plan khác cơ sở/khác bookingMode bị từ chối.
- [ ] Tiền không bị nhân đôi khi 2 request đồng thời.
- [ ] Precheck đúng số ca thu ngân và booking đến/trả chưa xử lý.
- [ ] Bộ E2E V3 và kiểm thử bảo mật/dữ liệu còn PASS.
Chưa được coi là nghiệm thu sản xuất trước khi tất cả điều kiện trên có bằng chứng thực thi.