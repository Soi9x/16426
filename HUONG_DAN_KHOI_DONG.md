# Hướng dẫn khởi động Age LAN Server - C# .NET 10.0

## 📋 Mục lục

1. [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
2. [Build dự án](#build-dự-án)
3. [Tạo chứng chỉ SSL](#tạo-chứng-chỉ-ssl)
4. [Chạy Server chính](#chạy-server-chính)
5. [Chạy Launcher](#chạy-launcher)
6. [Quản lý Battle Server](#quản-lý-battle-server)
7. [Cấu hình hệ thống](#cấu-hình-hệ-thống)
8. [Giám sát game (Agent)](#giám-sát-game-agent)
9. [Quy trình đầy đủ cho buổi chơi LAN](#quy-trình-đầy-đủ-cho-buổi-chơi-lan)
10. [Cấu hình qua file TOML](#cấu-hình-qua-file-toml)
11. [Xử lý lỗi thường gặp](#xử-lý-lỗi-thường-gặp)
12. [Danh sách executables](#danh-sách-executables)

---

## Yêu cầu hệ thống

### Phần mềm
- **.NET 10.0 SDK** trở lên (kiểm tra: `dotnet --version`)
- **Windows 7+** hoặc **Linux kernel 3.2+** hoặc **macOS 12+**
- Quyền **Administrator** (để mở port 443, sửa hosts file, cài chứng chỉ)

### Mạng
- Các máy phải cùng **mạng LAN** (hoặc VPN)
- Mở firewall cho:
  - **Port 443** (TCP) - Server HTTPS
  - **Port 31978** (UDP) - Khám phá server LAN

---

## Build dự án

### Build Release
```bash
cd Y:\AOE-IV-LAN-PROJECT\ageLANServer.CSharp
dotnet build AgeLanServer.slnx -c Release
```

### Publish tất cả executables
```bash
dotnet publish AgeLanServer.Server/AgeLanServer.Server.csproj -c Release -o publish\server
dotnet publish AgeLanServer.Launcher/AgeLanServer.Launcher.csproj -c Release -o publish\launcher
dotnet publish AgeLanServer.ServerGenCert/AgeLanServer.ServerGenCert.csproj -c Release -o publish\genCert
dotnet publish AgeLanServer.LauncherAgent/AgeLanServer.LauncherAgent.csproj -c Release -o publish\agent
dotnet publish AgeLanServer.LauncherConfig/AgeLanServer.LauncherConfig.csproj -c Release -o publish\config
dotnet publish AgeLanServer.LauncherConfigAdmin/AgeLanServer.LauncherConfigAdmin.csproj -c Release -o publish\config-admin
dotnet publish AgeLanServer.LauncherConfigAdminAgent/AgeLanServer.LauncherConfigAdminAgent.csproj -c Release -o publish\config-admin-agent
dotnet publish AgeLanServer.BattleServerManager/AgeLanServer.BattleServerManager.csproj -c Release -o publish\battle-server-manager
```

### Build + Publish một lệnh
```bash
dotnet publish AgeLanServer.slnx -c Release -o publish
```

---

## Tạo chứng chỉ SSL

> ⚠️ Chỉ cần làm **lần đầu tiên** hoặc khi chứng chỉ hết hạn.

### Cách 1: Dùng genCert.exe (khuyến nghị)
```bash
cd publish\genCert
genCert.exe --replace
```

**Kết quả:** 5 file trong `resources/certificates/`:
| File | Mô tả |
|---|---|
| `cacert.pem` | CA certificate (game dùng để trust server) |
| `cert.pem` | Leaf certificate (server dùng cho HTTPS) |
| `key.pem` | Private key của leaf cert |
| `selfsigned_cert.pem` | Self-signed cert (fallback cho game cũ) |
| `selfsigned_key.pem` | Private key của self-signed cert |

### Cách 2: Dùng OpenSSL
```bash
mkdir resources\certificates
openssl req -x509 -newkey rsa:4096 -keyout resources\certificates\key.pem -out resources\certificates\cert.pem -days 3650 -nodes
copy resources\certificates\cert.pem resources\certificates\selfsigned_cert.pem
copy resources\certificates\key.pem resources\certificates\selfsigned_key.pem
```

---

## Chạy Server chính

### Cơ bản
```bash
cd publish\server
server.exe --game age4
```

### Với cấu hình tuỳ chỉnh
```bash
server.exe --game age4 --port 443 --host 0.0.0.0 --log
```

### Tham số dòng lệnh
| Tham số | Mô tả | Mặc định |
|---|---|---|
| `--game`, `-g` | ID game (age1, age2, age3, age4, athens) | **Bắt buộc** |
| `--port` | Cổng HTTPS lắng nghe | `443` |
| `--host` | Địa chỉ IP lắng nghe | `0.0.0.0` |
| `--log` | Bật ghi log requests | `true` |

### Server sẽ:
- ✅ Lắng nghe HTTPS trên port 443
- ✅ Tự động load chứng chỉ từ `resources/certificates/`
- ✅ Phản hồi UDP announce trên port 31978
- ✅ Phục vụ 100+ API endpoints cho game client
- ✅ Quản lý lobby, chat, invitation, party, items, leaderboard...

> ⚠️ Cần quyền **Administrator** để mở port 443. Nếu không có, đổi sang port > 1024.

---

## Chạy Launcher

### Cơ bản (Steam)
```bash
cd publish\launcher
launcher.exe --game age4 --client-exe steam
```

### Cơ bản (Xbox/Microsoft Store)
```bash
launcher.exe --game age4 --client-exe msstore
```

### Launcher tuỳ chỉnh
```bash
launcher.exe --game age4 --client-exe "C:\path\to\custom\launcher.exe" --client-path "C:\path\to\game"
```

### Không sửa hosts/cert (nếu đã cấu hình sẵn)
```bash
launcher.exe --game age4 --client-exe steam --no-hosts --no-cert
```

### Không cô lập dữ liệu user
```bash
launcher.exe --game age4 --client-exe steam --no-isolate
```

### Tham số dòng lệnh
| Tham số | Mô tả | Mặc định |
|---|---|---|
| `--game`, `-g` | ID game | **Bắt buộc** |
| `--client-exe` | Launcher client (`steam`, `msstore`, hoặc đường dẫn) | `steam` |
| `--client-path` | Đường dẫn thư mục game | `` |
| `--server-exe` | Đường dẫn file thực thi server | Tự động tìm |
| `--no-cert` | Không cài chứng chỉ | `false` |
| `--no-hosts` | Không sửa file hosts | `false` |
| `--no-isolate` | Không cô lập metadata/profiles | `false` |

### Launcher sẽ tự động:
1. 🔍 Khám phá server LAN qua UDP multicast/broadcast
2. 🖥️ Nếu không tìm thấy → hỏi có muốn khởi động server mới không
3. 🔐 Cài chứng chỉ vào kho tin cậy (nếu cần)
4. 📝 Sửa file hosts để trỏ game domains về server IP
5. 💾 Backup metadata và profiles user
6. 🎮 Khởi động game qua client đã chọn
7. 👀 Giám sát tiến trình game đến khi thoát
8. 🔄 Tự động đảo ngược toàn bộ cấu hình khi thoát

---

## Quản lý Battle Server

> Dùng cho **AoE IV** và **AoM: Retold** - cần battle server riêng cho matchmaking.

### Khởi động battle server
```bash
cd publish\battle-server-manager
battle-server-manager.exe start --game age4
```

### Với cấu hình tuỳ chỉnh
```bash
battle-server-manager.exe start --game age4 --region lan-myserver --name "My LAN Server"
```

### Xem danh sách battle server đang chạy
```bash
# Kiểm tra file cấu hình trong %TEMP%\ageLANServer\battle-servers\age4\
dir %TEMP%\ageLANServer\battle-servers\age4\*.toml
```

### Dừng battle server theo region
```bash
battle-server-manager.exe remove --game age4 --region lan-myserver
```

### Dọn dẹp cấu hình hỏng
```bash
battle-server-manager.exe clean --game age4
```

### Xóa toàn bộ
```bash
battle-server-manager.exe remove-all --game age4
```

### Tham số dòng lệnh
| Tham số | Mô tả | Mặc định |
|---|---|---|
| `--game`, `-g` | ID game | **Bắt buộc** |
| `--region`, `-r` | Tên region | `auto` (tự sinh) |
| `--name`, `-n` | Tên server | `auto` (tự sinh) |
| `--host` | Địa chỉ IP lắng nghe | `127.0.0.1` |

---

## Cấu hình hệ thống

> Dùng khi cần cấu hình thủ công (không qua launcher).

### Setup cấu hình
```bash
cd publish\config-admin
config-admin.exe setup --ip 192.168.1.100 --cert "<base64_cert>" --hosts "aoe-api.reliclink.com,*.worldsedgelink.com" --gameId age4
```

### Đảo ngược cấu hình
```bash
config-admin.exe revert --ip 192.168.1.100 --cert "<base64_cert>" --all
```

### Tham số dòng lệnh
| Tham số | Mô tả |
|---|---|
| `--cert` | Chứng chỉ base64 để thêm vào kho tin cậy |
| `--ip` | IP server để ánh xạ vào hosts file |
| `--hosts` | Danh sách host cần ánh xạ (phân cách bởi `,`) |
| `--gameId` | ID game |
| `--all` | Đảo ngược toàn bộ (setup và revert) |
| `--logRoot` | Thư mục gốc cho log |

> ⚠️ `config-admin.exe` yêu cầu quyền **Administrator**.

---

## Giám sát game (Agent)

> Agent giám sát tiến trình game và xử lý cleanup khi game thoát.

### Cơ bản
```bash
cd publish\agent
agent.exe --game age4 --steam --logDir "logs\age4"
```

### Với Xbox
```bash
agent.exe --game age4 --xbox --logDir "logs\age4"
```

### Tham số dòng lệnh
| Tham số | Mô tả |
|---|---|
| `--game`, `-g` | ID game |
| `--steam` | Game chạy từ Steam |
| `--xbox` | Game chạy từ Xbox/Microsoft Store |
| `--logDir`, `-l` | Thư mục đích cho log game |

---

## Quy trình đầy đủ cho buổi chơi LAN

### Máy chủ (Host)

```bash
# 1. Tạo chứng chỉ (lần đầu)
cd publish\genCert
genCert.exe --replace

# 2. Chạy server
cd ..\server
server.exe --game age4

# Server chạy nền, giữ cửa sổ này mở
```

### Máy client 1 (Người chơi đầu tiên)

```bash
cd publish\launcher
launcher.exe --game age4 --client-exe steam

# Launcher sẽ:
# - Tự khám phá server qua UDP
# - Cài chứng chỉ, sửa hosts
# - Khởi động game
# - Giám sát game đến khi thoát
# - Tự dọn dẹp khi đóng
```

### Máy client 2, 3, 4...

```bash
# Giống client 1 - launcher tự động khám phá server
cd publish\launcher
launcher.exe --game age4 --client-exe steam
```

### Khi chơi xong

```bash
# Nhấn Ctrl+C trong launcher → tự động revert cấu hình
# Nhấn Ctrl+C trong server → dừng server
```

---

## Cấu hình qua file TOML

### Server config
**File:** `publish\server\resources\config\config.toml`

```toml
[Server]
Port = 443
Host = "0.0.0.0"
GameId = "age4"
LogRequests = true

[Certificate]
# Tự động tìm trong resources/certificates/
```

### Launcher config
**File:** `publish\launcher\resources\config.toml`

```toml
[Client]
Executable = "steam"
Path = ""
ExtraArgs = ""

[Server]
AutoStart = true
AutoStop = true
AnnouncePort = 31978

[Features]
TrustCertificate = true
MapHosts = true
IsolateMetadata = true
IsolateProfiles = true
LogToFile = true
```

### Launcher config theo game
**File:** `publish\launcher\resources\config.age4.toml`

```toml
# Override config cho AoE4 cụ thể
[Client]
Executable = "steam"
# Đường dẫn game custom (nếu không dùng Steam/Xbox)
# Path = "C:\\Games\\AgeOfEmpires4"
```

---

## Xử lý lỗi thường gặp

### Lỗi: Không mở được port 443
```
Error: Access denied - không thể lắng nghe trên port 443
```
**Giải pháp:**
- Chạy server với quyền Administrator
- Hoặc đổi port trong config: `server.exe --game age4 --port 8443`

### Lỗi: Launcher không tìm thấy server
```
Error: Không tìm thấy server LAN và auto-start bị tắt
```
**Giải pháp:**
- Đảm bảo server đang chạy trên máy host
- Kiểm tra firewall cho port 31978 (UDP)
- Các máy phải cùng mạng LAN
- Thử chạy launcher với `--server-exe <đường dẫn>` để tự khởi động server

### Lỗi: Game không kết nối được
```
Game multiplayer error - cannot connect to server
```
**Giải pháp:**
- Kiểm tra file hosts đã được sửa: `C:\Windows\System32\drivers\etc\hosts`
- Kiểm tra chứng chỉ đã được cài vào kho tin cậy
- Thạy chạy `config-admin.exe revert --all` rồi chạy lại launcher

### Lỗi: Chứng chỉ hết hạn
```
SSL certificate has expired
```
**Giải pháp:**
```bash
cd publish\genCert
genCert.exe --replace
# Copy certificates sang thư mục resources/certificates/ của server và launcher
```

### Lỗi: Battle server không khởi động
```
Error: Không tìm thấy file thực thi BattleServer
```
**Giải pháp:**
- Đảm bảo đã cài game và BattleServer.exe tồn tại
- Với AoE4/AoM, trỏ đến BattleServer.exe của AoE2DE:
  ```bash
  battle-server-manager.exe start --game age4 --executable "S:\SteamLibrary\steamapps\common\AoE2DE\BattleServer\BattleServer.exe"
  ```

### Lỗi: Steam không mở được game
```
Steam URI không hoạt động
```
**Giải pháp:**
- Đảm bảo Steam đang chạy
- Kiểm tra AppId game trong code (AoE4 = 1466860)
- Thử dùng launcher custom với `--client-exe <đường dẫn>`

---

## Danh sách Executables

| Executable | Thư mục | Chức năng |
|---|---|---|
| `server.exe` | `publish\server\` | Web server chính (ASP.NET Core Kestrel) |
| `launcher.exe` | `publish\launcher\` | Launcher chính (khám phá server, cấu hình, khởi động game) |
| `genCert.exe` | `publish\genCert\` | Tạo chứng chỉ SSL self-signed và CA-signed |
| `config.exe` | `publish\config\` | Cấu hình hệ thống (setup/revert) |
| `config-admin.exe` | `publish\config-admin\` | Cấu hình admin (cert LocalMachine, hosts file) |
| `config-admin-agent.exe` | `publish\config-admin-agent\` | IPC agent nhận yêu cầu từ config.exe |
| `agent.exe` | `publish\agent\` | Giám sát tiến trình game, copy log |
| `battle-server-manager.exe` | `publish\battle-server-manager\` | Quản lý battle server cho AoE4/AoM |

### Game IDs hỗ trợ

| ID | Game |
|---|---|
| `age1` | Age of Empires: Definitive Edition |
| `age2` | Age of Empires II: Definitive Edition |
| `age3` | Age of Empires III: Definitive Edition |
| `age4` | Age of Empires IV: Anniversary Edition |
| `athens` | Age of Mythology: Retold |

---

## Phụ thuộc giữa các thành phần

```
launcher.exe
├── server.exe (tự khởi động nếu cần)
├── config-admin-agent.exe (IPC cho cấu hình admin)
├── config.exe (setup/revert cấu hình)
├── agent.exe (giám sát game)
├── battle-server-manager.exe (quản lý battle server)
└── game client (Steam/Xbox/Custom)

server.exe
├── genCert.exe (tạo chứng chỉ nếu thiếu)
└── resources/certificates/*.pem
```

---

## Liên kết hữu ích

- **Repo gốc (Go):** https://github.com/luskaner/ageLANServer
- **Steam Emulator (offline 100%):** https://github.com/luskaner/ageLANServerLauncherCompanion
- **Tài liệu server gốc:** https://github.com/luskaner/ageLANServer/tree/main/server
- **Tài liệu launcher gốc:** https://github.com/luskaner/ageLANServer/tree/main/launcher
