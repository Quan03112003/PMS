# Cài đặt AI Assistant

Nhánh làm việc: `feature/ai-assistant`. Chưa đẩy production.

## Giao diện

- `frontend/src/app/features/ai-assistant/ai-assistant-widget.component.*` là khung chat.
- Nút nổi góc phải và nút Trợ giúp vận hành cùng mở khung.
- `ai-knowledge-admin.component.*` là màn Kho tri thức AI trong menu Hệ thống, chỉ quản lý thấy.

## Máy chủ

- Thực thể trong `backend/src/LotusPms.Domain/Entities/AiEntities.cs`.
- Cắt đoạn trong `LotusPms.Application/Ai/KnowledgeChunker.cs`.
- API và dịch vụ trong `backend/HotelPms.Api/Ai/`.
- `StaffAccess` cho phép `/api/ai` với mọi người đã đăng nhập. Quyền quản trị vẫn kiểm trong từng endpoint.

## Việc chưa làm

- Model soạn câu chạy trong API bằng LLamaSharp CPU. File `models/qwen2.5-0.5b-instruct-q4_k_m.gguf` không đưa vào git. Thiếu file thì chỉ trích tài liệu đã duyệt.
- Chưa bật pgvector.
- Chưa đọc PDF và XLSX.
- Chưa fine-tune.
- Chưa có mẫu Golden `APPROVED`. Mẫu nhập vào là `DRAFT`.
