# TechCare — Các vấn đề còn lại sau Đợt 4

> Cập nhật 03/11/2026, sau rà soát commit `175b8a6`. File này thay thế vai trò "danh sách tồn đọng" của file thống kê 21 hạng mục cũ: những mục đã đóng được ghi tóm tắt ở cuối để đối chiếu, phần chính là những gì **còn lại thật sự**.
> Mức độ: 🔴 nên sửa trước khi cho khách dùng thật · 🟠 nên xử lý sớm · 🟡 dài hạn/không chặn vận hành.

## A. Lệch/lỗi cần sửa code (3 mục)

### 🔴 R1. Ngưỡng tương đồng chatbot bị hạ xuống 0.30 và ghi cứng
- Bằng chứng: `AiChat/IAiChatService.cs` gọi `SearchChunksAsync(..., topK: 5, threshold: 0.30)`; khóa cấu hình `Ai:MinSimilarityScore` (đã có trong file cấu hình mẫu) không được đọc ở đâu.
- Hệ quả: khi chạy vector hash dự phòng thì 0.30 là hợp lý, nhưng khi điền khóa AI thật (vector ngữ nghĩa chuẩn), 0.30 quá dễ dãi → đoạn tri thức liên quan yếu cũng lọt, bot dễ trả lời lạc đề hoặc tổng hợp từ ngữ cảnh kém.
- Cách sửa (nhỏ): ngưỡng đọc từ cấu hình; mặc định theo chế độ — model thật `0.72`, dự phòng hash `0.30` (phân biệt qua `IEmbeddingService.CurrentModelName`/có khóa hay không). Ước lượng: dưới 1 giờ cho agent.

### 🟡 R2. Tọa độ chi nhánh còn giá trị mặc định ghi cứng trong code
- Bằng chứng: `Shipping/ShippingService.cs` — `_config.GetValue<decimal?>("Shipping:BranchLatitude") ?? 21.0280m` (kèm kinh độ tương tự).
- Hệ quả: nếu thiếu cấu hình, hệ thống lặng lẽ dùng tọa độ mặc định (khu vực Hoàn Kiếm, Hà Nội) thay vì báo thiếu — tiệm ở nơi khác là bản đồ sai mà không ai biết.
- Cách sửa: bỏ giá trị dự phòng trong code; thiếu cấu hình → ghi log cảnh báo + để tọa độ NULL (bản đồ tự ẩn theo cơ chế FIX-1). Cấu hình trong appsettings hiện đã có sẵn nên sửa xong không đổi hành vi hiện tại.

### 🟠 R3. (Tồn từ file 21 mục — A3) Refresh token vẫn trả trong body song song với cookie
- Bằng chứng: `AuthService` vẫn khai `AuthResponse(..., string? RefreshToken, ...)` và login/refresh trả token thật trong body, trong khi cookie HttpOnly cũng được set.
- Hệ quả: với client trình duyệt, JS đọc được refresh token thì lớp bảo vệ HttpOnly mất ý nghĩa (XSS là mất token).
- Cách sửa: ở endpoint, nếu request là từ trình duyệt (đi qua cookie) thì loại refresh token khỏi body trả về; chỉ giữ cho kênh non-browser khai báo rõ ràng. Chưa đợt nào xử lý mục này vì các đợt trước ưu tiên tính năng mới.

## B. Nợ kiểm chứng — chưa có bằng chứng chạy thật (5 mục)

Các mục này không phải lỗi đã thấy, mà là **chưa ai xác nhận bằng chạy thật**; Kavi không chạy được .NET/SQL Server/GHN từ môi trường của mình.

- **R4. `dotnet test` toàn bộ:** test đã viết đủ (shipping, webhook, RAG, ánh xạ) nhưng chưa có kết quả chạy xanh được ghi lại ở máy Nicole. Cần chạy một lần và giữ kết quả.
- **R5. Migration 002/003/004 trên DB thật:** script đã có trong repo thư mục `database/`, nhưng trạng thái đã import vào DB dev/DB chính nào chưa được xác nhận. Kiểm tra bằng các SELECT ở cuối mỗi file migration.
- **R6. GHN staging thật:** code và test hiện ở mức mock; chưa có bằng chứng một vận đơn staging thật được tạo, webhook thật bắn về, in nhãn thật. Cần một buổi nghiệm thu staging trước khi tính chuyện production.
- **R7. Chatbot ở chế độ LLM thật:** mới kiểm chứng tĩnh (đúng cấu trúc gọi API). Sau khi điền khóa AI: bắt buộc chạy endpoint nhúng lại toàn bộ (FIX-3) rồi hỏi thử vài câu trong/ngoài tri thức để xác nhận ngưỡng (liên quan R1) và chất lượng trả lời.
- **R8. Hai race của nhóm auth (A7/A8 trong file cũ):** lockout đếm atomic và race giới hạn 5 session/version báo giá — chưa được rà soát lại kể từ bản thống kê 21 mục (các đợt sau tập trung tính năng mới). Cần một lượt kiểm chứng riêng, không khẳng định đã sửa hay chưa.

## C. Việc vận hành thuộc về Nicole (không phải lỗi code) (3 mục)

- **R9. Điền khóa thật qua user-secrets/biến môi trường:** GHN token + ShopId + webhook secret; khóa AI. (JWT: điều kiện cũ vẫn còn — giá trị đang chạy phải là secret mới, chưa từng lộ.)
- **R10. GHN production:** staging và production khác base URL/ShopId và cần tài khoản/kho production được kích hoạt. Chỉ chuyển sau khi R6 đạt.
- **R11. Tài khoản test theo role:** để nghiệm thu trọn Đợt 3 (đăng nhập từng role vào đúng khu vực) cần mỗi role ít nhất một tài khoản thử trên DB đang dùng.

## D. Hoàn thiện dài hạn — không chặn vận hành (5 mục)

- **R12. CI (FIX-4b của Đợt 4):** chưa làm vì là mục tùy chọn chờ xác nhận — thêm GitHub Actions build + test 2 phía khi muốn khóa chất lượng tự động.
- **R13. Bộ test concurrency cho auth/session (T-01 → T-12)** trong tài liệu thiết kế token: vẫn chưa được viết; test hiện có mới phủ vận chuyển và RAG.
- **R14. Trang nghiệp vụ sâu cho từng khu vực role:** Đợt 3 mới dựng khung + điều hướng (đúng phạm vi đã duyệt). Các trang chi tiết của `/kho`, `/thu-ngan`, `/tai-khoan` và tổng quan có số liệu thật cần một đợt phát triển riêng theo ma trận role khi bắt đầu vận hành thật.
- **R15. Serial linh kiện đang lưu dạng JSON trong ghi chú thiết bị:** chạy tốt hiện tại; khi cần báo cáo/truy vấn theo serial (vd tra "serial SSD này đã qua tay tiệm chưa") thì tách bảng riêng `Repair.DeviceComponentSerials`.
- **R16. Độ phủ của từ điển tọa độ sự kiện:** hiện ở mức tỉnh/thành + hub lớn → ghim bản đồ chính xác tới mức đó. Bổ sung dần tọa độ quận/huyện/kho khi cần chi tiết hơn. Kèm: repo đã có `.gitignore` gốc và thư mục `database/`, nhưng **README gốc** hướng dẫn dựng dự án vẫn chưa có.

## Đã đóng kể từ file thống kê 21 hạng mục (đối chiếu nhanh)

A1 secret (đóng đúng cách ở `d99a485`) · A2 grace refresh · A4 phân quyền theo permission · A5 validation · A6 tra cứu bắt buộc mã + SĐT · B1 script SQL (đã có + đã lên repo) · B2 job dọn session · B4 file thiếu trên repo (gần hết, trừ README gốc — R16) · B6 dead code + serial · C1–C7 giao diện (qua các prompt và Đợt 1–3) · rate limit cho login/refresh/tra cứu/chat/tính phí (đã có đủ ở bản hiện tại) · toàn bộ FIX-1 → FIX-4 của Đợt 4 (trừ điểm lệch R1/R2 ghi ở trên).

**Thứ tự xử lý đề xuất:** R1 + R2 (một lượt vá nhỏ cho agent) → R4 + R5 (chạy kiểm chứng ở máy Nicole, không tốn công code) → R9 + R7 (điền khóa AI, nhúng lại, thử chatbot thật) → R6 (nghiệm thu GHN staging) → R3 (vá bảo mật khi rảnh tay) → nhóm D theo nhu cầu vận hành.
