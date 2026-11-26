# Đối chiếu gói SQL QOL

## Kết luận

`QOL-database-scripts.zip` là thiết kế database mở rộng gồm các script `002`–`008`.
Không chạy nguyên bộ script trên database QOL hiện tại: một số bảng đã được EF Core
tạo, còn các script khác phụ thuộc vào bảng/model/API chưa có. EF Core migrations
trong `backend/QOL.Infrastructure/Migrations/` tiếp tục là nguồn schema duy nhất
cho ứng dụng hiện tại.

Database local `QOL` từng được kiểm tra ở mốc có bảy EF Core migrations. Repo hiện có 12 migrations; môi trường chưa áp dụng các migration sau cần cập nhật database trước khi chạy phiên bản hiện tại:

- `InitialCreate`
- `AddAccountRolesAndPasswordHash`
- `AddAudioContent`
- `AddAudioVisibility`
- `AddSubscriptionsAndOrders`
- `AddLocationReviews`
- `AddUserSubscriptions`
- `AddLocationMetadata`
- `AddListeningTriggerMethod`
- `AddVisitorSessions`
- `AddPayments`
- `AddAudioTranslationsAndActionLogs`

Model EF Core hiện có 15 bảng: `Users`, `Locations`, `LocationImages`, `Languages`, `AudioFiles`,
`Favorites`, `ListeningHistory`, `LocationReviews`, `SubscriptionPlans`, `SubscriptionOrders`,
`UserSubscriptions`, `VisitorSessions`, `Payments`, `LocationTranslations` và `ActionLogs`.

## Đối chiếu từng phần trong ZIP

| Script | Nội dung | Hiện trạng trong ứng dụng | Xử lý |
|---|---|---|---|
| `002-users-roles.sql` | User, role/permission, hồ sơ, phiên, reset mật khẩu, lần đăng nhập | `Users` có trong EF; ứng dụng dùng một trường `Role` (`User`/`Admin`) và cookie HTTP-only. Các bảng role-permission, session, reset token và login attempts chưa có luồng ứng dụng. | Không chạy. Chỉ bổ sung khi có yêu cầu RBAC chi tiết, quản lý phiên hoặc khôi phục mật khẩu. |
| `003-locations.sql` | Địa điểm, GPS, danh mục, ảnh | `Locations` có tọa độ tùy chọn, bán kính, ưu tiên, mô tả, link chỉ đường và trạng thái; `LocationImages`/upload ảnh đã có model/API. Danh mục vẫn chưa được triển khai. | Không chạy. Dùng migration EF `AddLocationMetadata`; không chạy lại DDL của archive. Danh mục là phạm vi riêng. |
| `004-content-translation-audio.sql` | Ngôn ngữ, bài viết, bản dịch, audio/TTS | `Languages`, `AudioFiles` và `LocationTranslations` có trong EF. TTS tùy cấu hình Azure Speech; bài viết và `CategoryTranslations` chưa được triển khai. Script còn thay đổi `UserProfiles`, bảng chưa tồn tại. | Không chạy. Dùng migrations EF hiện hành cho bản dịch/TTS; không chạy lại DDL của archive. |
| `005-activity-reviews-notifications.sql` | Yêu thích, lịch sử nghe, đánh giá, báo cáo, thông báo | `Favorites`, `ListeningHistory`, `LocationReviews` đã có trong EF và được API dùng. `ReviewReports`, `Notifications` chưa có model/API. | Không chạy. Các bảng hiện có đã được tạo bởi migrations; thông báo/báo cáo là phạm vi riêng. |
| `006-payment-subscription.sql` | Gói, đơn hàng, payment, đăng ký, hoàn tiền | `SubscriptionPlans`, `SubscriptionOrders`, `UserSubscriptions` và `Payments` đã có. VNPay được bật tùy cấu hình; duyệt tay vẫn là phương án dự phòng. Hoàn tiền chưa có. | Không chạy. Payment và fulfillment được quản lý bằng EF/API; không chạy lại DDL của archive. |
| `007-system-management.sql` | Audit log, cài đặt hệ thống, error log, user activity | `ActionLogs` ghi các thao tác quản trị audio/TTS; cài đặt hệ thống, error log và báo cáo user activity tổng quát chưa có. | Không chạy. Chỉ bổ sung phần còn thiếu khi có yêu cầu lưu trữ và quyền truy cập cụ thể. |
| `008-views-procedures.sql` | Views dashboard/báo cáo; thủ tục heartbeat, xác minh đơn và hết hạn thuê bao | API tính dashboard bằng EF; `IOrderFulfillmentService` xử lý cả duyệt tay và IPN VNPay, đồng thời subscription được kiểm tra/hết hạn ở service. Không dùng các view/procedure trong archive. | Không chạy. Tránh trùng nghiệp vụ và không chạy DDL bên ngoài migrations EF. |

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
