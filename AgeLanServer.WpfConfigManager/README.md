# AgeLanServer.WpfConfigManager

WPF Config Manager giao diện Modern (Dark/Light, `WindowStyle=None`) để:

- Quản lý profile theo từng game (`age1`, `age2`, `age3`, `age4`, `athens`)
- Nạp/Lưu đầy đủ trường cấu hình trong `config.toml` và `config.game.toml`
- Khởi động game bằng launcher profile đang chọn
- Stop launcher và dọn dẹp tài nguyên (revert + kill process theo cấu hình)
- Tự động nạp profile khi mở ứng dụng

## Build (Windows)

```bash
dotnet build AgeLanServer.WpfConfigManager/AgeLanServer.WpfConfigManager.csproj -c Release
```

> Project WPF được tách riêng, chưa thêm vào `AgeLanServer.slnx` để tránh ảnh hưởng pipeline cross-platform hiện tại.
