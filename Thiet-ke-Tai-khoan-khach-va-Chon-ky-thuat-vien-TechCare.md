# TechCare — Đợt 5: Tài khoản khách hàng (lỗ hổng "khách không vào được") + Khách chọn kỹ thuật viên

> Nguồn: ý tưởng của Nicole (03/11/2026) — khách được chọn kỹ thuật viên của mình, trang xem đơn có bản đồ và biết kỹ thuật viên nào đang hỗ trợ máy; kèm yêu cầu kiểm tra toàn vẹn và tìm thiếu sót. Kavi đã đối chiếu code ở commit `fe12d06` trước khi viết.
> File SQL đi kèm: `TechCare_Migration_005_CustomerAccounts_Technicians.sql`. Phân công và quy tắc chung giữ nguyên như file lệnh gốc (agent code, Nicole import SQL + nghiệm thu, Kavi rà soát).

---

## 1. Lỗ hổng Nicole phát hiện — XÁC NHẬN, và nó nghiêm trọng thật

**Khách hàng hiện không có bất kỳ cách nào đăng nhập được.** Chuỗi nguyên nhân, đã kiểm chứng từng mắt xích:

1. Không có endpoint đăng ký tài khoản cho khách (toàn repo chỉ có "RegisterDevice" — đăng ký *thiết bị*); tài khoản `Security.Users` chỉ được tạo bởi admin qua quyền `USER.MANAGE`.
2. Bảng `CRM.Customers` **không có cột liên kết tới tài khoản đăng nhập** (không có `UserId`) — kể cả khi admin tạo tay một tài khoản cho khách, tài khoản đó cũng không nối được tới hồ sơ khách, thiết bị và phiếu sửa của họ.
3. Hệ quả: khu vực `/tai-khoan` (cổng khách hàng đã dựng khung ở Đợt 3) là **cổng không ai bước vào được**; khách hiện chỉ xem đơn qua tra cứu mã + số điện thoại dạng khách vãng lai.

Điểm cộng đã kiểm chứng: đặt lịch **có** gộp khách theo số điện thoại (find-or-create theo Phone) nên danh tính CRM của khách ổn định — thiếu duy nhất lớp đăng nhập bám vào đó. Ngoài ra tiện kiểm tra luôn: **chưa có luồng quên mật khẩu** ở bất kỳ đâu, và **chưa có cơ chế gửi thông báo thật** (SMS/email/Zalo) — bảng `Comms.Notifications` mới là nơi lưu, khách đổi trạng thái phiếu không nhận được tin gì, phải tự vào tra cứu (ghi ở Mục 4).

## 2. Thiết kế sửa lỗ hổng: đăng nhập khách hàng bằng OTP qua số điện thoại

Chọn OTP thay vì mật khẩu cho khách, vì số điện thoại vốn đã là danh tính của khách trong hệ thống (đặt lịch, tra cứu đều bằng SĐT) — khách sửa máy đa số không nhớ thêm một mật khẩu nữa.

**Luồng:**
1. Trang `/dang-nhap` có 2 tab: *Nhân viên* (tên đăng nhập + mật khẩu như hiện tại) và *Khách hàng* (số điện thoại).
2. Khách nhập SĐT → `POST /api/v1/auth/otp/request`: luôn trả thông điệp chung chung ("Nếu số điện thoại hợp lệ, mã đã được gửi") dù SĐT có tồn tại hay không (chống dò số). Mã 6 số, lưu **băm SHA-256** vào `Security.OtpCodes`, hạn 5 phút, tối đa 5 lần nhập sai, chờ 60 giây mới gửi lại được, giới hạn số lần/ngày theo SĐT và IP (rate limit riêng, chặt hơn các endpoint khác).
3. `POST /api/v1/auth/otp/verify`: đúng mã → nếu SĐT đã có khách trong CRM: tìm/tạo tài khoản `Users` (role CUSTOMER, `UserName` = SĐT, không mật khẩu) và **nối `Customers.UserId`**; cấp JWT + session bằng đúng cơ chế AuthService hiện có (không viết hệ token thứ hai). Sai/quá hạn → lỗi chung, tăng bộ đếm.
4. Gửi mã qua interface `ISmsProvider`: bản dev dùng `LogOnlySmsProvider` (ghi log server, **chỉ bật khi môi trường Development**) để test không tốn tiền; production cắm nhà cung cấp SMS thật (eSMS/Viettel SMS/...) bằng cấu hình, khóa theo quy tắc env/user-secrets như cũ. **Tuyệt đối không trả mã OTP trong response API ở bất kỳ môi trường nào.**
5. Cổng `/tai-khoan` thành cổng thật: *Máy của tôi* (thiết bị theo CustomerId), *Phiếu của tôi* (kèm khối kỹ thuật viên + bản đồ vận đơn như Mục 3), *Lịch hẹn của tôi*, *Hóa đơn & bảo hành*. Quyền sở hữu kiểm tra phía server theo `Customers.UserId` của token — khách A không xem được dữ liệu khách B kể cả khi đoán được mã phiếu (tra cứu công khai bằng mã + SĐT vẫn giữ cho khách vãng lai, tài khoản là nâng cấp chứ không bắt buộc).
6. Nhân viên quên mật khẩu: đợt này xử lý bằng quy trình — admin đặt lại mật khẩu qua `USER.MANAGE` (đã có quyền, cần bổ sung nút "Đặt lại mật khẩu" ở trang quản lý user nếu chưa có); luồng tự phục vụ để đợt sau.

## 3. Tính năng chọn kỹ thuật viên + hiển thị cho khách

**Dữ kiện đã có:** phiếu sửa đã có `AssignedTechnicianUserId` (người được phân công) — phần thiếu là *hồ sơ công khai của kỹ thuật viên*, *nguyện vọng của khách* và *hiển thị cho khách*.

**Dữ liệu mới (migration 005):**
- `Security.TechnicianProfiles`: 1-1 với Users có role TECHNICIAN — tên hiển thị, ảnh đại diện (URL), chuyên môn (vd "MacBook & mainboard", "Gaming & tản nhiệt"), kinh nghiệm, cờ `IsPublic` (được hiện cho khách chọn), `IsAcceptingRequests` (tạm ngưng nhận chọn), thứ tự hiển thị. Admin/Manager quản lý ở trang user; kỹ thuật viên tự sửa hồ sơ của mình (trừ 2 cờ duyệt).
- `CRM.Appointments.PreferredTechnicianUserId` và `Repair.RepairTickets.PreferredTechnicianUserId`: nguyện vọng của khách, chảy từ lịch hẹn sang phiếu khi tiếp nhận.
- `Repair.TicketRatings`: mỗi phiếu hoàn thành được khách đánh giá **một lần** (1–5 sao + nhận xét), chỉ chủ phiếu mới đánh được, chỉ khi phiếu ở trạng thái COMPLETED/RETURNED. Điểm trung bình + số ca hoàn thành tính từ dữ liệu thật này và từ phiếu đã phân công — không có số liệu trang trí.

**Luồng khách:**
- Đặt lịch thêm khối tùy chọn **"Chọn kỹ thuật viên"**: thẻ từng kỹ thuật viên (ảnh, tên, chuyên môn, sao trung bình + số ca, huy hiệu "Đang nhận lịch/Tạm ngưng") + lựa chọn mặc định "Để TechCare sắp xếp giúp" luôn đứng đầu. Không chọn cũng đặt lịch bình thường.
- Nguyên tắc nghiệp vụ ghi rõ cho khách ngay trên giao diện: *đây là nguyện vọng, không phải cam kết tuyệt đối* — nếu kỹ thuật viên được chọn bận/nghỉ, lễ tân sẽ liên hệ xác nhận hoặc sắp xếp người thay thế có chuyên môn tương đương. Phía lễ tân: màn phân công làm nổi bật nguyện vọng; nếu gán khác nguyện vọng phải chọn lý do (hệ thống ghi vào lịch sử phiếu).
- Trang xem đơn của khách (cả tra cứu công khai lẫn `/tai-khoan`) thêm thẻ **"Kỹ thuật viên phụ trách máy của bạn"**: ảnh + tên + chuyên môn, dòng trạng thái theo bước sửa hiện tại ("Đang chẩn đoán", "Đang chờ linh kiện"...), nút **"Nhắn hỗ trợ"** mở widget chat (qua bộ phận hỗ trợ, **không lộ số điện thoại cá nhân** của kỹ thuật viên). Phiếu xong → hiện lời mời đánh giá sao ngay trong thẻ này.
- **Về bản đồ:** giữ bản đồ vận đơn như hiện tại. Với đơn *nhận máy tận nơi*, thêm vào cùng trang theo dõi một dải trạng thái của kỹ thuật viên được phân công đi lấy máy (Đã phân công → Đang đến lấy → Đã nhận máy) lấy từ sự kiện phiếu. **Không làm định vị GPS trực tiếp của kỹ thuật viên trong đợt này** — việc đó cần app riêng cho kỹ thuật viên, cơ chế đồng ý chia sẻ vị trí và chỉ bật trong chuyến đi lấy máy; ghi vào lộ trình dài hạn ở Mục 4, không hứa với khách trước.

## 4. Các thiếu sót khác Kavi tìm thấy khi kiểm tra lại (ngoài 2 ý trên)

| # | Thiếu sót | Mức | Hướng xử lý |
|---|---|---|---|
| G1 | Chưa có gửi thông báo thật (SMS/email/Zalo) khi phiếu đổi trạng thái, có báo giá chờ duyệt, máy sửa xong | 🟠 Khách phải tự tra cứu — trải nghiệm thiếu một nửa | Đợt 6: dùng lại `ISmsProvider` của Mục 2 làm kênh SMS nền; sự kiện nghiệp vụ ghi `Comms.Notifications` (đã có bảng) + job gửi nền có retry; email qua SMTP cấu hình |
| G2 | Chưa có quên mật khẩu tự phục vụ cho nhân viên | 🟡 | Tạm xử lý qua admin đặt lại (Mục 2.6); tự phục vụ bằng OTP/email để cùng đợt G1 |
| G3 | User `admin` seed trong script SQL có mật khẩu mặc định đã băm sẵn | 🟠 Vận hành | Không tra ngược được từ phía Kavi; Nicole phải đăng nhập và **đổi mật khẩu admin ngay** khi dùng DB thật, và không dùng DB đã seed mật khẩu này cho production mà chưa đổi |
| G4 | Định vị trực tiếp kỹ thuật viên khi đi lấy máy (GPS) | 🟡 Dài hạn | Cần app di động cho kỹ thuật viên + đồng ý vị trí + chỉ chia sẻ trong chuyến; thiết kế riêng khi có nhu cầu thật |

**Toàn vẹn tổng thể (kết luận kiểm tra lại):** ngoài lỗ hổng tài khoản khách (Mục 1) và các thiếu sót chức năng ở bảng trên, khung hệ thống giữ nguyên đánh giá các lượt trước: build frontend sạch, EF ↔ SQL khớp, không rò rỉ khóa, test đã có nhưng **chưa được chạy xác nhận trên máy Nicole** (đang nằm ở Pha 2 của kế hoạch test) — đó vẫn là việc cần làm trước khi kết luận "ổn định" ở mức vận hành.

## 5. Việc cho agent (Đợt 5, chia 2 nửa độc lập)

- **Đợt 5A — Tài khoản khách:** migration 005 phần identity (bảng OtpCodes, cột Customers.UserId) → `ISmsProvider` + LogOnly → 2 endpoint OTP (rate limit riêng) → liên kết tài khoản khi verify → trang đăng nhập 2 tab → `/tai-khoan` nối dữ liệu thật theo CustomerId + kiểm tra sở hữu phía server. *Test bắt buộc:* OTP hết hạn/sai quá 5 lần bị khóa; verify 2 lần với cùng SĐT chỉ tạo 1 user + 1 liên kết; khách A dùng token của mình gọi dữ liệu khách B → 403/404.
- **Đợt 5B — Kỹ thuật viên:** migration 005 phần còn lại (TechnicianProfiles, PreferredTechnicianUserId ×2, TicketRatings) → API public danh sách kỹ thuật viên (chỉ trường an toàn) → khối chọn kỹ thuật viên ở đặt lịch → thẻ kỹ thuật viên ở trang xem đơn + đánh giá sau hoàn thành → màn phân công của lễ tân hiển thị nguyện vọng + lý do khi gán khác. *Test bắt buộc:* kỹ thuật viên `IsPublic=0` không xuất hiện ở API public; đánh giá lần 2 cho cùng phiếu bị từ chối; khách đánh giá phiếu của người khác bị từ chối.
- Thứ tự: 5A trước (mở được cổng khách), 5B sau. Mỗi nửa một commit `feat(customer-auth): ...` / `feat(technician): ...`, cổng: build 2 phía + test mới xanh, báo cáo theo mẫu cũ.

*Sau khi Nicole import migration 005, file `TechCare_Database_FULL.sql` đã được Kavi gấp sẵn phần 005 tương ứng để bản cài mới vẫn là một file duy nhất.*
