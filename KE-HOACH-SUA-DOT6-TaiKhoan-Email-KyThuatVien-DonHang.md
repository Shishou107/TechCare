# TechCare — Đợt 6: Tài khoản kiểu thông thường (xác thực email) + Kỹ thuật viên phải xuất hiện + Đơn hàng đầy đủ yếu tố

> Chỉ đạo của Nicole (03/11/2026, kèm 2 ảnh chụp): bỏ kiểu đăng nhập OTP qua SĐT ("cứ làm kiểu bình thường đi — xác thực email, SĐT điền không cần OTP nhưng phải đúng định dạng"); phần chọn kỹ thuật viên đang trống trơn dù đã có kỹ thuật viên trong hệ thống; kiểm tra mật khẩu đã mã hóa chưa; chừa chỗ cấu hình email trong Cài đặt để cô ấy đưa cấu hình sau; kiểm kỹ luồng tạo đơn và theo dõi vận đơn từ hãng ship trả về API.
> Kavi đã kiểm chứng code ở HEAD `ae67c54` trước khi viết. Quy tắc phân công giữ nguyên: Antigravity code + test + commit, Nicole import SQL và điền cấu hình, Kavi rà soát độc lập. File SQL đi kèm: `TechCare_Migration_006_EmailAccounts_TechnicianBackfill.sql` (viết theo đúng quy ước đặt tên của họ script trong repo — từ nay repo là nguồn sự thật duy nhất cho SQL).

## 0. Kết quả kiểm tra các câu hỏi của Nicole (đã xác minh trong code)

1. **Mật khẩu: ĐÃ mã hóa đúng chuẩn.** Hệ thống băm bằng PBKDF2-SHA256 kèm salt riêng từng tài khoản (`AuthService.HashPassword`), không lưu mật khẩu thô ở đâu cả. Không cần sửa.
2. **Vì sao mục chọn kỹ thuật viên trống (ảnh 1):** tạo user có role TECHNICIAN **không hề sinh hồ sơ** `TechnicianProfiles`, mà API public chỉ đọc từ bảng hồ sơ → danh sách luôn rỗng → form chỉ còn ô "Để TechCare sắp xếp". Đây là lỗi luồng nghiệp vụ, sửa ở Mục 2.
3. **Vì sao gửi mã OTP thất bại (ảnh 2):** API không ghi được xuống DB trên máy Nicole (bảng OTP chưa tồn tại vì DB chưa được nạp đúng — cùng gốc với vụ chạy nhầm DB `master`). Đợt này bỏ hẳn OTP nên lỗi biến mất theo; đồng thời thông điệp lỗi ở modal đăng nhập phải rõ ràng hơn (Mục 1.6).
4. **Theo dõi vận đơn từ hãng ship:** chuỗi code đã đủ — webhook GHN (`POST /api/v1/shipping/webhooks/ghn`) → ghi `ShipmentEvents` → trang tra cứu hiển thị timeline + bản đồ. Phần còn thiếu là **chạy thử với GHN staging thật** (nợ kiểm chứng cũ, nằm ở kế hoạch test, không phải việc code đợt này).
5. **Tạo đơn hiện tại:** đủ các yếu tố tiếp nhận tại quầy (khách, SĐT, email, model, serial máy + serial SSD/RAM, mô tả lỗi, lịch hẹn, chọn kỹ thuật viên). Thiếu 2 yếu tố để vận hành đầy đủ: *hình thức gửi máy* và *địa chỉ khi không đến quầy* — bổ sung ở Mục 3.

## 1. Đợt 6A — Tài khoản khách kiểu thông thường (thay thế OTP)

**1.1 Đăng ký (public):** Họ tên, Email, Số điện thoại, Mật khẩu, Nhập lại mật khẩu.
- SĐT: kiểm tra định dạng Việt Nam `^0(3|5|7|8|9)\d{8}$` ở cả frontend (FluentValidation phía server là chốt cuối). SĐT chỉ là thông tin liên hệ — **không OTP**.
- Email: định dạng hợp lệ + không trùng tài khoản khác (kiểm tra theo `NormalizedEmail`).
- Mật khẩu: tối thiểu 8 ký tự, có chữ và số; băm PBKDF2 như hiện tại.
- Tạo `Users` role CUSTOMER, `UserName` = email, `IsEmailConfirmed = 0` (cột đã có sẵn trong schema). Liên kết hồ sơ CRM: khớp `Customers` theo SĐT trước, theo email sau → set `Customers.UserId`; chưa có thì tạo hồ sơ khách mới từ thông tin đăng ký.
- Gửi **mã xác thực 6 số về email** (hạn 15 phút, tối đa 5 lần nhập sai, lưu băm SHA-256 vào `Security.EmailTokens` — migration 006). Chưa xác thực → chưa đăng nhập được, trang đăng nhập có nút "Gửi lại mã".

**1.2 Kênh gửi email (để chừa cấu hình cho Nicole):**
- Interface `IEmailSender` + 2 implementation: `LogOnlyEmailSender` (chỉ ghi log server, tự động dùng khi chưa có cấu hình SMTP — phục vụ dev/test) và `SmtpEmailSender` (đọc cấu hình, gửi thật).
- Khóa cấu hình qua env/user-secrets, KHÔNG commit giá trị thật: `TECHCARE_SMTP_HOST`, `TECHCARE_SMTP_PORT`, `TECHCARE_SMTP_USER`, `TECHCARE_SMTP_PASSWORD`, `TECHCARE_SMTP_FROM`, `TECHCARE_SMTP_FROM_NAME`, `TECHCARE_SMTP_ENABLE_SSL`. Bổ sung các khóa này (dạng placeholder) vào `appsettings.Integrations.sample.json` và trang Cài đặt tích hợp của admin thêm mục **"Email (SMTP)"** chỉ hiển thị trạng thái Đã cấu hình/Chưa cấu hình như các tích hợp khác. Nicole đưa cấu hình sau — hệ thống phải chạy hoàn chỉnh ở chế độ LogOnly trước khi có cấu hình thật.
- Tuyệt đối không trả mã xác thực trong response API ở mọi môi trường.

**1.3 Đăng nhập:** một form duy nhất: ô định danh chấp nhận **email (khách) hoặc tên đăng nhập (nhân viên)** + mật khẩu → dùng chung cơ chế session/JWT hiện có. Khách chưa xác thực email → trả mã lỗi riêng `EMAIL_NOT_VERIFIED` để giao diện dẫn tới bước nhập mã. Modal đăng nhập đổi thành 2 tab **Đăng nhập / Đăng ký**; bỏ tab OTP.

**1.4 Quên mật khẩu (bằng email):** nhập email → gửi mã đặt lại (Purpose `RESET_PASSWORD`, hạn 30 phút) → đặt mật khẩu mới → thu hồi toàn bộ session cũ (dùng cơ chế thu hồi hiện có). Email không tồn tại vẫn trả thông điệp chung (chống dò tài khoản).

**1.5 Gỡ OTP:** xóa 2 endpoint `/auth/otp/*`, `OtpAuthService`, tab OTP ở frontend. Bảng `Security.OtpCodes` giữ nguyên trong DB (không drop — tránh rủi ro dữ liệu; ghi chú deprecated trong code).

**1.6 Thông điệp lỗi:** các lỗi đăng nhập/đăng ký trả câu tiếng Việt cụ thể theo mã lỗi (sai mật khẩu, chưa xác thực email, email đã tồn tại, SĐT sai định dạng, máy chủ gửi mail chưa cấu hình ở chế độ dev thì hướng dẫn xem log) — không còn câu chung chung "Không thể... Vui lòng thử lại" cho mọi trường hợp.

*Test bắt buộc 6A:* đăng ký SĐT sai định dạng bị chặn ở server; email trùng bị chặn; chưa xác thực thì đăng nhập bị từ chối đúng mã lỗi; mã xác thực sai 5 lần bị khóa, hết hạn bị từ chối; đặt lại mật khẩu xong session cũ bị thu hồi; đăng ký bằng SĐT đã có hồ sơ khách (từng đặt lịch) thì tài khoản nối đúng hồ sơ đó (thấy được phiếu cũ trong cổng khách).

## 2. Đợt 6B — Kỹ thuật viên được thêm vào là phải chọn được

- **Tự động tạo hồ sơ:** khi tạo user có role TECHNICIAN **hoặc** gán role TECHNICIAN cho user sẵn có → tạo `TechnicianProfiles` nếu chưa có (DisplayName = họ tên user, Specialty mặc định "Kỹ thuật viên", `IsPublic = 1`, `IsAcceptingRequests = 1`) — thêm vào là hiện lên danh sách chọn ngay, đúng ý Nicole.
- **Backfill:** migration 006 đã chèn hồ sơ cho mọi kỹ thuật viên hiện có đang thiếu hồ sơ (Nicole import cùng đợt).
- **Trang quản trị:** trong trang quản lý nhân viên, với user là kỹ thuật viên có khối "Hồ sơ cho khách chọn": sửa tên hiển thị/chuyên môn/ảnh/kinh nghiệm, bật-tắt Công khai, bật-tắt Đang nhận chọn. Kỹ thuật viên tự sửa được hồ sơ của mình; 2 công tắc do Admin/Manager giữ.
- **Sửa điểm sao mặc định (lỗi P2 đợt trước):** API public trả `RatingAverage = null` khi `RatingCount = 0`; giao diện hiện "Chưa có đánh giá" thay vì 5.0★.
- **Trạng thái rỗng đúng nghĩa:** khi thật sự chưa có kỹ thuật viên công khai nào, khối chọn ở form đặt lịch ẩn hẳn (chỉ còn ghi chú "TechCare sẽ sắp xếp kỹ thuật viên phù hợp") thay vì hiện khung trống gây hiểu nhầm.

*Test bắt buộc 6B:* tạo user TECHNICIAN mới → API public có ngay hồ sơ đó; tắt Công khai → biến mất khỏi API nhưng vẫn gán việc nội bộ được; kỹ thuật viên chưa có đánh giá → `RatingAverage` null.

## 3. Đợt 6C — Tạo đơn đầy đủ yếu tố

- Bổ sung vào đặt lịch (migration 006 đã thêm cột): **Hình thức gửi máy** — Tại quầy (`AT_STORE`, mặc định) / Gửi ship đến (`SHIP_IN`) / Nhận tận nơi (`PICKUP`); khi chọn 2 hình thức sau thì hiện ô **Địa chỉ** (bắt buộc, tối thiểu 10 ký tự) lưu vào `PickupAddress`.
- Khi lễ tân tiếp nhận lịch hẹn dạng `SHIP_IN/PICKUP` → ở màn tiếp nhận có nút **"Tạo vận đơn"** mở sẵn luồng tạo shipment hiện có (địa chỉ lấy từ lịch hẹn, không nhập lại).
- Chuỗi theo dõi giữ nguyên (đã kiểm chứng ở Mục 0.4); đợt này chỉ thêm: trang tra cứu của khách với đơn ship phải hiện đủ 3 khối — tiến độ sửa, vận đơn (timeline + bản đồ), kỹ thuật viên phụ trách — và có test tích hợp webhook→sự kiện→API tra cứu bằng dữ liệu giả lập GHN (không cần tài khoản staging cho test này).

*Cổng nghiệm thu toàn đợt:* build 2 phía sạch; toàn bộ test cũ + test mới xanh (chạy `dotnet test` và chụp kết quả — đây cũng là lúc đóng nợ R4); commit tách 3 phần `feat(customer-accounts)`, `feat(technician-visibility)`, `feat(booking-completeness)`; báo cáo theo mẫu cũ. Điều kiện dừng giữ nguyên: không tự chạy SQL trên máy Nicole, không nhận/ghi khóa thật, lệch thiết kế thì dừng và hỏi.
