# Network Testing Scripts - Hướng dẫn sử dụng

## Mục đích

Các scripts này giúp bạn demo khả năng đo lường và giám sát network trong game cho buổi bảo vệ luận văn.

---

## Các Scripts đã tạo

### 1. NetworkStatsDebugUI.cs
**Chức năng:** Hiển thị thông tin network realtime trên màn hình

**Phím tắt:** `F3` để toggle

**Hiển thị:**
- Connection Status (Host/Server/Client)
- Current RTT (Round-Trip Time)
- Average/Min/Max RTT
- Jitter (biến động RTT)
- Connection Quality rating

### 2. NetworkLatencyTester.cs
**Chức năng:** Đo RTT chính xác bằng custom ping/pong RPC

**Cách hoạt động:**
1. Client gửi `PingServerRpc` với timestamp
2. Server trả lời `PongClientRpc` với timestamp gốc
3. Client tính: RTT = (now - originalTimestamp) × 1000ms

**Metrics:**
- CurrentRTT, AverageRTT, MinRTT, MaxRTT
- Jitter (độ biến động)
- Packet Loss %

### 3. NetworkConditionSimulator.cs
**Chức năng:** Giả lập điều kiện mạng khác nhau để demo

**Phím tắt:** `F4` để toggle

**Presets có sẵn:**
| Preset | Latency | Jitter | Packet Loss |
|--------|---------|--------|-------------|
| Perfect | 0ms | 0ms | 0% |
| LAN | 5ms | 2ms | 0% |
| WiFi | 50ms | 10ms | 1% |
| 4G | 80ms | 20ms | 2% |
| 3G | 150ms | 50ms | 5% |
| Bad | 300ms | 100ms | 10% |

### 4. NetworkTestManager.cs
**Chức năng:** Component tổng hợp để dễ setup

---

## Cách Setup trong Unity

### Bước 1: Thêm NetworkTestManager vào Scene

```
Menu: GameObject → Network → Create Network Debug Manager
```

Hoặc thủ công:
1. Tạo Empty GameObject trong scene Game
2. Đặt tên: `NetworkDebugManager`
3. Add Component: `NetworkTestManager`
4. Click "Setup All Components" trong Inspector

### Bước 2: Thêm NetworkLatencyTester vào Player Prefab

1. Mở Player prefab (trong Assets/Data/Prefabs hoặc tương tự)
2. Add Component: `NetworkLatencyTester`
3. Script sẽ tự động đo RTT khi player spawn

### Bước 3: Build và Test

```
1. Build Game: File → Build Settings → Build
2. Chạy 1 instance làm Host
3. Chạy 1 instance làm Client (Join vào Host IP)
4. Nhấn F3 để xem Network Stats
5. Nhấn F4 để giả lập điều kiện mạng khác nhau
```

---

## Demo Script cho Thesis Defense

### Kịch bản Demo 1: Hiển thị Network Stats

```
1. Chạy game với 2-4 người chơi
2. Nhấn F3 → Hiển thị Network Stats UI
3. Chỉ cho giáo sư thấy:
   - RTT đang là bao nhiêu ms
   - Quality rating (⭐ EXCELLENT / ✓ GOOD / etc.)
   - Số clients connected
```

### Kịch bản Demo 2: Giả lập mạng xấu

```
1. Nhấn F4 → Mở Network Simulator
2. Bật "Simulation: ENABLED"
3. Click preset "3G" hoặc "Bad"
4. Quan sát:
   - Game vẫn chơi được (nhờ client prediction)
   - RTT trong F3 tăng lên
   - Có thể thấy nhẹ desync nhưng game xử lý được
5. Click "Perfect" để reset
```

### Kịch bản Demo 3: Stress Test Latency

```
1. Trong F4, kéo slider Latency lên 200ms
2. Quan sát movement của player:
   - Local player vẫn mượt (client prediction)
   - Remote player có delay
3. Giải thích cơ chế:
   - Client-side prediction cho local
   - Interpolation cho remote
   - Server reconciliation khi sai lệch
```

---

## Giải thích cho Giáo sư

### "Làm sao đo độ trễ?"

> "Chúng em sử dụng cơ chế Ping-Pong RPC. Client gửi timestamp lên server, 
> server trả lại ngay, client tính thời gian round-trip. 
> Kết quả hiển thị realtime trên màn hình, bao gồm RTT hiện tại, 
> trung bình, min/max, và jitter."

### "Độ trễ ảnh hưởng thế nào?"

> "Với độ trễ dưới 100ms, gameplay gần như không bị ảnh hưởng nhờ 
> Client-Side Prediction. Player local thấy movement ngay lập tức, 
> server validate sau. Nếu sai lệch quá 0.5m, hệ thống reconcile 
> bằng cách snap về vị trí server."

### "Làm sao xử lý mất gói tin?"

> "Chúng em sử dụng NetworkVariables của Unity NGO với reliable delivery. 
> Các thao tác quan trọng như skill cast, damage đều validate server-side 
> và broadcast lại cho tất cả clients. Nếu client miss update, 
> hệ thống heartbeat sẽ phát hiện và xử lý timeout."

---

## Troubleshooting

### Stats UI không hiển thị
- Kiểm tra `NetworkStatsDebugUI` đã được add vào scene
- Nhấn F3 nhiều lần
- Check Console log xem có error không

### RTT luôn = 0
- Đảm bảo `NetworkLatencyTester` đã add vào Player prefab
- Script chỉ hoạt động khi IsOwner = true
- Check log: "[NetworkLatencyTester] Started for local player"

### Network Simulator không hoạt động
- Unity Transport phải enable Debug Simulator
- Vào NetworkManager → Transport → Debug tab
- Tick "Simulate Network Conditions"
