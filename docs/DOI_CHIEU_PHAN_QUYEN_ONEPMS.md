# Đối chiếu phân quyền OnePMS và Lotus PMS

Ngày đối chiếu: 11/11/2026.

Nguồn nghiệp vụ: [Hướng dẫn phân quyền nhân viên trên OnePMS](https://onepms.net/docs/huong-dan-phan-quyen-nhan-vien-tren-phan-mem-quan-ly-khach-san-onepms/).

Phạm vi: so quyền đang chạy trong Lotus (menu Angular và kiểm tra quyền trên API) với mô hình trong bài viết. File này là danh mục cần bổ sung. Chưa đổi mã.

## 1. OnePMS cấp quyền thế nào

Sau khi tạo tài khoản, nhân viên chưa có quyền nào. Người quản trị vào Quản lý, Nhân viên, rồi cấp quyền cho đúng tài khoản đó.

Có ba cách cấp:

1. Chọn một vị trí có sẵn, ví dụ Lễ tân. Hệ thống gắn cả gói quyền của vị trí đó.
2. Trên gói đã chọn, tick thêm hoặc bỏ bớt từng quyền. Ví dụ lễ tân kiêm tạo tài khoản thì chỉ thêm một quyền “Thêm người dùng vào khách sạn”, không đổi người đó thành quản lý.
3. Không chọn vị trí, mở danh sách quyền của hệ thống và tick từng việc. Danh sách chia theo mục và theo hành động. Có ô tìm quyền.

Người quản trị xem lại quyền đã cấp. Bỏ tick một quyền thì quyền đó hết hiệu lực, rồi bấm lưu.

Bài viết mô tả mười vị trí: Chủ khách sạn, Quản lý, Lễ tân, Kế toán, Buồng phòng, Kinh doanh, Quản lý kho, Người phê duyệt, Đối tác, Đối tác nội bộ.

## 2. Lotus đang cấp quyền thế nào

Lotus có bảy vai cố định, lưu một cột `Role` trên tài khoản:

| Vai | Tên trên màn hình |
| --- | --- |
| `CHAIN_ADMIN` | Quản trị toàn chuỗi |
| `PROPERTY_MANAGER` | Quản lý khách sạn |
| `FRONT_DESK_AGENT` | Lễ tân |
| `SALES_AGENT` | Kinh doanh |
| `HOUSEKEEPING_AGENT` | Buồng phòng |
| `MAINTENANCE_AGENT` | Bảo trì |
| `ACCOUNTING_AGENT` | Kế toán |

Tạo tài khoản là chọn một vai. Tài khoản dùng được cả gói của vai đó ngay. Không có trạng thái “chưa được cấp quyền”.

Một người chỉ có một vai. Không gắn hai vị trí, không thêm một quyền lẻ, không bỏ một quyền trong gói.

Menu ẩn theo vai trong `canView`. API chặn thêm ở từng endpoint và ở bộ lọc `Ops.DepartmentAccess`.

Điểm cần nhớ khi đọc bảng bên dưới: lễ tân, quản lý cơ sở và quản trị chuỗi đi thẳng qua `DepartmentAccess`. Lễ tân bị chặn ở những API có kiểm tra riêng, ví dụ sửa giá sàn, gắn bảo mật giá, hoàn cọc, tạo nhân viên. Các API không kiểm tra riêng thì lễ tân vẫn gọi được, kể cả khi menu đã giấu màn hình.

Bảng `action_permissions` đã có trong cơ sở dữ liệu. Cột đang là vai, mã quyền, cơ sở và cờ cho phép. Comment trong mã ghi bảng này dành cho RBAC theo hành động, nhưng chưa có API nào đọc bảng này để cho hoặc chặn. Bảng cũng gắn theo vai, chưa gắn theo từng nhân viên như OnePMS.

Quản lý cơ sở tạo được nhân viên phòng ban trong đúng cơ sở của mình. Quản lý cơ sở không tạo được quản trị chuỗi, không tạo được quản lý khác. Quản trị chuỗi tạo được tài khoản toàn chuỗi.

## 3. Từng vị trí OnePMS

Ký hiệu: Đã có nghĩa là Lotus làm được việc đó cho đúng người. Một phần nghĩa là có chức năng nhưng chưa khóa đúng người, hoặc chỉ khóa trên menu. Chưa có nghĩa là chưa có việc đó.

### 3.1. Chủ khách sạn

OnePMS: vào toàn bộ phòng, khách, hóa đơn, doanh thu, dịch vụ, báo cáo. Tạo, sửa, xóa nhân viên và phân công. Xem báo cáo doanh thu, chi phí và chỉ số. Được ưu tiên duyệt thay đổi quan trọng.

| Việc | Lotus |
| --- | --- |
| Vào toàn bộ nghiệp vụ một cơ sở | Đã có, vai quản trị chuỗi và quản lý cơ sở |
| Tạo, sửa, khóa tài khoản | Đã có. Quản trị chuỗi làm được trên toàn chuỗi. Quản lý cơ sở làm trong cơ sở của mình |
| Xóa hẳn tài khoản | Chưa có. Lotus khóa và mở khóa tài khoản |
| Báo cáo doanh thu, công suất, RevPAR | Đã có với quản lý và kế toán |
| Một vai “chủ” tách khỏi quản lý vận hành | Chưa có. Quản trị chuỗi đang kiêm chủ và quản trị nhiều cơ sở |

### 3.2. Quản lý

OnePMS: toàn bộ nghiệp vụ vận hành. Sửa cấu hình giá, chính sách hoàn hủy, giờ nhận trả, dịch vụ và khuyến mại. Xem và lập báo cáo doanh thu, công suất.

| Việc | Lotus |
| --- | --- |
| Vận hành phòng, khách, lưu trú, dịch vụ | Đã có |
| Sửa bảng giá, giá lễ, thuế, dịch vụ | Đã có với quản lý |
| Giờ nhận, giờ trả trên cơ sở | Đã có trường dữ liệu |
| Chính sách hoàn hủy dạng cấu hình riêng | Một phần. Hoàn cọc và hoàn tiền có người duyệt, chưa có màn cấu hình chính sách hủy |
| Khuyến mại | Chưa có chương trình khuyến mại riêng. Đang có trần giảm và giá sàn |
| Báo cáo OCC, ADR, RevPAR | Đã có trên chốt ngày |
| Giá sàn và trần giảm | Lotus chặt hơn OnePMS: chỉ quản trị chuỗi sửa giá sàn. Quản lý cơ sở không hạ sàn tại chi nhánh |

### 3.3. Lễ tân

OnePMS: tạo và sửa đặt phòng, nhận phòng, trả phòng. Xem và sửa hồ sơ khách. Tạo, sửa, in hóa đơn và tính tổng. Ghi dịch vụ vào hóa đơn. Xem báo cáo tình trạng phòng và lịch sử khách. Không xem báo cáo tài chính chi tiết.

| Việc | Lotus |
| --- | --- |
| Tạo, sửa, nhận, trả phòng | Đã có |
| Hồ sơ khách | Đã có. Lotus chỉ lưu loại giấy tờ và bốn số cuối |
| Hóa đơn lưu trú, ghi dịch vụ, thu tiền, in | Đã có |
| Sơ đồ phòng và lịch sử khách | Đã có |
| Giấu báo cáo tài chính chi tiết | Một phần. Menu lễ tân không có báo cáo doanh thu, nhưng API không chặn lễ tân gọi báo cáo vì lễ tân được đi qua `DepartmentAccess` |
| Bảo mật giá | Lotus có thêm: lễ tân không thấy tiền phòng và không trả phòng khi quản lý đã gắn bảo mật giá |

### 3.4. Kế toán

OnePMS: báo cáo tài chính, thu chi, doanh thu, kiểm tra hóa đơn. Lập báo cáo doanh thu, chi phí, lợi nhuận. Không sửa phòng và không sửa khách.

| Việc | Lotus |
| --- | --- |
| Báo cáo doanh thu, sổ quỹ, phiếu thu, công nợ, chốt ngày | Đã có trên menu và API |
| Phiếu thu, phiếu chi, ca thu ngân | Đã có |
| Lợi nhuận | Chưa có báo cáo lợi nhuận. Đang có doanh thu và tiền thu |
| Không sửa phòng, không sửa khách | Một phần. Kế toán không có menu sửa phòng. API kế toán vẫn được thu tiền, nhận cọc và trả phòng |
| Hóa đơn điện tử | Chỉ có bản nháp. Chưa có nhà cung cấp để phát hành |

### 3.5. Buồng phòng

OnePMS: cập nhật phòng trống, đang dọn, sẵn sàng. Ghi minibar và giặt ủi vào hóa đơn khách.

| Việc | Lotus |
| --- | --- |
| Đổi trạng thái vệ sinh | Đã có |
| Phiếu dọn, hạng mục, ảnh nghiệm thu | Đã có. Ảnh nghiệm thu là bắt buộc khi phiếu có hạng mục |
| Ghi minibar hoặc giặt ủi vào folio | Chưa có. API buồng phòng không có quyền ghi dịch vụ. Việc ghi dịch vụ đang thuộc lễ tân và quản lý |
| Nghiệm thu phòng đã dọn | Quản lý nghiệm thu. Buồng phòng không tự chuyển phòng sang đã nghiệm thu khi phiếu còn hạng mục |

### 3.6. Kinh doanh

OnePMS: tạo, sửa, xóa đặt phòng. Xem doanh thu từ khách và đối tác. Xem phòng trống và giá.

| Việc | Lotus |
| --- | --- |
| Tạo đặt phòng, hủy, thu cọc, tạo đoàn | Đã có trên API |
| Sửa nội dung một phiếu đã tạo | Một phần. Chưa có màn sửa tổng quát. Đổi giá và bảo mật giá thuộc quản lý |
| Xóa phiếu | Lotus hủy phiếu, không xóa dữ liệu |
| Phòng trống và bảng giá | Đã có |
| Báo cáo doanh thu theo khách và đại lý | Một phần. Có màn hiệu quả đại lý, nhưng menu kinh doanh không có báo cáo doanh thu |

### 3.7. Quản lý kho

OnePMS: nhập, xuất, sửa hàng tồn. Theo dõi số lượng và kiểm kê.

| Việc | Lotus |
| --- | --- |
| Vai quản lý kho | Chưa có |
| Nhập xuất tồn, nhà cung cấp, thẻ kho | Đã có chức năng, đang nằm ở quản lý |
| Kiểm kê và phiếu điều chỉnh tồn có người duyệt | Chưa đủ một quy trình kiểm kê riêng cho vai kho |

### 3.8. Người phê duyệt

OnePMS: xem đặt phòng chưa xác nhận, gồm khách, loại phòng, ngày đến và ngày đi. Duyệt hoặc từ chối. Thêm ghi chú hoặc yêu cầu bổ sung. Không được sửa nội dung phiếu.

| Việc | Lotus |
| --- | --- |
| Trạng thái chờ duyệt | Chưa có. Phiếu tạo xong là đã xác nhận |
| Vai chỉ duyệt hoặc từ chối | Chưa có |
| Ghi chú yêu cầu bổ sung trước khi duyệt | Đã có ghi chú đặt phòng, nhưng chưa gắn vào một bước duyệt |
| Cấm người duyệt sửa phiếu | Chưa có vì chưa có vai này |

### 3.9. Đối tác

OnePMS: đối tác đăng nhập, tạo, sửa, xóa yêu cầu đặt phòng. Phiếu ở trạng thái chưa phê duyệt, chờ quản lý hoặc người phê duyệt.

| Việc | Lotus |
| --- | --- |
| Tài khoản đối tác | Chưa có. Đại lý chỉ là danh mục do nhân viên khách sạn nhập |
| Đặt phòng chờ phê duyệt từ bên ngoài | Chưa có |

### 3.10. Đối tác nội bộ

OnePMS: giống đối tác, và được thêm thanh toán cùng dịch vụ trên phiếu chưa phê duyệt.

| Việc | Lotus |
| --- | --- |
| Vai đối tác nội bộ | Chưa có |
| Thu tiền và ghi dịch vụ trên phiếu chưa duyệt | Chưa có |

## 4. Cơ chế còn thiếu

Đây là phần OnePMS làm trên từng nhân viên, còn Lotus chưa có.

1. Tài khoản mới không có quyền cho đến khi được cấp.
2. Chọn một vị trí mẫu để gắn cả gói, rồi tick thêm hoặc bỏ từng quyền trên đúng người đó.
3. Cấp quyền lẻ mà không cần chọn vị trí. Danh sách chia theo mục và theo hành động, có ô tìm.
4. Màn xem quyền đang có của một nhân viên.
5. Bỏ tick là mất quyền đó sau khi lưu.
6. Một người giữ được quyền của lễ tân cộng một quyền của quản lý, ví dụ chỉ thêm “tạo tài khoản”.
7. API và menu cùng đọc một danh sách quyền. Giấu menu không được coi là đã chặn. Lễ tân hiện vẫn đi qua bộ lọc phòng ban trên API.
8. Bảng `action_permissions` phải được API đọc thật. Nếu làm theo OnePMS thì quyền phải gắn tới từng nhân viên, không chỉ tới vai.

## 5. Việc nên bổ sung

Làm theo thứ tự này thì không đụng Hotelink, hóa đơn điện tử, nhà hàng, spa hay tổng đài.

1. Danh mục quyền theo hành động, dùng chung cho menu và API. Quyền mẫu của bảy vai hiện tại là điểm xuất phát.
2. Màn phân quyền trên từng nhân viên: tìm quyền, tick, bỏ tick, xem quyền đã cấp, lưu.
3. Tài khoản mới chưa vào nghiệp vụ được cho đến khi có ít nhất một quyền.
4. Siết lễ tân đúng bài OnePMS: vẫn xem sơ đồ, lịch sử khách và hóa đơn lưu trú. Không xem báo cáo tài chính chi tiết. Chặn trên API, không chỉ giấu menu.
5. Siết kế toán: xem và lập chứng từ tài chính. Bỏ quyền sửa phòng, sửa khách, nhận phòng và trả phòng nếu không được tick thêm.
6. Cho buồng phòng ghi minibar và giặt ủi vào folio của khách đang ở. Không mở cả danh mục dịch vụ.
7. Cho kinh doanh xem báo cáo doanh thu theo khách và theo đại lý.
8. Thêm vai quản lý kho cho nhập, xuất, tồn và kiểm kê.
9. Thêm trạng thái đặt phòng chờ duyệt, vai người phê duyệt, rồi mới tới tài khoản đối tác và đối tác nội bộ.

## 6. Phần Lotus giữ lại

Bài OnePMS không có các việc sau. Không bỏ khi làm phân quyền.

- Vai bảo trì, khóa và mở phòng bảo trì.
- Quản trị chuỗi tách khỏi quản lý một cơ sở.
- Giá sàn và trần giảm chỉ quản trị chuỗi được sửa.
- Bảo mật giá phòng.
- Giao ca phải có người nhận xác nhận trước khi đóng ca.
- Không lưu số giấy tờ đầy đủ.
- Hủy phiếu và hoàn tiền là bút toán mới, không xóa dòng tiền đã ghi.
