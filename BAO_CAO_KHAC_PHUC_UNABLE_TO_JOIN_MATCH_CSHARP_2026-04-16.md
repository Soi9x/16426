# Báo cáo điều tra & khắc phục lỗi `Unable to join match` (C#)

## 1) Hiện tượng
- Client AoE4 hiển thị lỗi:
  - `Unable to join match`
  - Error code: `C04T01R04X-01 4D504D610B3C`
- Bản Go join match bình thường trong cùng môi trường.

## 2) Kết luận nguyên nhân chính
Qua đối chiếu flow `advertisement` giữa Go và C#, phát hiện các lệch parity ảnh hưởng trực tiếp tới join:

1. **Sai payload peer encode**
   - C# đang trả `party = -1` cố định trong `EncodePeer(...)`.
   - Go trả **party thực** của peer.
   - Sai lệch này làm metadata lobby/match không nhất quán ở phía client.

2. **Battle server metadata trả về thiếu động theo runtime**
   - C# hardcode IP/port battle server trong login/advertisement.
   - Không tận dụng cấu hình battle server runtime (temp configs do launcher/battle-server-manager tạo).
   - Dễ dẫn tới client nhận endpoint không đúng so với môi trường thực.

3. **Parity tìm lobby còn thiếu**
   - `findAdvertisements` chưa lọc theo tag đầy đủ, chưa loại lobby mà user đã là peer.
   - Có thể hiển thị lobby không phù hợp và gây lỗi khi join.

## 3) Các chỉnh sửa đã triển khai

### A. Sửa `peer encode` đúng chuẩn Go
- File: `AgeLanServer.Server/Routes/Advertisement/AdvertisementEndpoints.cs`
- `EncodePeer(...)` đã trả `peer.Party` thay vì `-1`.

### B. Bổ sung runtime resolver cho battle servers
- File mới: `AgeLanServer.Server/Internal/BattleServerRuntime.cs`
- Thêm các chức năng:
  - đọc battle server configs runtime theo game (`BattleServerConfigManager.LoadConfigs`)
  - resolve IP `auto` theo context request
  - encode danh sách battle servers cho login response
  - fallback an toàn khi chưa có config

### C. Login trả battle server list theo runtime thay vì hardcode
- File: `AgeLanServer.Server/Routes/Login/LoginEndpoints.cs`
- `BuildBattleServersResponse(...)` chuyển sang dùng `BattleServerRuntime.EncodeLoginServers(...)`.

### D. Siết parity cho advertisement host/join
- File: `AgeLanServer.Server/Routes/Advertisement/AdvertisementEndpoints.cs`
- Đồng bộ theo hướng Go:
  - host yêu cầu `id = -1`
  - non-AoE4: tự rời match cũ trước khi host/join
  - join kiểm tra `party` tương thích
  - host lưu metadata battle server theo relay region/runtime
  - host non-LAN sinh `XboxSessionId` theo pattern Go

### E. Cải thiện find/start observing
- File: `AgeLanServer.Server/Routes/Advertisement/AdvertisementEndpoints.cs`
- `findAdvertisements` thêm:
  - lọc theo tag
  - loại match đã là peer
  - kiểm tra relay/battle server hợp lệ hơn
- `startObserving` trả payload giàu thông tin hơn theo format gần Go (ip, ports, user lists, startTime).

## 4) Kỳ vọng sau fix
- Client nhận đúng battle server info theo môi trường runtime.
- Metadata peer/party nhất quán hơn với Go.
- Giảm mạnh tình trạng join vào lobby nhưng thất bại handshake/validation ở bước vào match.

## 5) Checklist kiểm thử đề xuất
1. Login 2 client cùng game `age4`.
2. Host lobby từ client A.
3. Client B `findAdvertisements` thấy lobby A.
4. Client B join lobby A (không còn pop-up `Unable to join match`).
5. Host start match, xác nhận cả 2 client vào game.
6. Lặp lại với:
   - relay region LAN UUID
   - relay region có battle-server-manager runtime config
   - khác input tags (KBM/controller) để xác nhận lọc lobby.
