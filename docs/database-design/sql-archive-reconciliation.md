# Đối chiếu gói SQL QOL

## Kết luận

`QOL-database-scripts.zip` là thiết kế database mở rộng gồm các script `002`–`008`.
Không chạy nguyên bộ script trên database QOL hiện tại: một số bảng đã được EF Core
tạo, còn các script khác phụ thuộc vào bảng/model/API chưa có. EF Core migrations
trong `backend/QOL.Infrastructure/Migrations/` tiếp tục là nguồn schema duy nhất
cho ứng dụng hiện tại.

Database local `QOL` đã áp dụng đủ bảy EF Core migrations hiện có:

- `InitialCreate`
- `AddAccountRolesAndPasswordHash`
- `AddAudioContent`
- `AddAudioVisibility`
- `AddSubscriptionsAndOrders`
- `AddLocationReviews`
- `AddUserSubscriptions`

Model EF Core hiện có mười entity/bảng nghiệp vụ: `Users`, `Locations`,
`Languages`, `AudioFiles`, `Favorites`, `ListeningHistory`, `LocationReviews`,
`SubscriptionPlans`, `SubscriptionOrders` và `UserSubscriptions`.

## Đối chiếu từng phần trong ZIP

| Script | Nội dung | Hiện trạng trong ứng dụng | Xử lý |
|---|---|---|---|
| `002-users-roles.sql` | User, role/permission, hồ sơ, phiên, reset mật khẩu, lần đăng nhập | `Users` có trong EF; ứng dụng dùng một trường `Role` (`User`/`Admin`) và cookie HTTP-only. Các bảng role-permission, session, reset token và login attempts chưa có luồng ứng dụng. | Không chạy. Chỉ bổ sung khi có yêu cầu RBAC chi tiết, quản lý phiên hoặc khôi phục mật khẩu. |
| `003-locations.sql` | Địa điểm, GPS, danh mục, ảnh | `Locations` có trong EF; chưa có model/API cho GPS, danh mục, ảnh địa điểm. | Không chạy. Thiết kế model và API trước khi thêm các bảng/cột này. |
| `004-content-translation-audio.sql` | Ngôn ngữ, bài viết, bản dịch, audio/TTS | `Languages` và `AudioFiles` có trong EF. Ứng dụng lưu tiêu đề, transcript và tệp audio thủ công; chưa có `Articles`, `Translations`, TTS hay `CategoryTranslations`. Script còn thay đổi `UserProfiles`, bảng chưa tồn tại. | Không chạy. Giữ luồng audio thủ công hiện tại; tích hợp nội dung dịch/TTS bằng model và migration khi có yêu cầu. |
| `005-activity-reviews-notifications.sql` | Yêu thích, lịch sử nghe, đánh giá, báo cáo, thông báo | `Favorites`, `ListeningHistory`, `LocationReviews` đã có trong EF và được API dùng. `ReviewReports`, `Notifications` chưa có model/API. | Không chạy. Các bảng hiện có đã được tạo bởi migrations; thông báo/báo cáo là phạm vi riêng. |
| `006-payment-subscription.sql` | Gói, đơn hàng, payment, đăng ký, hoàn tiền | `SubscriptionPlans`, `SubscriptionOrders` và `UserSubscriptions` đã có trong EF. Đơn hàng chỉ Pending đến khi Admin xác minh thủ công; không có tích hợp payment provider. `Payments` và `Refunds` chưa được ứng dụng sử dụng. | Không chạy. Không tạo bảng giao dịch/hoàn tiền khi chưa có nhà cung cấp thanh toán và nghiệp vụ tương ứng. |
| `007-system-management.sql` | Audit log, cài đặt hệ thống, error log, user activity | Chưa có model/API tương ứng; hiện không phải dependency của các chức năng đang chạy. | Chưa thêm schema. Xác định yêu cầu lưu trữ, quyền truy cập và vòng đời log trước. |
| `008-views-procedures.sql` | Views dashboard/báo cáo; thủ tục heartbeat, xác minh đơn và hết hạn thuê bao | API hiện tính dashboard bằng EF và có model/luồng tạo `UserSubscription`; chưa có model cho Payment và chưa gọi các views/procedures này. | Không chạy. Các thủ tục phụ thuộc vào bảng chưa tồn tại và có thể trùng nghiệp vụ trong API. |

## Quy tắc duy trì schema

- Thay đổi schema được tạo bằng EF Core migration trong
  `backend/QOL.Infrastructure/Migrations/`, sau đó áp dụng bằng `dotnet ef database update`.
- Không chạy song song một bản DDL khác tạo cùng các bảng nghiệp vụ.
- Trước khi thêm tính năng ở các script trên, cần có model, mapping, migration,
  API/UI sử dụng dữ liệu và kiểm thử tương ứng.
- Không xóa các EF migrations đã áp dụng hoặc bảng hiện có: chúng chứa lịch sử
  schema và dữ liệu mà ứng dụng đang sử dụng.

## Kiểm tra file trùng và dọn dẹp

- Gói ZIP nằm ngoài thư mục dự án, không được sao chép thành một bộ migrations
  SQL thứ hai trong repo.
- Không phát hiện file trùng nội dung trong các thư mục entity, API endpoint,
  EF configuration và frontend source đã kiểm tra.
- Giữ `database/Scripts/001-create-app-logins.sql`: script này tạo login SQL
  Server, không trùng chức năng với EF migrations.
