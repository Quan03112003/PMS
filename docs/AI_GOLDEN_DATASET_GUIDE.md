# Bộ câu hỏi chuẩn

Ba kho tách nhau:

- Kho tri thức: đoạn để tìm khi trả lời.
- Golden Dataset: câu hỏi và đáp án để chấm. Trạng thái `DRAFT`, `IN_REVIEW`, `APPROVED`, `ARCHIVED`.
- Training Dataset: chưa tạo. Không fine-tune ở đợt này.

Thêm câu trên màn Kho tri thức AI hoặc nhập `qa/ai-assistant/golden-dataset-template.csv`. Lần nhập luôn để `DRAFT`. Không có mẫu nào được đánh `APPROVED` sẵn trong code.

Chỉ dòng `APPROVED` đi vào lần chấm. Sửa một mẫu đã duyệt sẽ kéo về `DRAFT` khi lưu lại từ API `PATCH`.
