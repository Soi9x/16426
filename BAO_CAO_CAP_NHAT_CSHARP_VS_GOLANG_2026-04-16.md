# Báo cáo rà soát C# vs Golang và sửa lỗi game báo Offline

## 1) Mục tiêu thực hiện
- Đối chiếu flow giữa `AgeLanServer.GoLang` và `AgeLanServer.Server` (C#).
- Bổ sung các phần thiếu quan trọng từ Go sang C# theo hướng tương thích runtime.
- Sửa lỗi trạng thái game bị báo **Offline** ở bản C#.

## 2) Vấn đề chính phát hiện

### 2.1 Bind request chưa tương thích schema của Go
Bản Go dùng rất nhiều key kiểu:
- `presence_id`
- `presencePropertyDef_id`
- `targetProfileID`
- `platformUserID`
- `title`

Trong khi C# bind theo tên property C# (PascalCase), nên nhiều request nhận giá trị mặc định (0/rỗng).

**Hệ quả trực tiếp:**
- `/game/relationship/setPresence` có thể nhận sai `PresenceId` (thành 0), làm người chơi bị hiển thị offline.

### 2.2 Endpoint ngoài game routes thiếu/khác chuẩn Go
- C# có `AdditionalRouteRegistrar` nhưng chưa được đăng ký vào pipeline.
- Route text moderation và CDN server status chưa đúng đường dẫn chuẩn mà client Go/game thực tế dùng.

### 2.3 Trả về game context và dữ liệu presence chưa nhất quán
- `RelationshipEndpoints` dùng cứng game `age4` để đọc `presenceData.json`.
- Điều này sai khi chạy `age1/age2/age3/athens`.

### 2.4 Login response thiếu battle server fallback
- Go trả về battle server mặc định localhost khi chưa có server cấu hình.
- C# trả về mảng server rỗng.

---

## 3) Các thay đổi đã thực hiện

### A. Nâng cấp bind request theo chuẩn Go
**File:** `AgeLanServer.Server/Internal/HttpHelpers.cs`

Đã bổ sung:
- `BindAliasAttribute` để map tên field đặc biệt.
- Bind key linh hoạt theo nhiều dạng:
  - `PropertyName`
  - `camelCase`
  - `snake_case`
  - biến thể `Id -> ID`, `Ids -> IDs`
  - alias custom qua attribute
- Convert kiểu an toàn hơn (đặc biệt bool `0/1`, enum, number theo `InvariantCulture`).

### B. Bổ sung alias cho các DTO quan trọng
**File:** `AgeLanServer.Server/Routes/Shared/RouteDtos.cs`

Đã thêm:
- `[BindAlias("presence_id")]` cho `SetPresenceRequest.PresenceId`
- `[BindAlias("presencePropertyDef_id")]` cho `SetPresencePropertyRequest.PresencePropertyId`
- `[BindAlias("title")]` cho `PlatformLoginRequest.GameId`

### C. Sửa logic Relationship theo hướng gần với Go
**File:** `AgeLanServer.Server/Routes/Relationship/RelationshipEndpoints.cs`

Đã sửa:
- Bỏ hardcode game `age4`, đọc game runtime hiện tại.
- `getRelationships` phân nhánh friends/lastConnection theo game (`age3/age4/athens` vs còn lại) tương tự Go.
- `setPresenceProperty` hỗ trợ đúng form id/value (từ Go: `presencePropertyDef_id`, `value`).
- Chuẩn hóa encode profile info cho notify/response.
- `addfriend/ignore/clearRelationship` trả mã theo hành vi Go hiện tại (stub dạng `2`).

### D. Đồng bộ runtime game id để các route dùng chung
**Files:**
- `AgeLanServer.Server/Internal/ServerRuntime.cs`
- `AgeLanServer.Server/LanServer.cs`

Đã thêm `ServerRuntime.CurrentGameId` và set khi server khởi tạo.

### E. Đăng ký các endpoint bổ sung (non-game routes)
**Files:**
- `AgeLanServer.Server/LanServer.cs`
- `AgeLanServer.Server/Routes/AdditionalRouteRegistrar.cs`

Đã đăng ký `AdditionalRouteRegistrar.RegisterEndpoints(_app)` vào pipeline.

### F. Sửa route theo chuẩn Go cho text moderation và CDN status
**Files:**
- `AgeLanServer.Server/Routes/ApiAgeOfEmpires/TextModerationEndpoint.cs`
- `AgeLanServer.Server/Routes/CdnAgeOfEmpires/ServerStatusEndpoint.cs`

Đã map thêm đúng route Go:
- `POST /textmoderation`
- `GET /aoe/rl-server-status.json`
- `GET /aoe/athens-server-status.json`

(giữ route cũ để tương thích ngược).

### G. Sửa fallback game title cho CloudFiles
**File:** `AgeLanServer.Server/Routes/CloudFiles/CloudFilesEndpoint.cs`

- Fallback game title từ runtime hiện tại thay vì hardcode `age4`.

### H. Bổ sung battle server fallback trong login response
**File:** `AgeLanServer.Server/Routes/Login/LoginEndpoints.cs`

- Trả về server localhost mặc định khi login (mô phỏng hành vi Go), tránh mảng server rỗng.

---

## 4) Kết luận về lỗi “game báo Offline”

Các nguyên nhân chính đã được xử lý:
1. **Bind sai `presence_id`** => người chơi bị set presence = 0 (offline) ngoài ý muốn.
2. **Thiếu route/route sai chuẩn Go** ở nhóm endpoint ngoài game route.
3. **Thiếu fallback server trong login response**, làm dữ liệu kết nối không đầy đủ.

Sau chỉnh sửa, flow dữ liệu presence và endpoint compatibility đã sát với bản Go hơn và nhất quán hơn theo runtime game.

---

## 5) Trạng thái các hạng mục “cần làm thêm”

1. ✅ **Chuẩn hóa định danh game (`age*` vs `aoe*`)**
   - Đã bổ sung `GameIds.Normalize(...)` + alias map (hỗ trợ `aoe1/aoe2de/aoe3de/aoe4/aom` -> `age1/age2/age3/age4/athens`).
   - Đã đồng bộ `AppConstants.GameAoE*` về canonical ids từ `GameIds`.
   - Đã áp dụng normalize ở các entry-point quan trọng của Server/Launcher/LauncherConfig.

2. ✅ **Tăng tương thích bind cho payload mảng lồng sâu (`JsonArray<T>`)**
   - `HttpHelpers` đã parse được:
     - `JsonArray<T>`
     - `List<T>`
     - `Dictionary<string,string>`
     - dữ liệu JSON lồng nhau từ `application/x-www-form-urlencoded`.
   - Đã hỗ trợ bind alias và đa dạng key schema gần với Go.

3. ⚠️ **Parity 1:1 response shape toàn bộ route gameplay**
   - Đã ưu tiên xử lý nhóm route ảnh hưởng trực tiếp lỗi Online/Offline (login + relationship + presence).
   - Các route gameplay khác vẫn còn một số endpoint ở mức stub hoặc simplified response.

4. ⚠️ **Kiểm thử tích hợp client thật theo từng game**
   - Chưa thể chạy full test với client game thật trong môi trường CI hiện tại.
   - Đã chuẩn bị checklist kiểm thử thủ công đề xuất (mục 7 bên dưới).

---

## 6) Danh sách file đã chỉnh sửa
- `AgeLanServer.Common/GameIds.cs`
- `AgeLanServer.Common/AppConstants.cs`
- `AgeLanServer.Server/Internal/HttpHelpers.cs`
- `AgeLanServer.Server/Routes/Shared/RouteDtos.cs`
- `AgeLanServer.Server/Routes/Relationship/RelationshipEndpoints.cs`
- `AgeLanServer.Server/Internal/ServerRuntime.cs`
- `AgeLanServer.Server/LanServer.cs`
- `AgeLanServer.Server/Program.cs`
- `AgeLanServer.Server/Routes/ApiAgeOfEmpires/TextModerationEndpoint.cs`
- `AgeLanServer.Server/Routes/CdnAgeOfEmpires/ServerStatusEndpoint.cs`
- `AgeLanServer.Server/Routes/CloudFiles/CloudFilesEndpoint.cs`
- `AgeLanServer.Server/Routes/AdditionalRouteRegistrar.cs`
- `AgeLanServer.Server/Routes/Login/LoginEndpoints.cs`
- `AgeLanServer.Launcher/LauncherCmdRoot.cs`
- `AgeLanServer.Launcher/LauncherGameExecutor.cs`
- `AgeLanServer.Launcher/Program.cs`
- `AgeLanServer.LauncherConfig/CmdSetup.cs`
- `AgeLanServer.LauncherConfig/CmdRevert.cs`
- `AgeLanServer.LauncherConfig/UserDataBackup.cs`
- `AgeLanServer.LauncherConfig/CaCertManager.cs`

---

## 7) Checklist kiểm thử tích hợp đề xuất (client thật)

1. **Login + Presence (mỗi game: age1, age2, age3, age4, athens)**
   - Đăng nhập 2 client
   - client A gọi `setPresence` với `presence_id`
   - client B nhận `PresenceMessage` đúng trạng thái

2. **Relationship API**
   - `getRelationships` trả đúng cấu trúc `friends/lastConnection` theo từng game
   - `addfriend`, `ignore`, `clearRelationship` trả đúng error code stub như Go

3. **External routes parity**
   - `POST /textmoderation`
   - `GET /aoe/rl-server-status.json`
   - `GET /aoe/athens-server-status.json`

4. **Cloudfiles metadata headers**
   - verify headers theo game, đặc biệt case age3/athens skip `x-ms-meta-*`

5. **Login battle server fallback**
   - khi chưa có battle server cấu hình, response vẫn có localhost fallback như Go.

---

## 8) Cập nhật tiếp theo (đợt rà soát bổ sung)

### 8.1 Session/auth context cho game routes đã được nối vào pipeline
- Đã thêm middleware runtime trong `LanServer` để:
  - xác thực `sessionID` cho các `/game/*` route (trừ anonymous paths giống Go)
  - set `HttpContext.Items["SessionId"|"UserId"|"UserName"|"ClientLibVersion"]`
- Đây là điểm quan trọng để `setPresence`, `setPresenceProperty`, `readSession`, `logout` không còn chạy “mất session”.

### 8.2 WebSocket đã nối đúng với message sender dùng bởi routes
- Đã bật `UseWebSockets()` trong pipeline.
- `WebSocketEndpoints` giờ dùng chung `WsMessageSender.HandleConnectionAsync(...)`.
- Tránh tình trạng trước đây có 2 hệ quản lý connection tách biệt làm notify presence không tới client realtime.

### 8.3 Login + Relationship đã được tăng parity gần Go
- `platformlogin`:
  - giữ user identity theo `platformUserID` (không tạo user hoàn toàn mới mỗi lần login cùng tài khoản)
  - response shape bổ sung các trường profile gần hơn với Go
  - nạp login key/value từ `resources/config/{game}/login.json`
  - phát presence notify ngay sau login
- `getRelationships`:
  - dùng profile-info có kèm presence tương thích hơn
  - phân nhánh friends/lastConnection theo game như Go
- `setPresence` và `setPresenceProperty`:
  - cập nhật trạng thái/thuộc tính đúng session
  - broadcast `PresenceMessage` theo flow Go
  - hỗ trợ remove presence property khi value rỗng

### 8.4 Bind JSON đã hỗ trợ alias key như form/query
- `HttpHelpers.BindAsync` (nhánh `application/json`) đã bind theo cùng bộ key linh hoạt:
  - PascalCase / camelCase / snake_case / ID suffix
  - alias qua `[BindAlias(...)]`
- Giảm rủi ro payload JSON dùng key kiểu Go nhưng không map vào DTO C#.

### 8.5 Đồng bộ runtime game id thêm cho nhiều route response loaders
- Các route trước đó hardcode `age4` (achievement/automatch/challenge/item/leaderboard/chat/cloud/community event) đã fallback theo `ServerRuntime.CurrentGameId`.
- Giảm sai lệch dữ liệu file responses/config khi chạy game khác `age4`.
