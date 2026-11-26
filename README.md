# QOL

FE DEV (react native + React.js) chuyên nghiệp

Monorepo cho QOL — ứng dụng hướng dẫn tham quan bằng âm thanh đa ngôn ngữ. Nền tảng hiện được tổ chức cho React + TypeScript, ASP.NET Core và Microsoft SQL Server.

## Chạy từ đầu (Development)

Cần Docker Desktop. Từ thư mục gốc, chạy tối đa ba lệnh PowerShell; trước khi chạy Compose, sửa ba mật khẩu SQL trong `.env`:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Mở `http://localhost:5173/explore`. Compose chạy frontend, API và SQL Server; môi trường Development tự thêm ba địa điểm, một WAV tự tạo và tài khoản demo `demo@qol.local` / `QolDemo!2026`. Mật khẩu demo chỉ dùng local.

## Cấu trúc

- `frontend/user-web/`: ứng dụng web người dùng và trang quản trị tại `/admin`; mã React nằm trong `src/`.
- `frontend/shared/`: API client, kiểu dữ liệu và tiện ích dùng chung.
- `backend/`: solution ASP.NET Core với các project API, Application, Domain, Infrastructure và Tests; API hiện dùng Minimal API, chưa dùng Controllers.
- `backend/QOL.Infrastructure/Migrations/`: vị trí chính thức duy nhất cho EF Core migrations.
- `database/`: scripts, seeds, stored procedures, views và backups; không lưu EF migrations.
- Đối chiếu gói SQL mở rộng với schema và chức năng hiện tại tại [sql-archive-reconciliation.md](./docs/database-design/sql-archive-reconciliation.md); không chạy các script trùng bảng với EF migrations.
- `deployment/`: cấu hình triển khai bổ sung.
- `storage/`: tài nguyên hình ảnh, âm thanh và tài liệu.
- `docs/`: yêu cầu, sơ đồ, thiết kế và kiểm thử.
- Xem [kiến trúc và quy ước phát triển](./docs/architecture.md) trước khi thêm service, endpoint hoặc migration mới.

### Luồng chạy và phân lớp

- `QOL.API` là điểm vào HTTP: xác thực, CORS, kiểm tra đầu vào, health check và các nhóm Minimal API trong `backend/QOL.API/Endpoints/`.
- `QOL.Application` chứa các abstraction/use case; `QOL.Domain` chứa entity và quy tắc nghiệp vụ; `QOL.Infrastructure` chứa EF Core `QolDbContext`, cấu hình entity, seed và migrations.
- `QOL.Tests` kiểm thử quy tắc domain. Frontend `user-web` chứa cả giao diện người dùng và `/admin`; `frontend/shared` dùng chung API client và các kiểu dữ liệu.
- Luồng dữ liệu điển hình: trang React → `frontend/shared` API client → endpoint ASP.NET Core → `QolDbContext`/dịch vụ Infrastructure → SQL Server.
- API hiện dùng dịch vụ Application cho tài khoản và địa điểm; một số nhóm endpoint audio, hoạt động, đơn hàng và đánh giá truy vấn `QolDbContext` trực tiếp. Khi sửa các luồng này, giữ truy vấn/ghi dữ liệu trong transaction và kiểm tra quyền sở hữu/role ở backend; có thể chuyển dần nghiệp vụ phức tạp vào Application services nếu cần.

### Cấu trúc dữ liệu và quy tắc thay đổi

- `QolDbContext` và các cấu hình entity trong `backend/QOL.Infrastructure/Persistence/Configurations/` mô tả schema mà API hiện sử dụng.
- Model hiện có 15 bảng: `Users`, `Locations`, `LocationImages`, `Languages`, `AudioFiles`, `Favorites`, `ListeningHistory`, `LocationReviews`, `SubscriptionPlans`, `SubscriptionOrders`, `UserSubscriptions`, `VisitorSessions`, `Payments`, `LocationTranslations` và `ActionLogs`. Cả 12 EF migrations được quản lý tại `backend/QOL.Infrastructure/Migrations/`.
- Khi thêm trường/bảng: cập nhật Domain entity → EF configuration/`QolDbContext` nếu cần → tạo migration → cập nhật API và frontend → kiểm thử → áp dụng migration bằng `dotnet ef database update`. Không sửa/xóa migration đã áp dụng và không chạy song song bộ SQL tạo lại các bảng này.
- [sql-archive-reconciliation.md](./docs/database-design/sql-archive-reconciliation.md) đối chiếu 7 script SQL mở rộng với model hiện tại. Archive là bản thiết kế, không phải migration để chạy; các tính năng được triển khai dùng EF migrations, phần còn thiếu được ghi rõ trong tài liệu.

### Kiểm thử hiện tại

- Backend: `dotnet test backend\QOL.Tests\QOL.Tests.csproj -c Release`; domain và WebApplicationFactory API tests chạy với EF InMemory. Hai kiểm thử SQL Server thật được tự bỏ qua nếu chưa đặt `QOL_TEST_SQLSERVER`; CI chạy chúng với SQL Server service container.
- Frontend: `npm.cmd --prefix frontend run test --workspace @qol/user-web` chạy unit tests Vitest; `npm.cmd --prefix frontend run build` kiểm tra production build.
- Vì vậy build xanh xác nhận biên dịch, chưa thay thế cho kiểm thử luồng đăng nhập, upload, đơn hàng hay quyền Admin trên runtime.

Schema `Locations` lưu tọa độ tùy chọn, bán kính kích hoạt, độ ưu tiên, mô tả gốc, link chỉ đường và trạng thái `ACTIVE`/`INACTIVE`/`PENDING`/`REJECTED`; ảnh địa điểm được lưu trong bảng `LocationImages`. Migration `AddLocationMetadata` chuyển `IsActive` cũ sang `Status` trước khi bỏ cột, đồng thời `Down` khôi phục `IsActive`. Migration `AddListeningTriggerMethod` thêm nguồn bắt đầu nghe (`TAP`/`AUTO_GPS`) và gán `TAP` cho lịch sử cũ. Tọa độ cũ để trống vẫn hợp lệ. Languages có seed `vi`, `en`, `ko`, `zh`, `ja`; frontend đọc catalog từ API. API trả metadata, ảnh và link chỉ đường của địa điểm đang hoạt động.

Đăng ký, đăng nhập bằng cookie HTTP-only, hồ sơ cơ bản, phân quyền User/Admin, CRUD địa điểm, nội dung/audio thủ công, yêu thích, lịch sử nghe, đánh giá địa điểm, gói dịch vụ và đơn hàng đã được triển khai. Cookie tài khoản `QOL.Session` có `Path=/`; antiforgery vẫn áp dụng cho mọi thao tác ghi dưới `/api`.

Trang chủ User Web có phần giới thiệu, tìm kiếm địa điểm, ngôn ngữ và lối vào trang gói dịch vụ. Danh sách địa điểm trên trang chủ và trang Khám phá được tải từ API; thẻ dùng ảnh đã tải lên hoặc hình minh họa SVG cục bộ, không tải ảnh Unsplash. Admin quản lý tọa độ, bán kính, ưu tiên, mô tả, trạng thái, link chỉ đường và ảnh PNG/JPG/WEBP (tối đa 5 MB) tại `/admin`; ảnh được lưu cục bộ trong `backend\QOL.API\App_Data\location-images` với tên GUID. Admin nhập tiêu đề, bản dịch và tệp audio (MP3/WAV/OGG/M4A tối đa 20 MB) tại `/admin`; tệp được lưu cục bộ trong `backend\QOL.API\App_Data\audio`. WAV được đo thời lượng từ RIFF header; các định dạng MP3/OGG/M4A dùng thời lượng khai báo bị giới hạn tối đa một giờ. URL `/media/audio/{fileName}` được kiểm tra trạng thái audio, địa điểm và subscription còn hạn trước khi phát; Admin vẫn có thể nghe thử nội dung đã ẩn. Người dùng phải đăng nhập và có subscription còn hạn để lấy danh sách audio; thiếu gói sẽ nhận HTTP 402 ProblemDetails `subscription-required`. Gói/đơn hàng được quản lý tại `/plans` và `/orders`. Phương thức QR tĩnh dùng ảnh `frontend/user-web/public/payment-qr.jpg`; người dùng phải nhập đúng số tiền/mã đơn, sau đó báo đã thanh toán. QOL không tự xác nhận việc quét hay chuyển khoản: đơn vẫn Pending cho tới khi Admin đối soát và xác minh thủ công. Tùy chọn VNPay tạo liên kết theo số tiền trên đơn; IPN cần chữ ký HMAC-SHA512 hợp lệ và fulfillment idempotent trước khi kích hoạt gói.

Khách ẩn danh có thể mở `/access`, chọn ngôn ngữ và xem địa điểm; phiên khách không cấp quyền phát audio hoặc thao tác thay tài khoản. Chính sách được trả từ `GET /api/policy` và hiển thị tại `/privacy`; Admin tạo QR vào hệ thống tại `/admin/access-qr`. Bản dịch quản trị được lưu riêng; TTS dùng Azure Speech nếu cấu hình, còn Fake provider chỉ dùng trong test. Audio TTS và upload dùng chung `IFileStorage`, quyền `/media/audio` và quy tắc xóa/ẩn theo lịch sử nghe. Email xác minh dùng SMTP nếu được cấu hình; lỗi email không hủy việc kích hoạt gói.

Các API bổ sung chính:

- Visitor: `POST /api/access/start`, `PUT /api/session/language`, `GET /api/policy`.
- Thanh toán/gói: `POST /api/me/orders/{id}/pay`, `POST /api/me/orders/{id}/qr-payment-claim`, `GET /api/payments/vnpay/ipn`, `GET /api/me/subscriptions`.
- Bản dịch/TTS: `GET`/`PUT /api/admin/locations/{locationId}/translations/...`, `POST /api/admin/locations/{locationId}/translations/{translationId}/audio/tts`.
- Quản trị audio: `DELETE /api/admin/locations/{locationId}/audio/{audioId}`, thay tệp qua `PUT .../audio/{audioId}/file`, và dọn mồ côi qua `POST /api/admin/audio/cleanup?dryRun=true|false`.

Trang `/map` dùng Leaflet và OpenStreetMap để hiển thị POI có tọa độ. Khi cấp quyền vị trí, GPS được theo dõi bằng `watchPosition`; POI gần nhất trong bán kính được tự phát một lần, với `Priority` dùng để phá hòa. Chạm marker vẫn phát thủ công khi GPS không dùng được. Nút “Bắt đầu tham quan” mở khóa phát audio; nếu autoplay bị trình duyệt chặn (đặc biệt Safari iOS), trình phát thủ công vẫn hiển thị. Thiếu subscription trả lời bằng lời nhắc tới `/plans` hoặc `/login`, không tự gửi lại liên tục.

Trong `/admin`, có thể thay tệp audio; tệp cũ chỉ bị xóa sau khi DB lưu tệp mới thành công. Xóa audio sẽ xóa cả bản ghi và tệp nếu chưa có lịch sử nghe; nếu có lịch sử, audio chỉ bị ẩn để giữ nguyên tham chiếu. Công cụ "Preview orphan cleanup" liệt kê các tệp không còn bản ghi; chỉ nút xóa mới dọn chúng. API tương ứng là `POST /api/admin/audio/cleanup?dryRun=true` để chạy thử và `dryRun=false` để xóa.

## Chạy frontend

Cần Node.js 20.19+ hoặc 22.12+.

```powershell
Set-Location frontend
npm.cmd install
npm.cmd run dev --workspace @qol/user-web
```

Giao diện mặc định gọi profile Visual Studio tại `http://localhost:49820/api/`; profile API không tự mở trình duyệt vào endpoint JSON nữa. Mở giao diện tại `http://localhost:5173`. CORS cho phép origin Vite `http://localhost:5173`. Nếu API chạy địa chỉ khác, đặt `VITE_API_BASE_URL` thành URL `/api/` tương ứng trước khi chạy Vite. Với Docker Compose, API được publish ở cổng `8080`, vì vậy chạy `$env:VITE_API_BASE_URL = "http://localhost:8080/api/"` trước khi khởi động frontend.

Trên Windows PowerShell, dùng `npm.cmd` thay vì `npm` nếu máy chặn script `npm.ps1` theo Execution Policy; không cần hạ chính sách chạy script.

## Chạy backend

Cần .NET 10 SDK và SQL Server. Không lưu thông tin xác thực thật trong `appsettings`.

```powershell
dotnet restore QOL.sln
dotnet tool restore
$env:ConnectionStrings__DefaultConnection = "<connection-string dùng tài khoản migration>"
dotnet ef database update --project backend\QOL.Infrastructure\QOL.Infrastructure.csproj --startup-project backend\QOL.API\QOL.API.csproj
$env:ConnectionStrings__DefaultConnection = "<connection-string tài khoản ứng dụng>"
dotnet run --project backend\QOL.API\QOL.API.csproj
dotnet test QOL.sln
```

API khởi chạy qua profile `QOL.API` trên `http://localhost:49820` và dùng connection string local đã cấu hình trong `backend\QOL.API\Properties\launchSettings.json`. Profile không tự mở trình duyệt; giao diện riêng chạy trên `http://localhost:5173`. Tất cả migration nằm trong Infrastructure. Tạo migration mới (không tạo lại `InitialCreate`) bằng:

```powershell
dotnet ef migrations add <MigrationName> --project backend\QOL.Infrastructure\QOL.Infrastructure.csproj --startup-project backend\QOL.API\QOL.API.csproj --output-dir Migrations
```

API không tự áp dụng migration khi khởi động. Tài khoản runtime chỉ nên có quyền đọc/ghi dữ liệu; tài khoản migration có quyền DDL được dùng riêng.

Với SQL Server bên ngoài Compose, quản trị viên SQL chạy script tạo database/login bằng mật khẩu mạnh rồi áp migration bằng login `qol_migrator`:

```powershell
sqlcmd -S SERVER_NAME -d master -U ADMIN_LOGIN -P ADMIN_PASSWORD -C -b -v APP_PASSWORD="APP_PASSWORD" MIGRATOR_PASSWORD="MIGRATOR_PASSWORD" -i database\Scripts\001-create-app-logins.sql
$env:ConnectionStrings__DefaultConnection = "Server=<server>;Database=QOL;User Id=qol_migrator;Password=<migration-password>;Encrypt=True;TrustServerCertificate=False"
dotnet ef database update --project backend\QOL.Infrastructure\QOL.Infrastructure.csproj --startup-project backend\QOL.API\QOL.API.csproj
$env:ConnectionStrings__DefaultConnection = "Server=<server>;Database=QOL;User Id=qol_app;Password=<app-password>;Encrypt=True;TrustServerCertificate=False"
dotnet run --project backend\QOL.API\QOL.API.csproj
```

Thay các giá trị viết hoa trong lệnh `sqlcmd` bằng thông tin của bạn trước khi chạy; không lưu mật khẩu thật vào repo hoặc history được chia sẻ.

Chỉ dùng `TrustServerCertificate=True` cho SQL Server phát triển local nếu chưa cài chứng thư phù hợp.

## Tạo Admin đầu tiên và kiểm tra tài khoản

Admin đầu tiên được tạo qua bootstrap một lần. Tạo token ngẫu nhiên và lưu bằng .NET User Secrets (không đặt token vào `appsettings.json`, source control hoặc file `.http`):

```powershell
dotnet user-secrets init --project backend\QOL.API\QOL.API.csproj
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$bootstrapToken = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Authentication:AdminBootstrapToken" $bootstrapToken --project backend\QOL.API\QOL.API.csproj
Write-Output $bootstrapToken
```

Khởi động lại API, mở trang `/bootstrap-admin` trên web, rồi nhập token vừa tạo cùng email, tên hiển thị và mật khẩu mạnh (ít nhất 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt). Sau khi tạo Admin đầu tiên, xóa token:

```powershell
dotnet user-secrets remove "Authentication:AdminBootstrapToken" --project backend\QOL.API\QOL.API.csproj
```

Admin có thể đăng nhập tại `/login` và quản lý địa điểm, audio, gói dịch vụ, đơn hàng, điều phối đánh giá và dashboard tại `/admin`. Người dùng có thể đánh giá địa điểm tại trang chi tiết. Tạo tài khoản thường tại `/register`; trang hồ sơ ở `/profile`. Các endpoint API và ví dụ payload nằm trong [QOL.API.http](./backend/QOL.API/QOL.API.http). File HTTP dùng cookie của phiên đăng nhập hiện tại; cần gọi Login hoặc Bootstrap trước các yêu cầu được bảo vệ. Không dùng chung mật khẩu mẫu trong file này trên môi trường thật.

## Chạy bằng Docker Compose

```powershell
Copy-Item .env.example .env
# Thay cả ba mật khẩu mẫu trong .env trước khi khởi chạy.
docker compose up --build
```

Compose khởi động SQL Server, tạo database và tài khoản riêng, chạy migration bằng tài khoản `qol_migrator`, sau đó chạy API với tài khoản ít quyền `qol_app` cùng frontend Nginx. Frontend ở `http://localhost:5173`, API ở `http://localhost:8080`. Mật khẩu ứng dụng/migration chỉ nhận chữ, số, `_`, `@`, `#`, `%`, `!`, `-`; dài ít nhất 12 ký tự và gồm chữ hoa, chữ thường, số, ký tự đặc biệt. Nếu đổi thông tin tài khoản sau lần khởi chạy đầu, cần cập nhật login trong SQL Server tương ứng. `ASPNETCORE_ENVIRONMENT` lấy từ `.env`; giá trị mẫu là `Development`, còn khi triển khai cần đặt `Production`, `SQL_TRUST_SERVER_CERTIFICATE=false` và cung cấp chứng thư phù hợp.

API health check `http://localhost:8080/health` kiểm tra kết nối database; OpenAPI ở `/openapi/v1.json` trong Development. SQL Server lắng nghe tại cổng `1433`. Cấu hình gateway VNPay, Azure Speech và SMTP nằm trong `.env` (không commit secrets); Compose vẫn cần HTTPS, secrets manager, CORS origins cụ thể và cấu hình triển khai an toàn trước Production.

## Trạng thái và phần còn thiếu trước khi dùng thật

- **Đã có:** xác thực/phân quyền, phiên khách giới hạn, GPS/map, audio upload/TTS, quản trị bản dịch, quyền nghe theo gói, QR tĩnh có người dùng báo đã thanh toán và Admin xác minh thủ công, tích hợp VNPay tùy cấu hình, email SMTP tùy cấu hình, audit log audio và CI.
- **Chưa có:** hoàn tiền, danh mục địa điểm, dịch tự động, thiết lập quản trị tổng quát và triển khai Production hoàn chỉnh.
- **Trước Production:** bật HTTPS, cấu hình CORS theo domain thật, quản lý Data Protection keys/secrets, cung cấp chứng thư SQL hợp lệ, chạy migration bằng tài khoản chuyên dụng và hoàn tất rà soát bảo mật/UX.
