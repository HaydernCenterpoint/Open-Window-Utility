# Open Window Utility

Tiện ích Windows 10/11 miễn phí, mã nguồn mở: cài app, tinh chỉnh có hoàn tác, bật tính năng Windows, dọn rác, và cấu hình Windows Update từ một ứng dụng WPF native.

Lấy cảm hứng từ [Chris Titus Tech WinUtil](https://christitus.com/windows-tool/). Đây là bản viết lại độc lập (C# / WPF, MIT), **không** liên kết với CTT và không sao chép mã nguồn, JSON hay thương hiệu WinUtil.

## Tải về

EXE portable nằm ở [Releases](https://github.com/HaydernCenterpoint/Open-Window-Utility/releases/latest). Copy EXE sang USB hoặc thư mục rồi chạy với quyền Administrator — không cần PowerShell, không cần cài đặt.

Tinh chỉnh, tính năng, dọn rác và Máy này chạy được khi không có mạng. Cài app và kiểm tra bản EXE mới cần internet. Bấm chip phiên bản khi muốn cập nhật; lúc mở app không gọi mạng.

Cài đặt, log và journal hoàn tác nằm trong thư mục `data` cạnh EXE nếu ghi được.

```powershell
git clone https://github.com/HaydernCenterpoint/Open-Window-Utility.git
```

## Yêu cầu

- Windows 10 22H2 hoặc Windows 11 (x64)
- Quyền Administrator
- Nên có [WinGet](https://aka.ms/getwinget); Chocolatey là dự phòng

## Chạy từ mã nguồn

```powershell
dotnet build OpenWindowUtility.slnx -c Release
dotnet run --project src/OpenWindowUtility.App/OpenWindowUtility.App.csproj
```

## v2 gồm

- **Ứng dụng** — catalog ~80 gói qua WinGet / Chocolatey
- **Tinh chỉnh** — thiết yếu / nâng cao / công tắc, có journal hoàn tác và điểm khôi phục
- **Cấu hình hệ thống** — DISM, sửa lỗi, Control Panel cổ điển
- **Dọn dẹp** — quét rác đã biết, phân loại SAFE/DEEP, chỉ xóa mục bạn chọn
- **Windows Update** — Mặc định / Bảo mật / Tắt hết (phải gõ DISABLE)
- **Win11 Creator** — Bỏ qua kiểm tra TPM 2.0 / CPU / RAM / Secure Boot, bỏ qua bắt buộc tài khoản Microsoft, tự động xuất file `autounattend.xml` hoặc đóng gói ISO Windows 11 tùy chỉnh

## An toàn

Đọc [docs/SAFETY.md](docs/SAFETY.md). Giữ tùy chọn tạo restore point. Không thử BitLocker / tắt hết update trên máy chính.

## Giấy phép

MIT. Xem [LICENSE](LICENSE).
