# Age LAN Server - C# .NET 10.0 Port Documentation

## Tổng quan dự án

Dự án này là bản port toàn bộ từ **Go** sang **C# .NET 10.0** của hệ thống **Age LAN Server** - một web server cho phép chơi multiplayer Age of Empires qua LAN mà không cần kết nối internet tới server chính thức.

### Bảng ánh xạ module Go → C#

| Module Go gốc | Module C# .NET 10 | Tên assembly | Trạng thái |
|---|---|---|---|
| `common` | `AgeLanServer.Common` | `AgeLanServer.Common.dll` | ✅ Hoàn thành |
| `launcher-common` | `AgeLanServer.LauncherCommon` | `AgeLanServer.LauncherCommon.dll` | ✅ Hoàn thành |
| `server` | `AgeLanServer.Server` | `server.dll` | ✅ Hoàn thành |
| `server-genCert` | `AgeLanServer.ServerGenCert` | `genCert.dll` | ✅ Hoàn thành |
| `launcher-agent` | `AgeLanServer.LauncherAgent` | `agent.dll` | ✅ Hoàn thành |
| `launcher-config` | `AgeLanServer.LauncherConfig` | `config.dll` | ✅ Hoàn thành |
| `launcher-config-admin` | `AgeLanServer.LauncherConfigAdmin` | `config-admin.dll` | ✅ Hoàn thành |
| `launcher-config-admin-agent` | `AgeLanServer.LauncherConfigAdminAgent` | `config-admin-agent.dll` | ✅ Hoàn thành |
| `launcher` | `AgeLanServer.Launcher` | `launcher.dll` | ✅ Hoàn thành |
| `battle-server-manager` | `AgeLanServer.BattleServerManager` | `battle-server-manager.dll` | ✅ Hoàn thành |
| `battle-server-broadcast` | `AgeLanServer.BattleServerBroadcast` | `AgeLanServer.BattleServerBroadcast.dll` | ✅ Hoàn thành |

---

## Chi tiết từng module

### 1. AgeLanServer.Common (Core Shared Library)

**Mục đích:** Thư viện dùng chung cho toàn bộ hệ thống.

**Các lớp chính:**

| Lớp | Mô tả | Tương đương Go |
|---|---|---|
| `AppConstants` | Hằng số: tên app, file cert, cổng announce, header HTTP | `common/common.go` |
| `ErrorCodes` | Mã lỗi chung (Success, General, Signal, PidLock, ...) | `common/errors.go` |
| `GameIds` | ID 5 game: age1, age2, age3, age4, athens + tập SupportedGames | `common/game.go` |
| `GameDomains` | Quản lý tên miền game, domain mappings cho hosts file | `common/domain.go` |
| `DnsResolver` | Phân giải DNS, cache IP-host, kiểm tra connectivity | `common/resolve.go` |
| `ProcessManager` | Tìm, theo dõi, dừng tiến trình game | `common/process/` |
| `PidFileLock` | Khóa file PID đảm bảo single instance | `common/fileLock/` |
| `CertificateManager` | Quản lý chứng chỉ SSL: đọc, kiểm tra, tạo folder | `common/cert.go` |
| `ConfigLoader` | Nạp cấu hình TOML, env vars, CLI args | `common/config.go` |
| `ExecutablePaths` | Tìm đường dẫn file thực thi | `common/executables/` |
| `CommandExecutor` | Thực thi lệnh, kiểm tra quyền admin | `common/executor/` |
| `CommandArgsParser` | Phân tích tham số dòng lệnh, thay thế biến | `common/parse.go` |
| `AppLogger` | Hệ thống ghi log có buffer | `common/logger/` |
| `BattleServerConfigManager` | Quản lý file cấu hình battle server TOML | `common/battleServerConfig/` |
| `AnnounceTypes` | Các phiên bản announce UDP (V0, V1, V2) | `common/announce.go` |
| `Utilities` | User-Agent, kiểm tra interactive mode | `common/http.go`, `common/terminal.go` |

**Package dependencies:**
- `Tomlyn` - Đọc/ghi TOML
- `Microsoft.Extensions.Logging.Abstractions`
- `System.CommandLine`

---

### 2. AgeLanServer.LauncherCommon (Launcher Shared Library)

**Mục đích:** Thư viện dùng chung cho launcher components.

**Các lớp chính:**

| Lớp | Mô tả | Tương đương Go |
|---|---|---|
| `HostsManager` | Quản lý file hosts: thêm/xóa ánh xạ IP-host, backup/restore, flush DNS | `launcher-common/hosts/` |
| `UserDataManager` | Backup/restore metadata và profiles game | `launcher-common/userData/` |
| `ConfigRevertManager` | Lưu và thực thi revert cấu hình (flags-based) | `launcher-common/configRevert.go` |
| `ArgsStore` | Lưu trữ tham số dòng lệnh vào file | `launcher-common/argsStore.go` |
| `CertificateUtilities` | Thêm/xóa cert khỏi kho tin cậy (Windows LocalMachine ROOT, Linux CA trust) | `launcher-common/cert/` |
| `GameCertificateManager` | Quản lý CA cert trong thư mục game (cacert.pem) | `launcher-common/cert/ca.go` |
| `IpcConstants` | Hằng số IPC: action bytes, named pipe path, data records | `launcher-common/ipc/` |
| `LauncherErrorCodes` | Mã lỗi riêng: NotAdmin, InvalidGame | `launcher-common/errors.go` |

---

### 3. AgeLanServer.Server (Core Web Server)

**Mục đích:** Web server ASP.NET Core xử lý API requests của game.

**Tính năng chính:**
- ✅ HTTPS với Kestrel (load cert + key riêng)
- ✅ Health check endpoint `/test`
- ✅ Login API `/api/login`
- ✅ Player presence `/api/player/presence`
- ✅ Lobby CRUD: tạo, đọc, cập nhật, xóa lobby
- ✅ Join/Leave/Invite lobby
- ✅ Game sessions: tạo và restore
- ✅ Static resources: achievements, leaderboards, items, challenges, presence data, automatch maps, cloud files
- ✅ Shutdown endpoint `/shutdown`
- ✅ Middleware logging và CORS

**Các lớp chính:**

| Lớp | Mô tả |
|---|---|
| `LanServer` | Lớp chính: cấu hình WebApplication, đăng ký endpoints, quản lý lobbies/players/sessions trong-memory |
| `Program` | Entry point: đọc cấu hình TOML, tìm cert, khởi động server |

**Package dependencies:**
- `Microsoft.AspNetCore.App` (via SDK Web)
- `AgeLanServer.Common`

---

### 4. AgeLanServer.ServerGenCert (Certificate Generator)

**Mục đích:** Tạo chứng chỉ SSL self-signed và CA-signed.

**Tính năng:**
- ✅ Tạo self-signed certificate (RSA 2048-bit)
- ✅ Tạo CA certificate (RSA 4096-bit)
- ✅ Tạo leaf certificate ký bởi CA
- ✅ SAN: wildcard domains cho tất cả game
- ✅ Flag `-r/--replace` để thay thế cert cũ

**Lớp chính:** `CertificateGenerator` + `Program`

---

### 5. AgeLanServer.LauncherAgent (Game Process Watcher)

**Mục đích:** Giám sát tiến trình game, copy log khi game thoát.

**Các lớp chính:**

| Lớp | Mô tả |
|---|---|
| `ProcessWatcher` | Chờ game khởi động (timeout 1 phút), giám sát đến khi thoát, gọi cleanup |
| `GameLogCopier` | Sao chép file log và thư mục log theo game |
| `Program` | CLI với flags: `--game`, `--steam`, `--xbox`, `--logDir` |

**Log paths hỗ trợ:**
- AoE1: `Logs/StartupLog.txt`
- AoE2: `logs/Age2SessionData.txt`
- AoE3: `Logs/Age3SessionData.txt`, `Age3Log.txt`
- AoE4: `session_data.txt`, `warnings.log`, `LogFiles/unhandled.*.txt`
- AoM: `temp/Logs/mythsessiondata.txt`, `mythlog.txt`

---

### 6. AgeLanServer.LauncherConfig (System Configurator)

**Mục đích:** Thiết lập và đảo ngược cấu hình hệ thống.

**Lệnh `setup`:**
1. Thêm chứng chỉ vào kho tin cậy user
2. Backup metadata và profiles
3. Thêm CA cert vào game (age2, age3, athens)
4. Ánh xạ IP vào hosts file
5. Thông báo admin agent qua IPC

**Lệnh `revert`:**
1. Xóa chứng chỉ
2. Khôi phục metadata/profiles
3. Khôi phục CA cert game
4. Xóa ánh xạ hosts
5. Thông báo admin agent

**Lớp chính:** `LauncherConfig` + `Program`

---

### 7. AgeLanServer.LauncherConfigAdmin (Admin Config Tool)

**Mục đích:** Thao tác cần quyền admin - trust cert LocalMachine, sửa hosts file hệ thống.

**Yêu cầu:** Phải chạy với quyền admin.

**Lớp chính:**
- `CertificateManager` - Trust/Untrust cert trong LocalMachine ROOT
- `Program` - CLI với `setup` và `revert` commands

---

### 8. AgeLanServer.LauncherConfigAdminAgent (Admin IPC Agent)

**Mục đích:** IPC server chạy nền với quyền admin, nhận yêu cầu từ launcher-config.

**Tính năng:**
- ✅ NamedPipe server lắng nghe IPC
- ✅ Xử lý Action Setup: thêm cert LocalMachine, ánh xạ hosts
- ✅ Xử lý Action Revert: xóa cert, xóa hosts
- ✅ Action Exit: dừng agent
- ✅ PID file lock đảm bảo single instance
- ✅ Kiểm tra quyền admin khi khởi động

**Lớp chính:** `AdminAgent` + `Program`

---

### 9. AgeLanServer.Launcher (Main Launcher)

**Mục đích:** Điều phối toàn bộ quy trình LAN gaming.

**Luồng thực thi:**
1. Kiểm tra game đang chạy → chờ tối đa 1 phút
2. Cleanup ban đầu (kill agent cũ, revert cấu hình trước)
3. Khám phá server LAN qua UDP multicast/broadcast
4. Tạo/xác thực chứng chỉ SSL
5. Cấu hình hệ thống (hosts, metadata isolation, profiles, certs)
6. Lưu tham số revert để cleanup khi thoát
7. Khởi động game (Steam/Xbox/custom)
8. Chờ game thoát (ProcessWatcher)
9. Cleanup: revert cấu hình, dừng server

**CLI flags:**
- `--game/-g` (required): ID game
- `--server-exe`: Đường dẫn server
- `--client-exe`: Launcher client (steam/msstore/path)
- `--client-path`: Đường dẫn game
- `--no-cert`: Không cài chứng chỉ
- `--no-hosts`: Không sửa hosts file
- `--no-isolate`: Không cô lập dữ liệu user

**Lớp chính:** `LauncherApp` + `Program`

---

### 10. AgeLanServer.BattleServerManager (Battle Server Manager)

**Mục đích:** Quản lý Online-like Battle Servers.

**Lệnh:**
- `start` - Khởi động battle server (auto-generate name, region, ports, SSL, exe path)
- `clean` - Xóa cấu hình không hợp lệ
- `remove` - Xóa battle server theo region
- `remove-all` - Xóa toàn bộ

**Tính năng:**
- ✅ Tự động sinh cổng qua TcpListener bind port 0
- ✅ Kiểm tra cổng khả dụng
- ✅ Chờ battle server khởi tạo (timeout 10s)
- ✅ Lưu cấu hình TOML
- ✅ Dừng và dọn dẹp process

**Lớp chính:** `BattleServerManager` (static) + `Program`

---

### 11. AgeLanServer.BattleServerBroadcast (UDP Broadcaster)

**Mục đích:** Phát sóng thông báo UDP multicast/broadcast để khám phá battle server.

**Tính năng:**
- ✅ Gửi announce tới multicast group `239.31.97.8:31978`
- ✅ Gửi tới broadcast `255.255.255.255`
- ✅ Gửi tới tất cả network interfaces
- ✅ Kiểm tra `IsRequired()` - chỉ cần trên Windows, không phải AoE4/AoM

**Lớp chính:** `BattleServerBroadcaster`

---

## Cấu trúc thư mục

```
Y:\AOE-IV-LAN-PROJECT\ageLANServer.CSharp\
├── AgeLanServer.slnx                     # Solution file (.NET 10 format)
├── AgeLanServer.Common\                  # Shared library (13 files)
│   ├── AppConstants.cs
│   ├── ErrorCodes.cs
│   ├── GameIds.cs
│   ├── GameDomains.cs
│   ├── AnnounceTypes.cs
│   ├── DnsResolver.cs
│   ├── ProcessManager.cs
│   ├── PidFileLock.cs
│   ├── CertificateManager.cs
│   ├── ConfigLoader.cs
│   ├── ExecutablePaths.cs
│   ├── CommandExecutor.cs
│   ├── AppLogger.cs
│   ├── CommandArgsParser.cs
│   ├── Utilities.cs
│   └── BattleServerConfig.cs
├── AgeLanServer.LauncherCommon\          # Launcher shared (6 files)
│   ├── ErrorCodes.cs
│   ├── HostsManager.cs
│   ├── UserDataManager.cs
│   ├── ConfigRevertManager.cs
│   ├── CertificateUtilities.cs
│   ├── GameCertificateManager.cs
│   └── IpcConstants.cs
├── AgeLanServer.Server\                  # Core web server (2 files)
│   ├── LanServer.cs
│   └── Program.cs
├── AgeLanServer.ServerGenCert\           # Cert generator (1 file)
│   └── Program.cs
├── AgeLanServer.LauncherAgent\           # Process watcher (3 files)
│   ├── ProcessWatcher.cs
│   ├── GameLogCopier.cs
│   └── Program.cs
├── AgeLanServer.LauncherConfig\          # System config (1 file)
│   └── Program.cs
├── AgeLanServer.LauncherConfigAdmin\     # Admin config (3 files)
│   ├── CertificateManager.cs
│   ├── Program.cs
│   └── ...
├── AgeLanServer.LauncherConfigAdminAgent # IPC agent (1 file)
│   └── Program.cs
├── AgeLanServer.Launcher\                # Main launcher (1 file)
│   └── Program.cs
├── AgeLanServer.BattleServerManager\     # BS manager (1 file)
│   └── Program.cs
└── AgeLanServer.BattleServerBroadcast\   # UDP broadcast (1 file)
    └── BattleServerBroadcaster.cs
```

---

## Hướng dẫn build

```bash
cd Y:\AOE-IV-LAN-PROJECT\ageLANServer.CSharp
dotnet build AgeLanServer.slnx
```

### Build individual projects:
```bash
dotnet build AgeLanServer.Common
dotnet build AgeLanServer.Server
dotnet build AgeLanServer.Launcher
# ... etc
```

### Publish for production:
```bash
dotnet publish AgeLanServer.Server -c Release -o publish/server
dotnet publish AgeLanServer.Launcher -c Release -o publish/launcher
dotnet publish AgeLanServer.ServerGenCert -c Release -o publish/genCert
dotnet publish AgeLanServer.LauncherConfig -c Release -o publish/config
dotnet publish AgeLanServer.LauncherConfigAdmin -c Release -o publish/config-admin
dotnet publish AgeLanServer.LauncherConfigAdminAgent -c Release -o publish/config-admin-agent
dotnet publish AgeLanServer.LauncherAgent -c Release -o publish/agent
dotnet publish AgeLanServer.BattleServerManager -c Release -o publish/battle-server-manager
```

---

## Hướng dẫn sử dụng

### 1. Tạo chứng chỉ SSL
```bash
genCert --replace
```

### 2. Chạy Server
```bash
server --game age4
```

### 3. Chạy Launcher
```bash
launcher --game age4 --client-exe steam
```

### 4. Quản lý Battle Server
```bash
battle-server-manager start --game age4
battle-server-manager clean --game age4
battle-server-manager remove --game age4 --region lan-1234
battle-server-manager remove-all --game age4
```

### 5. Cấu hình hệ thống
```bash
# Setup
config setup --game age4 --ip 192.168.1.100 --cert <base64>

# Revert
config revert --game age4
config revert --all
```

### 6. Admin Config (yêu cầu admin rights)
```bash
config-admin setup --ip 192.168.1.100 --cert <base64> --hosts "aoe-api.reliclink.com" --game age4
config-admin revert --ip 192.168.1.100 --cert <base64>
```

### 7. Admin Agent (IPC server)
```bash
config-admin-agent
```

---

## So sánh tính năng: Go → C#

| Tính năng | Go | C# .NET 10 | Ghi chú |
|---|---|---|---|
| Web server | net/http | ASP.NET Core (Kestrel) | Tương đương, modern hơn |
| TOML config | koanf | Tomlyn | Tương đương |
| CLI | cobra | System.CommandLine | Tương đương |
| Process mgmt | os/exec | System.Diagnostics.Process | Tương đương |
| File locking | syscall.Flock / LockFileEx | FileStream FileShare.None | Tương đương |
| Cert generation | crypto/x509 | System.Security.Cryptography.X509Certificates | Tương đương |
| DNS resolution | miekg/dns | System.Net.Dns | Đơn giản hóa |
| UDP multicast | net.udp | System.Net.Sockets.UdpClient | Tương đương |
| IPC (named pipe) | winio / net.Unix | NamedPipeServer/ClientStream | Tương đương |
| Hosts file | custom | custom | Tương đương |
| Logging | log package | Custom AppLogger | Tương đương |

---

## Những điểm khác biệt chính

1. **Web Framework**: Go dùng `net/http` thuần → C# dùng ASP.NET Core với `WebApplication` builder pattern
2. **Config loading**: Go dùng `koanf` layers → C# dùng `Tomlyn` + `ConfigLoader` custom
3. **CLI**: Go dùng `cobra` → C# dùng `System.CommandLine`
4. **Goroutines → async/await**: Toàn bộ code bất đồng bộ dùng Task-based pattern
5. **Build tags → Runtime checks**: `*_windows.go`, `*_linux.go` → `OperatingSystem.IsWindows()`
6. **go:linkname → Platform APIs**: Certificate store reload hack → Windows API trực tiếp
7. **Channels → async streams/Events**: Go channels → TaskCompletionSource / event handlers

---

## Trạng thái hoàn thành

| Module | Build | Chức năng core | Comments tiếng Việt |
|---|---|---|---|
| Common | ✅ | ✅ | ✅ |
| LauncherCommon | ✅ | ✅ | ✅ |
| Server | ✅ | ✅ | ✅ |
| ServerGenCert | ✅ | ✅ | ✅ |
| LauncherAgent | ✅ | ✅ | ✅ |
| LauncherConfig | ✅ | ✅ | ✅ |
| LauncherConfigAdmin | ✅ | ✅ | ✅ |
| LauncherConfigAdminAgent | ✅ | ✅ | ✅ |
| Launcher | ✅ | ✅ | ✅ |
| BattleServerManager | ✅ | ✅ | ✅ |
| BattleServerBroadcast | ✅ | ✅ | ✅ |

**Tổng: 11/11 modules hoàn thành, build thành công 0 lỗi.**
