# Lotus AI Assistant

Trợ lý nằm trong Lotus PMS. Nhân viên đã đăng nhập mở khung chat. Máy chủ giữ hội thoại, tìm tài liệu đã duyệt, rồi mới soạn câu trả lời. Trợ lý không tạo đặt phòng, không nhận phòng, không thu tiền và không đọc hồ sơ khách.

## Luồng

1. Angular gọi `POST /api/ai/chat` qua `ApiService`. Trình duyệt không gọi model.
2. API lấy người dùng từ JWT. Cơ sở của lễ tân lấy từ token, không tin `propertyId` nếu tài khoản đã gắn một cơ sở.
3. Tìm đoạn trong `ai_knowledge_chunks` của tài liệu `APPROVED`. Cách tìm hiện tại là **lexical**: đếm từ trùng. Đây không phải semantic search và không dùng pgvector.
4. Câu hỏi về chức năng lấy các bước đúng với menu và nút đang có. Câu hỏi về trạng thái phòng lấy dữ liệu đang lưu của cơ sở đang chọn: số phòng, buồng, mã phiếu và trạng thái. Không đưa tên khách, giấy tờ hay số tiền cho model.
5. Không có chức năng và cũng không có dữ liệu khớp thì trả đúng câu từ chối trong đặc tả.
6. Câu hỏi cách dùng thì model local soạn từ các bước đó. Nếu model không trả lời được thì trả lại đúng các bước. Câu hỏi trạng thái trả đúng dữ liệu đang lưu, không chờ model bịa thêm.

`IEmbeddingService` đang tắt. Bật vector search chỉ sau khi Neon có pgvector và có lần đo riêng.

## Bảng

`sql/20-ai-assistant-postgres.sql` tạo các bảng `ai_*`. Script không sửa bảng đặt phòng hay folio. Database đã có dữ liệu không dùng `EnsureCreated` để thêm bảng này. Chưa chạy script thì API trợ lý lỗi khi ghi.

## Quyền

Mọi tài khoản đã đăng nhập gọi được `/api/ai`. `/api/admin/ai` chỉ `CHAIN_ADMIN` và `PROPERTY_MANAGER`. Hội thoại chỉ đọc và xóa bởi đúng người tạo.
