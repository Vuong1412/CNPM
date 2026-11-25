# QOL

Monorepo cho QOL — ứng dụng hướng dẫn tham quan bằng âm thanh đa ngôn ngữ. Nền tảng hiện được tổ chức cho React + TypeScript, ASP.NET Core và Microsoft SQL Server.

## Chạy từ đầu (Development)

Cần Docker Desktop và Node.js 20.19+ hoặc 22.12+. Từ thư mục gốc, mở hai cửa sổ PowerShell. Sao chép `.env.example` thành `.env` rồi thay các mật khẩu mẫu trước khi chạy.

**Cửa sổ 1 — SQL Server và API:**

```powershell
Copy-Item .env.example .env
docker compose up --build
```

**Cửa sổ 2 — web:**

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev --workspace @qol/user-web
```

Mở `http://localhost:5173/explore`. API trong Compose chạy ở `http://localhost:8080`. Khi API chạy ở môi trường `Development`, hệ thống tự tạo ba địa điểm, một file WAV mẫu tự sinh và tài khoản người dùng demo. Đăng nhập bằng `demo@qol.local` / `QolDemo!2026`. Tài khoản và mật khẩu mẫu chỉ dành cho dữ liệu phát triển; không dùng ở môi trường thật.

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
- Database hiện có 10 entity/bảng nghiệp vụ: `Users`, `Locations`, `Languages`, `AudioFiles`, `Favorites`, `ListeningHistory`, `LocationReviews`, `SubscriptionPlans`, `SubscriptionOrders` và `UserSubscriptions`; bảy EF migrations hiện hành được quản lý tại `backend/QOL.Infrastructure/Migrations/`.
- Khi thêm trường/bảng: cập nhật Domain entity → EF configuration/`QolDbContext` nếu cần → tạo migration → cập nhật API và frontend → kiểm thử → áp dụng migration bằng `dotnet ef database update`. Không sửa/xóa migration đã áp dụng và không chạy song song bộ SQL tạo lại các bảng này.
- Đối chiếu 7 script SQL mở rộng, các bảng hiện hữu và phần chưa được ứng dụng dùng ở [sql-archive-reconciliation.md](./docs/database-design/sql-archive-reconciliation.md). Các bảng thông báo, thanh toán tự động, audit/settings và nội dung dịch/TTS trong gói SQL chưa có luồng ứng dụng; chưa nên tạo riêng lẻ trong database.

### Kiểm thử hiện tại

- Backend: `dotnet test QOL.sln -c Release`; hiện có unit tests và API integration tests chạy bằng WebApplicationFactory cùng EF InMemory. Các test API bao phủ inactive user cookie, trạng thái media, yêu cầu subscription và thời lượng WAV.
- Frontend: `npm.cmd run build --workspace @qol/user-web`; hiện chưa có test runner hoặc frontend test files.
- Vì vậy build xanh xác nhận biên dịch, chưa thay thế cho kiểm thử luồng đăng nhập, upload, đơn hàng hay quyền Admin trên runtime.

Đã có schema khởi tạo cho `Users`, `Locations`, `Languages`, `AudioFiles`, `Favorites` và `ListeningHistory`. Languages có seed `vi`, `en`, `ko`, `zh`, `ja`; frontend đọc catalog từ API. API hiện có endpoint đọc danh sách ngôn ngữ, địa điểm và chi tiết địa điểm.

Đăng ký, đăng nhập bằng cookie HTTP-only, hồ sơ cơ bản, phân quyền User/Admin, CRUD địa điểm, nội dung/audio thủ công, yêu thích, lịch sử nghe, đánh giá địa điểm, gói dịch vụ, đơn hàng Pending và xác minh thủ công đã được triển khai. Admin có thể ẩn/hiện đánh giá. Chưa kết nối dịch vụ thanh toán, email, dịch thuật hoặc TTS bên ngoài.

Trang chủ User Web có phần giới thiệu, tìm kiếm địa điểm, ngôn ngữ và lối vào trang gói dịch vụ. Danh sách địa điểm trên trang chủ và trang Khám phá được tải từ API; hình minh họa dùng gradient nội bộ, không tải ảnh Unsplash. Admin nhập tiêu đề, bản dịch và tệp audio (MP3/WAV/OGG/M4A tối đa 20 MB) tại `/admin`; tệp được lưu cục bộ trong `backend\QOL.API\App_Data\audio`. WAV được đo thời lượng từ RIFF header; các định dạng MP3/OGG/M4A dùng thời lượng khai báo bị giới hạn tối đa một giờ. URL `/media/audio/{fileName}` được kiểm tra trạng thái audio, địa điểm và subscription còn hạn trước khi phát; Admin vẫn có thể nghe thử nội dung đã ẩn. Người dùng phải đăng nhập và có subscription còn hạn để lấy danh sách audio; thiếu gói sẽ nhận HTTP 403. Gói/đơn hàng được quản lý tại `/plans` và `/orders`; đơn luôn Pending cho tới khi Admin xác minh thủ công.

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

Compose khởi động SQL Server, tạo database và tài khoản riêng, chạy migration bằng tài khoản `qol_migrator`, sau đó chạy API với tài khoản ít quyền `qol_app`. Mật khẩu ứng dụng/migration chỉ nhận chữ, số, `_`, `@`, `#`, `%`, `!`, `-`; dài ít nhất 12 ký tự và gồm chữ hoa, chữ thường, số, ký tự đặc biệt. Nếu đổi thông tin tài khoản sau lần khởi chạy đầu, cần cập nhật login trong SQL Server tương ứng.

API health check `http://localhost:8080/health` kiểm tra kết nối database; OpenAPI ở `/openapi/v1.json` trong Development. SQL Server lắng nghe tại cổng `1433`. Compose chỉ dành cho phát triển; Production cần secrets manager, HTTPS, CORS origins cụ thể và triển khai migration riêng.

## Trạng thái và phần còn thiếu trước khi dùng thật

- **Đã có:** xác thực/phân quyền, hồ sơ, địa điểm, audio và bản dịch thủ công, yêu thích, lịch sử nghe, đánh giá có điều phối Admin, quản lý gói, đơn hàng Pending, xác minh đơn thủ công và dashboard số liệu.
- **Còn thiếu:** cài đặt quản trị và các tiện ích vận hành như thông báo/email.
- **Chưa tích hợp:** nhà cung cấp thanh toán, dịch thuật tự động và TTS. Không đánh dấu thanh toán thành công nếu chưa được xác minh.
- **Trước Production:** bật HTTPS, cấu hình CORS theo domain thật, quản lý Data Protection keys và secrets, thêm kiểm thử tích hợp/E2E và rà soát bảo mật; chạy migration bằng tài khoản chuyên dụng.
