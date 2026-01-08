# Trả lời Câu hỏi Bảo vệ Luận văn - Project-X

## Câu 1: Một tính năng mới sẽ cần thêm những Component/System/Event nào?

Dựa trên kiến trúc ECS của Project-X, phân tích 3 ví dụ cụ thể:

---

### 1.1 Thêm Enemy/Player mới

| Loại | Tên | Mô tả |
|------|-----|-------|
| **Component** | `<NewEnemy>Component.cs` | Dữ liệu riêng của enemy mới (VD: `GolemComponent` với `RockArmorAmount`, `IsShielded`) |
| **Component** | Sử dụng lại | `EnemyComponent`, `HealthDataComponent`, `MovementDataComponent`, `AttackDataComponent`, `TransformDataComponent` |
| **System** | `<NewEnemy>AISystem.cs` (nếu behavior đặc biệt) | Logic AI riêng (VD: `GolemAISystem` xử lý hành vi đặc biệt như tạo tường đá) |
| **System** | Sử dụng lại | `EnemyAISystem`, `EnemyMovementSystem`, `EnemyVisionSystem`, `DamageSystem` |
| **Event** | `<NewEnemy>SpawnEvent` (optional) | Event spawn với tham số riêng |
| **View** | `<NewEnemy>View.cs` | Kết nối với Unity GameObject, animation |
| **ScriptableObject** | `Assets/SO/Enemies/<NewEnemy>Data.asset` | Cấu hình stats (HP, damage, speed, detection range) |

**Ví dụ thêm Golem Enemy:**
```
Assets/Scripts/
├── ECS/Components/GolemComponent.cs          # [MỚI] Thêm dữ liệu armor/shield
├── ECS/Systems/GolemAISystem.cs              # [MỚI] Logic tạo tường đá, skill đặc biệt
├── ECS/AI/GolemShieldStateAI.cs              # [MỚI] State khi bật shield
├── ECS/Views/GolemView.cs                    # [MỚI] Animation đặc thù
└── SO/Enemies/GolemData.asset                # [MỚI] ScriptableObject cấu hình
```

---

### 1.2 Thêm Vũ khí mới

| Loại | Tên | Mô tả |
|------|-----|-------|
| **Component** | Không cần thêm | Sử dụng `WeaponDataComponent` có sẵn |
| **System** | `<WeaponType>AttackSystem.cs` (nếu behavior khác biệt) | VD: `BowAttackSystem` cho projectile weapons |
| **Event** | `ProjectileEvent` hoặc tương tự | Event khi bắn projectile |
| **View** | `<Weapon>ProjectileView.cs` | Hiển thị đạn, trails |
| **ScriptableObject** | `Assets/SO/Weapons/<WeaponName>.asset` | Cấu hình damage, attack speed, range |

**Ví dụ thêm Bow/Crossbow:**
```
Assets/Scripts/
├── ECS/Systems/RangedAttackSystem.cs         # [MỚI] Logic xử lý projectiles
├── ECS/Events/ProjectileLaunchEvent.cs       # [MỚI] Event khi bắn
├── ECS/Views/ArrowProjectileView.cs          # [MỚI] Hiển thị mũi tên bay
└── SO/Weapons/CrossbowData.asset             # [MỚI] Stats vũ khí
```

---

### 1.3 Thêm Vùng được Buff chỉ số (Buff Zone)

| Loại | Tên | Mô tả |
|------|-----|-------|
| **Component** | `BuffZoneComponent.cs` | Dữ liệu vùng buff: loại buff, cường độ, thời gian |
| **Component** | `ActiveBuffComponent.cs` | Gắn vào player/enemy đang được buff |
| **System** | `BuffZoneSystem.cs` | Phát hiện entities trong vùng, áp dụng/gỡ buff |
| **System** | `BuffSystem.cs` | Xử lý logic buff (tăng/giảm stats theo thời gian) |
| **Event** | `BuffAppliedEvent.cs` | Event khi buff được áp dụng |
| **Event** | `BuffExpiredEvent.cs` | Event khi buff hết hạn |
| **View** | `BuffZoneView.cs` | VFX vùng buff, highlight |

**Ví dụ thêm Healing Zone:**
```
Assets/Scripts/ECS/
├── Components/BuffZoneComponent.cs           # [MỚI]
│   // BuffType (Health, Speed, Damage)
│   // BuffAmount, TickRate, Duration
│   
├── Components/ActiveBuffComponent.cs         # [MỚI]
│   // List<BuffData> ActiveBuffs
│   // Các buff đang áp dụng cho entity
│   
├── Systems/BuffZoneSystem.cs                 # [MỚI]
│   // Query<TransformData, BuffZone>
│   // Phát hiện entities trong radius
│   // Publish BuffAppliedEvent
│   
├── Systems/BuffSystem.cs                     # [MỚI]
│   // Query<ActiveBuff, Health, Movement>
│   // Áp dụng hiệu ứng buff theo thời gian
│   
├── Events/BuffAppliedEvent.cs                # [MỚI]
├── Events/BuffExpiredEvent.cs                # [MỚI]
└── Views/BuffZoneView.cs                     # [MỚI] VFX
```

---

## Câu 2: Trong các game tương tự, người ta xử lý Pathfinding + Obstacle Detection như thế nào?

### Cách Project-X đang xử lý (Custom A*)

Dự án sử dụng **thuật toán A* tự implement** kết hợp với **Grid-based system**:

```
[Enemy cần di chuyển] 
    → [GridSystem: World Position → Grid Position]
    → [A* Pathfinder: Tìm đường đi tối ưu]
    → [Path Smoothing - Line of Sight]
    → [List Waypoints]
    → [EnemyMovementSystem: Di chuyển theo từng waypoint]
```

**Các thành phần chính:**

| File | Chức năng |
|------|-----------|
| `GridSystem.cs` | Chia map thành lưới 2D, đánh dấu ô walkable/obstacle |
| `AStarPathfinder.cs` | Thuật toán A* với Octile distance heuristic |
| `EnemyPathfindingSystem.cs` | Nhận request, gọi pathfinder, trả về đường đi |
| `EnemyMovementSystem.cs` | Di chuyển theo path, tránh va chạm động |

---

### So sánh với các giải pháp phổ biến trong game industry

| Phương pháp | Mô tả | Ưu điểm | Nhược điểm | Games sử dụng |
|-------------|-------|---------|------------|---------------|
| **Unity NavMesh (NavMeshAgent)** | Bake mesh navigation có sẵn của Unity | Dễ dùng, tối ưu cho static environments | Khó với dynamic obstacles, cần rebake | Most Unity games |
| **A* + Grid System** *(Project-X dùng cách này)* | Lưới 2D + A* pathfinding | Linh hoạt, dễ custom, hỗ trợ dynamic obstacles | Tốn memory với bản đồ lớn | RTS games (Starcraft), Roguelikes |
| **NavMesh + Crowd Simulation** | NavMesh + RVO (Reciprocal Velocity Obstacles) | Xử lý tốt đám đông, tránh va chạm | Phức tạp, tốn CPU | Assassin's Creed, Hitman |
| **Flow Field Pathfinding** | Tính trước hướng di chuyển cho mỗi cell | Rất hiệu quả cho nhiều units đến 1 điểm | Không tối ưu cho 1 unit | Supreme Commander, Planetary Annihilation |
| **Hierarchical Pathfinding (HPA*)** | A* đa cấp độ (region → local) | Tối ưu cho bản đồ cực lớn | Phức tạp implement | Open-world games (Skyrim) |

---

### Chi tiết cách Project-X xử lý Obstacle Detection

**1. Static Obstacles (tường, vật cản cố định):**
```csharp
// GridSystem.cs - Khởi tạo grid
private void InitializeGrid()
{
    // Dùng Physics.CheckBox để detect obstacles trong mỗi cell
    bool hasObstacle = Physics.CheckBox(center, halfExtents, 
        Quaternion.identity, obstacleLayer);
    walkable.SetValue(x, y, !hasObstacle);
}
```

**2. Dynamic Obstacles (enemy khác, player):**
```csharp
// EnemyMovementSystem.cs - Tránh va chạm realtime
// SphereCast phát hiện vật cản phía trước
if (Physics.SphereCast(trans.Position, castRadius, forwardDir, 
    out RaycastHit hit, forwardDist, mask))
{
    Vector3 nudge = hit.normal * 0.2f;  // Đẩy sang bên
    trans.Position += nudge;
    RequestRepath(entity, enemy);        // Yêu cầu tìm đường mới
}

// Separation behavior - tránh enemy khác
int cnt = Physics.OverlapSphereNonAlloc(trans.Position, checkRadius, buffer);
// Tính hướng đẩy ra xa các enemy gần
```

**3. Stuck Detection & Recovery:**
```csharp
// Phát hiện kẹt dựa trên tiến trình
if (moved < NO_PROGRESS_THRESHOLD)
{
    enemy.NoProgressTimer += dt;
    if (enemy.NoProgressTimer > NO_PROGRESS_REPATH_TIME)
    {
        ApplyStuckNudge(entity, enemy, trans); // Đẩy ngẫu nhiên
        RequestRepath(entity, enemy);           // Tìm đường mới
    }
}

// Boss stuck quá lâu → teleport
if (boss.ConsecutiveStuckChecks >= 3)
{
    TeleportBossNearTarget(entity, enemy, boss, trans);
}
```

---

### Các game tương tự thường xử lý thế nào

| Game Type | Pathfinding | Obstacle Avoidance |
|-----------|-------------|-------------------|
| **MOBA (LoL, Dota 2)** | NavMesh + A* | RVO crowd simulation |
| **Roguelike (Hades, Dead Cells)** | A* / Dijkstra trên Tile grid | Simple collision + push |
| **Battle Royale (Fortnite)** | NavMesh + octree | Prediction-based avoidance |
| **RTS (Starcraft 2)** | Flow Field + A* hybrid | Formation-based movement |

---

## Câu 3: Nhóm có đo độ trễ khi chơi game không? Độ trễ bị ảnh hưởng ra sao nếu có thêm nhiều người chơi cùng lúc?

### Phân tích từ code hiện tại

Dựa trên kiến trúc networking của Project-X sử dụng **Unity Netcode for GameObjects (NGO)**:

**Hiện tại:**
- Chưa có code đo latency explicitly trong dự án
- Sử dụng mô hình **Server-Authoritative** giúp giảm cheating nhưng tăng perceived latency

---

### Cách đo độ trễ (Latency Measurement)

Để đo latency trong Unity NGO, có thể implement:

```csharp
// Ví dụ code đo Round-Trip Time (RTT)
public class LatencyMeasurement : NetworkBehaviour
{
    private float _lastPingTime;
    public NetworkVariable<float> CurrentRTT = new();
    
    [ServerRpc]
    private void PingServerRpc(float clientSendTime)
    {
        // Server nhận và trả lời ngay
        PongClientRpc(clientSendTime);
    }
    
    [ClientRpc]
    private void PongClientRpc(float originalSendTime)
    {
        if (!IsOwner) return;
        float rtt = Time.time - originalSendTime;
        CurrentRTT.Value = rtt * 1000f; // Convert to ms
    }
    
    void Update()
    {
        if (IsOwner && Time.time - _lastPingTime > 1f) // Ping mỗi giây
        {
            _lastPingTime = Time.time;
            PingServerRpc(Time.time);
        }
    }
}
```

---

### Độ trễ bị ảnh hưởng thế nào khi thêm người chơi?

```
┌─────────────────────────────────┐     ┌─────────────────────────────────┐
│         1-2 Players             │     │         3-4 Players             │
├─────────────────────────────────┤     ├─────────────────────────────────┤
│  Client ──Input──► Server       │     │  Client1 ──Input──► Server      │
│  Client ◄──State── Server       │     │  Client2 ──Input──► Server      │
│                                 │     │  Client3 ──Input──► Server      │
│                                 │     │  Server ──State x3──► All       │
└─────────────────────────────────┘     └─────────────────────────────────┘
```

| Yếu tố | 1-2 Players | 3-4 Players | Lý do |
|--------|-------------|-------------|-------|
| **Bandwidth per client** | Thấp | Tăng tuyến tính | Mỗi client phải nhận state của tất cả players khác |
| **Server CPU** | Thấp | Tăng đáng kể | Server phải xử lý: `n * ECS systems` + `n² * collision checks` |
| **Latency (RTT)** | 50-100ms (LAN) | 100-200ms+ | Tăng do queue processing time |
| **Jitter** | Ổn định | Biến động | Packet scheduling phức tạp hơn |

---

### Các yếu tố ảnh hưởng latency trong Project-X

| Yếu tố | Mô tả | Ảnh hưởng |
|--------|-------|-----------|
| **Network Variable Sync** | Mỗi player có ~10 NetworkVariables (Position, Health, State...) | +5-10ms per player per sync |
| **RPC calls** | Input RPCs, Skill RPCs, Animation RPCs | Tăng theo số người chơi |
| **ECS System Updates** | Server chạy 22+ systems mỗi frame | CPU bottleneck khi nhiều entities |
| **Enemy AI + Pathfinding** | Mỗi enemy tính A* path riêng | O(n log n) per enemy request |
| **Collision Detection** | Physics queries tăng quadratic | `OverlapSphereNonAlloc` cho mỗi enemy |

---

### Khuyến nghị để đo và tối ưu

**1. Thêm Latency Monitoring:**
```csharp
// Hiển thị RTT on-screen cho debug
public class NetworkStatsUI : MonoBehaviour
{
    void OnGUI()
    {
        if (NetworkManager.Singleton.IsClient)
        {
            var transport = NetworkManager.Singleton.NetworkConfig
                .NetworkTransport as UnityTransport;
            var rtt = transport.GetCurrentRtt(
                NetworkManager.Singleton.ServerClientId);
            GUI.Label(new Rect(10, 10, 200, 20), $"RTT: {rtt}ms");
        }
    }
}
```

**2. Các kỹ thuật giảm latency đã có trong code:**
- ✅ **Client-side Prediction**: Player thấy movement ngay lập tức
- ✅ **Input buffering**: `SkillCastBufferComponent` lưu input
- ✅ **Interpolation**: Smooth rendering cho remote players

**3. Cải tiến có thể thêm:**
- 🔲 **Delta compression**: Chỉ gửi thay đổi, không gửi full state
- 🔲 **Interest Management**: Chỉ sync entities trong view range
- 🔲 **Tick Rate adjustment**: Giảm sync frequency khi nhiều players

---

### Kết luận về Latency

| Số người chơi | Expected Latency (LAN) | Expected Latency (Internet) | Gameplay Impact |
|---------------|------------------------|-----------------------------|-----------------|
| 1-2 | 20-50ms | 50-100ms | Không cảm nhận |
| 3-4 | 50-80ms | 100-200ms | Chấp nhận được |
| 5+ | 100ms+ | 200ms+ | Cần tối ưu thêm |

> **Ghi chú:** Project-X được thiết kế cho tối đa 4 người chơi, nên latency ở mức chấp nhận được với kiến trúc hiện tại. Nếu muốn mở rộng, cần thêm **Interest Management** và **State Delta Compression**.
