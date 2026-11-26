# Baseline kiểm thử QOL

## Môi trường và phạm vi

Baseline được ghi nhận ngày 2026-11-25 trên workspace Windows, .NET 10 và Node.js với dependencies frontend đã có sẵn. Các lệnh build/test chạy từ thư mục gốc dự án, ngoại trừ build frontend chạy trong `frontend`.

## Kết quả trước khi thay đổi chức năng

| Lệnh | Kết quả |
|---|---|
| `dotnet build QOL.sln -c Release --no-restore` | Thành công; 0 warning, 0 error. |
| `dotnet test QOL.sln -c Release --no-restore` | Thành công; 11 passed, 0 failed, 0 skipped. |
| `npm.cmd run build --workspace @qol/user-web` (trong `frontend`) | Thành công; TypeScript build và Vite production build hoàn tất. |

Build frontend tạo 99 modules, JavaScript bundle 406.10 kB (123.86 kB gzip) và CSS 28.07 kB (6.53 kB gzip). Trước thay đổi, test backend chỉ bao gồm domain/persistence-model tests; chưa có kiểm thử endpoint API.

## Lưu ý

- Đây là baseline biên dịch và kiểm thử tự động; chưa chứng minh kết nối với một SQL Server sạch hoặc triển khai production.
- `--no-restore` dùng các gói đã được restore sẵn trong workspace.
- Generated output như `bin/`, `obj/`, `dist/`, `.vs/`, `node_modules/` và media runtime không thuộc source submission.

## Kết quả sau P1/P2

- `dotnet build QOL.sln -c Release --no-restore`: thành công, 0 warning, 0 error.
- `dotnet test QOL.sln -c Release --no-restore`: thành công, 19 passed, 0 failed, 0 skipped; có API tests cho account bị khóa, media/subscription, WAV duration, xóa/thay audio và dọn tệp mồ côi.
- `npm.cmd --prefix frontend run build`: thành công; TypeScript và Vite production build hoàn tất.

## Kết quả sau Visitor, Payments, TTS và CI

| Lệnh | Kết quả |
|---|---|
| `dotnet build QOL.sln --no-restore` | Thành công; 0 warning, 0 error. |
| `dotnet build QOL.sln --configuration Release --no-restore` | Thành công ở output tạm biệt lập; 0 warning, 0 error. Output Release mặc định bị khóa bởi API đang chạy (`QOL.API.exe`), nên không dừng tiến trình người dùng. |
| `dotnet test backend\QOL.Tests\QOL.Tests.csproj --no-restore` | 39 passed, 0 failed, 2 skipped, 41 total. Hai SQL Server tests cần `QOL_TEST_SQLSERVER`. |
| `npm.cmd --prefix frontend run test --workspace @qol/user-web` | 5 passed. |
| `npm.cmd --prefix frontend run build` | Thành công; TypeScript build và Vite production build hoàn tất. |
| `docker compose --env-file .env.example config --quiet` | Thành công. |

`docker build -f frontend\Dockerfile -t qol-frontend:validation .` chưa chạy được do Docker Desktop Linux engine chưa khởi động. CI chạy SQL Server integration tests với service container; các ràng buộc SQL và giao dịch Serializable chưa được xác minh cục bộ trong lần kiểm tra này.
