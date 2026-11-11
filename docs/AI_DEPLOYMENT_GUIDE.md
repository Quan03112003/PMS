# Triển khai AI Assistant

Chưa triển khai production trong đợt này.

1. Chạy `sql/20-ai-assistant-postgres.sql` trên database thử. Sao lưu trước. Không chạy lên Neon cho đến khi được duyệt.
2. Model là file GGUF chạy trong API (`AI__ModelPath`). Không đặt khóa ChatGPT. Không commit file model.
3. Render free không đủ RAM để nhồi model. `AI__ModelPath` để trống thì chat chỉ trả tài liệu đã duyệt hoặc câu từ chối.
4. Máy local dùng `models/qwen2.5-0.5b-instruct-q4_k_m.gguf` ở gốc repo. Máy này có 8 GB RAM nên chỉ chạy bản 0.5B trên CPU.
5. Frontend vẫn gọi `/api/*`. Không tải model về trình duyệt.

Health `/api/health/ready` không đọc bảng `ai_*`, nên thiếu script không làm readiness 503. Riêng API trợ lý sẽ lỗi cho đến khi script đã chạy.
