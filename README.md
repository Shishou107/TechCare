# TechCare — Hệ Thống Quản Lý Trung Tâm Sửa Chữa Máy Tính Chuyên Nghiệp

Dự án TechCare là giải pháp phần mềm chuyên sâu dành cho chuỗi trung tâm sửa chữa máy tính, laptop và MacBook. Hệ thống tích hợp đầy đủ quy trình từ tiếp nhận trực tuyến, quét tự động cấu hình phần cứng 1-Click (.bat), quản lý phiếu sửa chữa, điều phối kỹ thuật viên, lập báo giá đa phiên bản, quản lý kho linh kiện, giao nhận chuyển phát toàn quốc (GHN Express) với bản đồ hành trình Leaflet, và trợ lý AI tư vấn tự động (RAG + Fallback).

---

## 1. Ngăn Xếp Công Nghệ (Tech Stack)

### Backend
- **Framework**: ASP.NET Core (.NET 10) Minimal APIs
- **ORM**: Entity Framework Core 10 (SQL Server)
- **Bảo mật & Xác thực**:
  - JWT Bearer Token (thời hạn 15 phút) kết hợp Sliding Session & Refresh Token Rotation (thời hạn 14 ngày, thu hồi sau 30 ngày).
  - 20-giây Grace Period Token Cache chống race condition khi xoay vòng token trong kết nối mạng trễ.
  - Phân quyền hạt nhân kết hợp Role-Based Access Control (RBAC) và Ma trận quyền hạn chi tiết (`CUSTOMER.*`, `REPAIR.*`, `QUOTE.*`, `INVENTORY.*`, `USER.*`, `KNOWLEDGE.*`, `SUPPORT.*`).
  - Refresh Token được bảo vệ qua Cookie `HttpOnly` an toàn; thuộc tính `refreshToken` tự động bị ẩn khỏi JSON response đối với client trình duyệt để triệt tiêu nguy cơ rò rỉ token qua XSS.
  - Chống Brute-force với Atomic Account Lockout counter (`ExecuteUpdateAsync`).
  - Endpoint Rate Limiting theo tiêu chuẩn OWASP (Login, Refresh Token, Tra cứu đơn, Chatbot AI).
  - Dịch vụ nền định kỳ (`SessionCleanupBackgroundService`) dọn dẹp các session và token đã hết hạn/thu hồi quá 30 ngày theo lô 1.000 dòng.
- **Tích hợp Vận Chuyển**: GHN Express API (Preview cước, tạo vận đơn 2 chiều, Webhook cập nhật trạng thái với cơ chế chống trùng lặp theo `(OrderCode, Status, OccurredAtUtc)`).
- **Trợ lý AI & RAG**:
  - Tích hợp chuẩn OpenAI-compatible Chat Completions và Text Embeddings.
  - Cơ chế dự phòng ngoại tuyến: Tự động chuyển đổi sang thuật toán băm FNV-1a 128 chiều (`local-hash-v2`) khi chưa cấu hình API key.
  - Ngưỡng tương đồng động: `0.72` cho mô hình AI chuẩn, `0.30` cho mô hình hash dự phòng (có thể tùy biến qua `Ai:MinSimilarityScore`).
  - Bộ lọc chống Prompt Injection ngăn chặn rò rỉ system prompt.
- **Validation**: FluentValidation pipeline tích hợp qua Minimal API `EndpointFilter`.

### Frontend
- **Framework**: React 19, TypeScript, Vite
- **Thiết kế**: CSS Grid & Flexbox thuần, monochrome tối giản, tối ưu theo Design Token (`--ink`, `--sub`, `--line`), không phụ thuộc thư viện UI cồng kềnh.
- **Tính năng nổi bật**:
  - Giao diện responsive 100% không cuộn ngang từ 360px (mobile) đến 1440px (desktop).
  - Form đặt lịch 2 bước gọn nhẹ: Bước 1 gửi đơn tức thì chỉ với 4 trường; Bước 2 thu gọn chứa thẻ Quét cấu hình máy 1-Click (.bat) và thông số phần cứng.
  - Theo dõi hành trình chuyển phát với bản đồ Leaflet / OpenStreetMap hiển thị tọa độ thực từ bưu cục trung chuyển, tự động ẩn khi chưa có tọa độ.
  - Quản trị cơ sở tri thức AI (RAG) với tính năng "Nhúng lại toàn bộ" phục vụ xoay vòng mô hình vector.
  - Tra cứu tiến độ sửa chữa bảo mật: yêu cầu bắt buộc cả Mã phiếu/đơn và Số điện thoại, che số điện thoại riêng tư (`090****567`).

---

## 2. Chuẩn Bị & Cài Đặt Cơ Sở Dữ Liệu

### Yêu cầu
- SQL Server 2019 / 2022 hoặc Azure SQL Database.
- SQL Server Management Studio (SSMS) hoặc Azure Data Studio.

### Thực thi Script SQL theo thứ tự (Thư mục `TechCare/database/`)
1. **Khởi tạo hệ thống (Init)**: Chạy file `database/TechCare_Database.sql` để tạo schema (`CRM`, `Repair`, `Inventory`, `Billing`, `Security`, `Comms`), các bảng nghiệp vụ, trigger, views và seed dữ liệu ban đầu.
2. **Migration 002 (Sessions & Token Rotation)**: Chạy `database/TechCare_Migration_002_Sessions.sql` để bổ sung bảng `Security.Sessions`, các cột bảo mật cho `RefreshTokens`, filtered unique index `TrackingCodeHash`.
3. **Migration 003 (Chatbot AI & Vận Chuyển)**: Chạy `database/TechCare_Migration_003_AiChatbot_Shipping_Roles.sql` để tạo bảng lưu trữ tài liệu tri thức `Ai.KnowledgeDocuments`, vector chunks `Ai.KnowledgeChunks`, phiên chat `Comms.ChatSessions`, tin nhắn `Comms.ChatMessages`, vận đơn `Shipping.Shipments`, sự kiện bưu cục `Shipping.ShipmentEvents` và phân quyền mở rộng.
4. **Migration 004 (Tọa độ Bản đồ & Geocoding)**: Chạy `database/TechCare_Migration_004_ShipmentCoordinates.sql` bổ sung các cột tọa độ `SenderLatitude`, `SenderLongitude`, `ReceiverLatitude`, `ReceiverLongitude` trong bảng `Shipping.Shipments` và `Latitude`, `Longitude` trong bảng `Shipping.ShipmentEvents`.

---

## 3. Cấu Hình Biến Môi Trường (Zero-Secret Repository)

Hệ thống áp dụng nguyên tắc **Zero-Secret** trên repository. Mọi khóa bảo mật đều được để trống trong `appsettings.json` và được truyền thông qua biến môi trường hoặc User Secrets. Ứng dụng áp dụng cơ chế **Fail-Closed** — từ chối khởi động nếu thiếu `Jwt:Key`.

### Danh sách Biến Môi Trường

| Biến môi trường | Mục đích | Bắt buộc? | Mặc định nếu bỏ trống |
|---|---|---|---|
| `TECHCARE_JWT_SECRET` hoặc `Jwt__Key` | Khóa ký JWT (tối thiểu 32 ký tự) | **Bắt buộc** | Không có (Fail-closed) |
| `TECHCARE_AI_API_KEY` hoặc `Ai__ApiKey` | API key OpenAI/Meta AI/Gemini | Tùy chọn | Dùng hash FNV-1a dự phòng (`local-hash-v2`) |
| `TECHCARE_AI_CHAT_URL` hoặc `Ai__ChatBaseUrl` | URL endpoint Chat Completions | Tùy chọn | `https://api.openai.com/v1/chat/completions` |
| `TECHCARE_AI_EMBEDDING_URL` | URL endpoint sinh Vector Embedding | Tùy chọn | `https://api.openai.com/v1/embeddings` |
| `TECHCARE_GHN_TOKEN` | Token xác thực đối tác GHN Express | Tùy chọn | Dùng mock provider nội bộ |
| `TECHCARE_GHN_SHOP_ID` | Mã cửa hàng GHN Shop ID | Tùy chọn | `0` |
| `TECHCARE_GHN_WEBHOOK_SECRET` | Chữ ký xác thực webhook GHN | Tùy chọn | Bỏ qua kiểm tra hash nếu chưa đặt |
| `Shipping__BranchLatitude` | Vĩ độ chi nhánh TechCare | Tùy chọn | `null` (Bản đồ tự ẩn nếu thiếu) |
| `Shipping__BranchLongitude` | Kinh độ chi nhánh TechCare | Tùy chọn | `null` (Bản đồ tự ẩn nếu thiếu) |

#### Thiết lập trong PowerShell (Windows)
```powershell
$env:TECHCARE_JWT_SECRET="TechCare_2026_Secure_Key_Production_9988_Safe_Rotated_Secret!"
$env:TECHCARE_AI_API_KEY="sk-..."
$env:TECHCARE_GHN_TOKEN="..."
$env:TECHCARE_GHN_SHOP_ID="123456"
```

#### Thiết lập bằng .NET User Secrets (Development)
```powershell
cd TechCare/API_FixComputerCenter
dotnet user-secrets set "Jwt:Key" "TechCare_2026_Secure_Key_Production_9988_Safe_Rotated_Secret!"
dotnet user-secrets set "Ai:ApiKey" "sk-..."
```

---

## 4. Hướng Dẫn Khởi Chạy

### 1. Khởi Chạy Backend API
Yêu cầu đã cài đặt [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
# 1. Đặt biến môi trường JWT Secret
$env:TECHCARE_JWT_SECRET="TechCare_2026_Secure_Key_Production_9988_Safe_Rotated_Secret!"

# 2. Khởi chạy API
cd TechCare/API_FixComputerCenter
dotnet restore
dotnet run
```
- API khởi chạy tại: `http://localhost:5000`.
- Tài liệu OpenAPI: `http://localhost:5000/openapi/v1.json`.
- Kiểm tra kết nối DB: `GET http://localhost:5000/api/v1/health/database`.

### 2. Khởi Chạy Frontend Web
Yêu cầu đã cài đặt [Node.js 18+](https://nodejs.org/).

```powershell
cd TechCare/TechCare.Web
npm install
npm run dev
```
- Ứng dụng web mở tại: `http://localhost:5173`.

### 3. Chạy Toàn Bộ Bộ Kiểm Thử (Unit & Integration Tests)
TechCare đi kèm bộ test toàn diện 24 ca kiểm thử kiểm tra tính đúng đắn của logic nghiệp vụ:

```powershell
dotnet test TechCare/API_FixComputerCenter.Tests/API_FixComputerCenter.Tests.csproj
```
Các hạng mục được kiểm thử:
- Webhook GHN: Chống ghi lặp sự kiện trùng lặp (`Webhook_DuplicateSubmission_DoesNotCreateDuplicateEvents`).
- Tạo đơn giao vận: Cơ chế Idempotency chống trùng đơn (`CreateOrder_CalledTwiceForSameAppointment_ReturnsExistingOrderWithoutCallingCarrierAgain`).
- Tọa độ chi nhánh: Xử lý `null` an toàn và đọc cấu hình linh hoạt (R2).
- Ánh xạ trạng thái GHN ↔ TechCare: Đúng chuẩn 100% cho mọi mã trạng thái của GHN Express.
- Thuật toán RAG Embedding: Tính hàm băm FNV-1a ổn định qua các lần restart, kiểm tra chiều vector, tương đồng cosine.
- Ngưỡng tương đồng động Chatbot: Chuyển đổi chính xác giữa chế độ fallback và model chuẩn (R1).
- Chống Prompt Injection: Bảo vệ ngữ cảnh hệ thống trước các câu hỏi cố tình khai thác.
- Bảo mật Token: Ẩn hoàn toàn `refreshToken` trong body JSON đối với client web (R3).

---

## 5. Tài Khoản Thử Nghiệm Mặc Định (Seed Data)

| Vai trò | Tên đăng nhập | Mật khẩu mặc định | Quyền hạn tiêu biểu |
|---|---|---|---|
| **ADMIN** | `admin` | `Admin@123` | Toàn quyền quản trị hệ thống, người dùng, phiên và cấu hình tri thức |
| **QUẢN LÝ** | `manager` | `Manager@123` | Quản lý sửa chữa, duyệt báo giá, xem báo cáo thống kê |
| **KỸ THUẬT VIÊN** | `technician` | `Tech@123` | Chẩn đoán máy, cập nhật tiến trình sửa, xem linh kiện |
| **KHO LINH KIỆN** | `inventory` | `Stock@123` | Xem tồn kho, xuất linh kiện cho phiếu sửa chữa |
| **LỄ TÂN / THU NGÂN** | `reception` | `Staff@123` | Tiếp nhận máy tại quầy, quản lý lịch hẹn, xuất biên nhận |

---

## 6. Cấu Trúc Thư Mục Dự Án

```
TechCare/
├── database/                                     # Toàn bộ script SQL khởi tạo & migration CSDL
│   ├── TechCare_Database.sql                     # Script khởi tạo toàn bộ CSDL TechCare
│   ├── TechCare_Migration_002_Sessions.sql       # Script migration v2 (Sessions, Token Rotation)
│   ├── TechCare_Migration_003_AiChatbot_Shipping_Roles.sql # Script migration v3 (Chatbot, Shipping, Roles)
│   └── TechCare_Migration_004_ShipmentCoordinates.sql      # Script migration v4 (Tọa độ vận chuyển)
├── API_FixComputerCenter/                        # Backend API (.NET 10)
│   ├── Application/
│   │   ├── Common/                               # Exceptions, Interfaces, Validators dùng chung
│   │   └── Features/                             # Vertical Slices: AiChat, Auth, Appointments, Customers, Devices, Diagnostics, Inventory, Knowledge, Quotations, Repairs, Shipping
│   ├── Domain/                                   # Entities và Domain Exceptions
│   ├── Infrastructure/                           # EF Core DbContext, Shipping, Hosted Services, Audit
│   ├── Endpoints/                                # Minimal API route definitions & security mapping
│   ├── Security/                                 # Permission Requirements & Authorization Handlers
│   ├── DependencyInjection.cs                    # Đăng ký dịch vụ, RateLimiter, CORS, JWT
│   └── appsettings.json                          # Cấu hình hệ thống (Zero-Secret)
├── API_FixComputerCenter.Tests/                  # Bộ kiểm thử tích hợp & kiểm thử đơn vị (24 tests)
│   ├── AuthResponseTests.cs                      # Test bảo mật ẩn refresh token (R3)
│   ├── RagKnowledgeTests.cs                      # Test RAG AI, vector FNV-1a, ngưỡng động (R1)
│   ├── ShippingTests.cs                          # Test GHN idempotency, webhook deduplication, tọa độ (R2)
│   └── ShippingStatusMappingTests.cs             # Test ánh xạ trạng thái vận chuyển
└── TechCare.Web/                                 # Frontend React 19 + TypeScript + Vite
    ├── src/
    │   ├── api/client.ts                         # Base API client configuration
    │   ├── components/                           # BookingSection (2-step), TrackingSection (Bản đồ Leaflet), KnowledgeAdmin (RAG)
    │   ├── data/notifications.ts                 # Dữ liệu thông báo hệ thống cấu trúc hóa
    │   ├── App.tsx                               # Main application component & Navigation
    │   └── App.css                               # Responsive design monochrome
    └── index.html                                # HTML entry point (TechCare branding)
```
