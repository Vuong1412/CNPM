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
