# Tổng hợp tài liệu PMS và phần Lotus còn thiếu

Ngày tổng hợp: 11/11/2026.

Bản này gom các tài liệu công khai đã đọc được. Bài giảng trên Studocu chỉ mở được phần xem trước, nên không dùng làm nguồn chi tiết. Không chép nguyên văn bài giảng hay hướng dẫn của nhà cung cấp.

## 1. Nguồn đã xem

| Nguồn | Đường dẫn | Đọc được |
| --- | --- | --- |
| Bài giảng PMQLKS, Trường Đại học Tài chính – Marketing, 09/2025 | Trang Studocu bài *Bài giảng phần mềm quản lý khách sạn* | Mục lục bốn chương và một phần chương 1, chương 2. Chương 3 và chương 4 chỉ thấy tên mục |
| OnePMS, bảng tính năng | https://onepms.net/bang-gia/ và https://onepms.net/phan-mem-quan-ly-khach-san-onepms/ | Danh mục module và cách họ tách OnePMS, OneCMS, OneBE |
| OnePMS, hướng dẫn vận hành | Đặt phòng, phân quyền, đặt phòng ảo, công nợ, lưu chuyển tiền tệ trên onepms.net/docs | Đủ để hiểu nghiệp vụ, không đủ để coi là đặc tả API |
| ASI PMS, Anand Systems | https://anandsystems.com/vi/asipms và bài *Getting Started with ASI PMS* | Module lễ tân, buồng phòng, kiểm toán, báo cáo, tích hợp |
| ASI FrontDesk, mô tả tính năng | ITQlick và CNET | Đặt nhiều phòng, tách folio, sổ công nợ, khóa cửa, máy quét giấy tờ |

## 2. Bốn nguồn cùng mô tả một PMS

Một PMS đủ dùng cho khách sạn độc lập hoặc chuỗi nhỏ gồm bảy khối:

1. **Lễ tân.** Sơ đồ phòng theo màu. Đặt lẻ và đoàn. Nhận phòng, trả phòng, đổi phòng. Giá theo giờ, ngày, và các gói dài hơn. Hóa đơn cập nhật ngay khi phát sinh tiền.
2. **Khách.** Hồ sơ lưu trú, lịch sử khách quay lại, công nợ theo khách và theo nguồn đặt.
3. **Buồng phòng và bảo trì.** Trạng thái sạch, bẩn, hỏng. Giao việc. Minibar và vật tư trong phòng. Báo lễ tân khi phòng sẵn sàng.
4. **Dịch vụ.** Minibar, giặt ủi, thuê xe, điện nước, và khoản thu không gắn phòng. Tiền vào hóa đơn khách hoặc vào một folio riêng.
5. **Tiền.** Thu tại quầy, cọc, nhiều cách trả, ca thu ngân, dòng tiền theo quỹ, chi phí, công nợ, chốt ngày.
6. **Báo cáo.** Doanh thu theo thời gian, nguồn, hạng phòng. Công suất. Thuế. Khách đang ở. Xuất bảng cho kế toán.
7. **Quyền.** Mỗi người một danh sách việc được làm. Tài khoản mới chưa có quyền cho đến khi được cấp.

Phần nối ra ngoài, cả OnePMS và ASI đều tách khỏi lõi lễ tân: kênh OTA, máy bán hàng nhà hàng, hóa đơn điện tử, cổng thanh toán, khóa cửa, máy quét hộ chiếu. Các phần này cần tài khoản hoặc thiết bị của đối tác.

## 3. Điểm từng nguồn nói rõ hơn bài giảng

### OnePMS

- Đặt phòng trên sơ đồ theo ngày, hoặc tạo nhanh không cần bấm vào ô phòng.
- Ở từ hai đêm trở lên thì sửa được giá từng đêm.
- Đặt phòng ảo là folio không chiếm phòng. Dùng cho spa, thuê xe, quà lưu niệm, minibar bán lẻ. Có giảm giá và thu tiền riêng. Doanh thu này không trộn vào tiền phòng.
- Công nợ ghi khi thu bằng City Ledger. Theo dõi theo phiếu và theo dịch vụ. Lọc theo nguồn đặt và trạng thái. Thu một phần thì dư nợ giảm. Xuất Excel.
- Dòng tiền theo từng quỹ: số dư, nạp, rút, chuyển quỹ, lọc thu chi, xuất Excel.
- Buồng phòng trên điện thoại: sạch, bẩn, lịch dọn, minibar, phòng đang sửa.
- Dịch vụ gắn hóa đơn: minibar, giặt ủi, thuê xe, điện, nước.
- Hóa đơn điện tử họ nêu Easy Invoice, Fast Invoice, VNPT. Đây là tên nhà cung cấp, chưa phải khóa API.
- Kênh bán là sản phẩm OneCMS, nối Hotel Link và khoảng 77 kênh. Web đặt phòng là OneBE. Máy bán hàng là OneRES. Ba phần này là gói riêng, không nằm trong lõi OnePMS.

Phân quyền OnePMS đã được đối chiếu riêng trong `docs/DOI_CHIEU_PHAN_QUYEN_ONEPMS.md`.

### ASI

- Sơ đồ kéo thả, mã màu, nhận phòng nhanh, tìm phiếu theo tên, số phiếu hoặc ngày.
- Đoàn có folio chung và folio từng phòng. Sửa phân phòng của đoàn. In nhiều hóa đơn một lúc.
- Đổi phòng, trả phòng muộn có phụ thu, khóa phòng khỏi quỹ bán khi sửa chữa.
- Chốt đêm ghi cả doanh thu no-show và hủy, không chỉ tiền phòng của khách đang ở.
- Buồng phòng giao việc theo ca, tầng, ưu tiên, rồi báo cáo năng suất.
- Sổ khách, sổ cọc, sổ phải thu. Hoàn tác một bút toán sai bằng bút toán đảo, không xóa dòng gốc.
- Báo cáo có lãi lỗ, thuế, hoạt động trong ngày.
- Tích hợp họ liệt kê: OTA, GDS, cổng thanh toán, khóa cửa, máy quét giấy tờ, phần mềm kế toán, báo cáo chính phủ. Mỗi mục cần đối tác hoặc thiết bị.

### Bài giảng

Phần xem trước khớp với hai nguồn trên: PMS thay sổ giấy, phòng theo tầng và màu, lễ tân nhớ khách cũ, buồng phòng nhận việc khi khách trả phòng, nhà hàng cộng tiền vào phòng. ASI được đem ra làm bài tập, với giá giờ, ngày, tuần, tháng, nhiều mức giá, và ngày khóa sổ đoàn để nhả phòng chưa xác nhận. Phần AI, chatbot và robot chỉ là ví dụ thị trường.

## 4. Lotus đang đứng ở đâu

| Khối | Lotus hiện có | Còn thiếu so với các nguồn |
| --- | --- | --- |
| Lễ tân | Sơ đồ màu, lọc trạng thái, đặt giờ và ngày, đoàn cơ bản, nhận trả, đổi phòng, giữ phòng, cọc | Giá từng đêm trong một phiếu. Gói tuần và tháng. Ngày khóa sổ đoàn tự nhả phòng chưa xác nhận. Phụ thu trả muộn. No-show thành một trạng thái và một khoản doanh thu |
| Khách | Hồ sơ, quốc tịch, bốn số cuối giấy tờ, cột nợ | Sở thích và yêu cầu của khách quay lại. Công nợ mở bằng một phương thức thu, rồi thu dần và xuất danh sách |
| Buồng | Phiếu dọn, ảnh nghiệm thu, bảo trì | Giao việc trên điện thoại theo ca và tầng. Buồng phòng tự ghi minibar vào folio. Cảnh báo vật tư trong phòng sắp hết |
| Dịch vụ | Danh mục dịch vụ, minibar trừ kho, bán lẻ ngoài phòng | Folio không chiếm phòng cho khoản thu ngoài lưu trú, tách khỏi tiền phòng |
| Tiền | Ca, giao ca có người nhận, phiếu thu chi, hoàn tiền có duyệt, giá sàn, bảo mật giá | Nhiều quỹ và chuyển tiền giữa các quỹ. Báo cáo dòng tiền. Báo cáo lãi lỗ |
| Báo cáo | Doanh thu, quốc tịch, OCC, ADR, RevPAR trên chốt ngày | Biểu đồ. Lãi lỗ. Doanh thu no-show và hủy |
| Quyền | Bảy vai cố định. Bảng `action_permissions` chưa được API dùng | Tick quyền trên từng người. Tài khoản mới chưa có quyền. Vai kho, người duyệt, đối tác |

## 5. Việc làm được trên Lotus, không cần hợp đồng

Thứ tự này bám các nguồn, và không đụng kênh OTA, hóa đơn điện tử, nhà hàng, spa hay tổng đài.

1. Tick quyền trên từng nhân viên. Siết lễ tân và kế toán đúng việc họ được làm. Chi tiết nằm ở file đối chiếu OnePMS.
2. Sửa giá từng đêm trên một phiếu ở nhiều đêm. Giá đã chốt không đổi khi bảng giá đổi sau đó. Vẫn tôn trọng giá sàn và bảo mật giá.
3. Folio không chiếm phòng cho thu ngoài lưu trú. Giảm giá và thu tiền trên folio đó. Doanh thu không cộng vào tiền phòng.
4. Công nợ theo khách: ghi nợ, xem theo phiếu, thu một phần, danh sách lọc được.
5. Quỹ tiền: số dư từng quỹ, nạp, rút, chuyển quỹ, báo cáo thu chi.
6. Buồng phòng ghi minibar và giặt ủi vào folio khách đang ở.
7. No-show và phụ thu trả muộn, mỗi thứ một quy tắc và một dòng tiền, không xóa phiếu.
8. Ngày khóa sổ đoàn: phòng chưa xác nhận được nhả khi quá ngày, không tự hủy cả đoàn.
9. Hồ sơ khách quay lại: sở thích và yêu cầu đặc biệt, hiện lại khi đặt phòng mới.

## 6. Việc các nguồn đều gắn với đối tác hoặc thiết bị

Chưa làm khi chưa có tài khoản hoặc máy:

- OneCMS, Hotel Link, GDS, Booking.com, Agoda.
- Hóa đơn điện tử Easy Invoice, Fast Invoice, VNPT, Viettel, MISA.
- OneRES hoặc POS nhà hàng tự cộng vào phòng.
- Cổng thanh toán trừ tiền thật.
- Khóa cửa, máy quét hộ chiếu, chữ ký điện tử.
- AI đổi giá theo cung cầu.

Lotus đã có lớp sandbox kênh bán và hóa đơn nháp. Hai lớp này chưa phát hành ra hệ thống đối tác.
