# Báo cáo kiểm tra lỗi không Create/Join match (C#) và đối chiếu Go

## 1) Tóm tắt log bạn cung cấp

Từ log server C#:
- `POST /game/advertisement/host` trả `200` và có log `Advertisement created`.
- `GET /game/advertisement/findAdvertisements` chạy lặp lại bình thường.
- Không thấy `4xx/5xx` ở các route advertisement trong phiên log này.
- WebSocket `/wss/` reconnect theo chu kỳ ~60s (do timeout đọc), không phải lỗi create/join trực tiếp.

=> Bề ngoài route trả thành công, nhưng phía client vẫn có thể thất bại vào match nếu endpoint BattleServer thực tế chưa sẵn sàng.

---

## 2) Đối chiếu với Go và nguyên nhân gốc

So với Go:
1. **Go yêu cầu BattleServer hợp lệ cho AoE4/AoM ngay từ khởi tạo game** (không có battle server hợp lệ thì fail init).
2. **Go host advertisement sẽ trả lỗi** nếu `relayRegion` không map được battle server configured (không fallback thành công giả).

Trong C# trước khi sửa:
- `BattleServerRuntime` đọc config với `onlyValid: false` (có thể lấy config stale/chưa ready).
- Host route fallback `CreateDefaultBattleServer()` khi không resolve được region, dẫn tới **server vẫn trả success host** dù BattleServer chưa usable.
- Vì vậy có tình huống “host thành công trên API nhưng game không vào trận được”.

---

## 3) Các thay đổi đã thực hiện để khắc phục

### A. Bắt buộc BattleServer sẵn sàng trước khi server game chạy (AoE4/AoM)
**File:** `AgeLanServer.Server/LanServer.cs`
- Thêm `EnsureBattleServerReadyAsync(...)`.
- Với `age4/athens`, server sẽ:
  - kiểm tra battle server ready,
  - chờ tối đa 30s,
  - nếu vẫn chưa sẵn sàng thì dừng khởi động với thông báo rõ ràng.

### B. Chỉ dùng battle server config hợp lệ (ready)
**File:** `AgeLanServer.Server/Internal/BattleServerRuntime.cs`
- Đổi `BattleServerConfigManager.LoadConfigs(gameId, onlyValid: true)`.
- Bổ sung helper:
  - `RequiresDedicatedBattleServer(...)`
  - `HasReadyBattleServers(...)`
  - `WaitForReadyBattleServersAsync(...)`

### C. Siết parity host route theo Go (không success giả)
**File:** `AgeLanServer.Server/Routes/Advertisement/AdvertisementEndpoints.cs`
- Thay `ResolveBattleServer(...)` bằng `TryResolveBattleServer(...)` có lý do lỗi.
- Nếu `relayRegion` rỗng/không map được battle server hợp lệ => trả lỗi host thay vì tạo advertisement thành công giả.
- Bổ sung check `SESSION_MATCH_KEY` cho non-AoE4 giống hướng Go.

### D. Bổ sung log lỗi theo route (đúng yêu cầu)
**Files:**
- `AgeLanServer.Server/LanServer.cs`
- `AgeLanServer.Server/Routes/Advertisement/AdvertisementEndpoints.cs`

Đã thêm:
- log exception và status >= 500 theo route,
- log warning chi tiết lý do reject cho:
  - `/game/advertisement/host`
  - `/game/advertisement/join`

---

## 4) Kết quả kỳ vọng sau fix

1. Không còn trạng thái “create match thành công giả” khi BattleServer.exe chưa ready.
2. Nếu battle server chưa sẵn sàng, server báo lỗi rõ ràng ngay lúc startup hoặc tại route host với lý do cụ thể.
3. Join/create match đồng nhất hành vi với Go hơn, giảm lỗi vào trận thất bại.

---

## 5) Checklist verify nhanh

1. Khởi động `battle-server-manager ... start --game age4` (đợi ready).
2. Khởi động `server.exe --game age4`.
3. Login 2 client.
4. Client A host lobby.
5. Client B find + join lobby.
6. Start match.

Nếu BattleServer chưa ready, log sẽ hiển thị lý do cụ thể thay vì silent success.
