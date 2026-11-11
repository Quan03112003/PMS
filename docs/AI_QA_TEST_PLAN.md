# Kế hoạch kiểm thử AI Assistant

Mỗi dòng khi chạy thật cần Expected, Actual, Status và bằng chứng. HTTP 200 chưa đủ để ghi Passed.

| Nhóm | Việc | Kết quả mong đợi |
| --- | --- | --- |
| UI | Mở và đóng khung, Enter gửi, Shift+Enter xuống dòng | Không đổi màn hình PMS |
| Chức năng | Hỏi khi chưa có tài liệu duyệt | Đúng câu từ chối trong đặc tả |
| Chất lượng | Câu không có trong kho | Không bịa bước thao tác |
| Bảo mật | Gọi `/api/ai/chat` không token | 401 |
| Bảo mật | Lễ tân gọi API quản trị kho | 403 |
| Bảo mật | Xem hội thoại của người khác | 404 |
| Hồi quy | Đăng nhập, sơ đồ, folio, buồng phòng | Luồng cũ vẫn mở |

Đánh giá tự động hiện chấm từ khóa có nằm trong đoạn tìm được hay không. Nó không để model tự chấm model.
