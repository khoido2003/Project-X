# Câu hỏi Bảo vệ Luận văn Bổ sung - Project-X

## Mục lục
1. [Kiến trúc ECS](#câu-hỏi-về-kiến-trúc-ecs)
2. [Networking & Multiplayer](#câu-hỏi-về-networking--multiplayer)
3. [Hiệu năng & Tối ưu hóa](#câu-hỏi-về-hiệu-năng--tối-ưu-hóa)
4. [Bảo mật & Anti-Cheat](#câu-hỏi-về-bảo-mật--anti-cheat)
5. [State Management](#câu-hỏi-về-state-management)
6. [AI & Pathfinding](#câu-hỏi-về-ai--pathfinding)
7. [Scalability](#câu-hỏi-về-scalability)
8. [Kiểm thử](#câu-hỏi-về-kiểm-thử)

---

## Câu hỏi về Kiến trúc ECS

### Câu 4: Tại sao nhóm chọn ECS custom thay vì Unity DOTS ECS hoặc MonoBehaviour truyền thống?

**Trả lời:**

| Phương pháp | Ưu điểm | Nhược điểm | Phù hợp với Project-X? |
|-------------|---------|------------|------------------------|
| **MonoBehaviour truyền thống** | Dễ tiếp cận, tài liệu phong phú | Coupling cao, khó test, khó scale | ❌ Khó mở rộng cho multiplayer |
| **Unity DOTS ECS** | Hiệu năng cực cao (Burst, Jobs) | Learning curve cao, khó debug, API thay đổi liên tục | ❌ Overhead quá lớn cho game 4 người |
| **Custom ECS** *(chọn cách này)* | Linh hoạt, dễ tích hợp NGO, code dễ đọc | Không có tối ưu Burst/Jobs | ✅ Cân bằng tốt giữa hiệu năng và maintainability |

**Lý do chọn Custom ECS:**

1. **Tách biệt rõ ràng Data/Logic/Presentation:**
```
Component (Data) → System (Logic) → View (Presentation)
     ↓                   ↓                 ↓
HealthDataComponent  DamageSystem    HealthBarView
```

2. **Dễ tích hợp với Unity Netcode (NGO):**
   - NGO sử dụng NetworkBehaviour, không tương thích trực tiếp với DOTS
   - Custom ECS cho phép kết hợp linh hoạt qua `NetworkSyncView`

3. **Dễ thêm tính năng mới:**
   - Thêm Component = Thêm file .cs với data
   - Thêm System = Thêm file .cs với logic
   - Không cần sửa code cũ (Open-Closed Principle)

4. **Query linh hoạt:**
```csharp
// Tìm tất cả entity có 3 component cùng lúc
foreach (var (entity, enemy, trans, movement) in 
    _world.Components.Query<EnemyComponent, TransformComponent, MovementDataComponent>())
{
    // Xử lý logic
}
```

---

### Câu 5: ComponentStore lưu trữ dữ liệu như thế nào? Độ phức tạp của các thao tác?

**Trả lời:**

**Cấu trúc lưu trữ:**
```csharp
// Nested Dictionary: Type → (EntityId → Component)
Dictionary<Type, IDictionary<EntityId, object>> _storage;
```

**Độ phức tạp thao tác:**

| Thao tác | Độ phức tạp | Giải thích |
|----------|-------------|------------|
| `Add<T>(entity, component)` | O(1) amortized | Dictionary insert |
| `TryGet<T>(entity, out comp)` | O(1) | Double dictionary lookup |
| `Has<T>(entity)` | O(1) | Dictionary contains |
| `Remove<T>(entity)` | O(1) | Dictionary remove |
| `Query<T>()` | O(n) | Iterate all entities with component T |
| `Query<T1, T2, T3>()` | O(n) | Iterate smallest dict, check others |
| `RemoveAllComponents(entity)` | O(k) | k = số loại component đã đăng ký |

**Tối ưu trong implementation:**
```csharp
// Pre-allocated capacity để giảm resize
private const int DICTIONARY_CAPACITY = 64;
dict = new Dictionary<EntityId, object>(DICTIONARY_CAPACITY);

// Event để reactive systems biết khi component thay đổi
public event Action<EntityId, Type> OnComponentAdded;
```

---

### Câu 6: Event Bus hoạt động như thế nào? Có vấn đề gì về thứ tự xử lý event không?

**Trả lời:**

**Event Bus Pattern:**
```
Publisher                Event Bus               Subscribers
    │                        │                        │
    ├──Publish(DamageEvent)──►│                        │
    │                        ├──Notify──────────────►HealthSystem
    │                        ├──Notify──────────────►VFXSystem
    │                        └──Notify──────────────►AudioSystem
```

**Flow xử lý một đòn tấn công:**
```
1. AttackSystem           → Publish(DamageEvent)
2.    ↓ DamageSystem      → Tính damage, giảm HP
3.    ↓ DamageSystem      → Publish(HealthChangedEvent)
4.       ↓ HealthSystem   → Kiểm tra chết
5.       ↓ VFXController  → Hiển thị máu
6.       ↓ AudioSystem    → Phát tiếng đau
```

**Vấn đề về thứ tự và giải pháp:**

| Vấn đề | Giải pháp hiện tại |
|--------|-------------------|
| Event xử lý đồng bộ trong cùng frame | Sử dụng System priority để đảm bảo thứ tự |
| Event cascade (A → B → C → A) | Thiết kế để tránh circular dependencies |
| Subscriber bị miss event | Đăng ký trong `Initialize()`, hủy trong `Shutdown()` |

**Ví dụ đăng ký và hủy đăng ký đúng cách:**
```csharp
public class DamageSystem : ISystem
{
    public void Initialize(World world)
    {
        _world = world;
        _world.Events.Subscribe<DamageEvent>(OnDamage);
        _world.Events.Subscribe<ApplyBuffEvent>(OnApplyBuff);
    }

    public void Shutdown()
    {
        _world.Events.Unsubscribe<DamageEvent>(OnDamage);
        _world.Events.Unsubscribe<ApplyBuffEvent>(OnApplyBuff);
    }
}
```

---

## Câu hỏi về Networking & Multiplayer

### Câu 7: Giải thích Client-Side Prediction và Server Reconciliation trong game

**Trả lời:**

**Vấn đề:** Độ trễ mạng khiến player cảm thấy game không responsive (nhấn di chuyển → đợi server → mới thấy di chuyển)

**Giải pháp - Client-Side Prediction:**

```
Timeline (100ms RTT):

CLIENT (Local Player):
t=0ms:    Player nhấn W (forward)
t=0ms:    ──► PREDICT: Di chuyển ngay lập tức (không đợi server)
t=0ms:    ──► Gửi input đến server
t=50ms:   Server nhận input
t=50ms:   Server xử lý input
t=50ms:   Server gửi authoritative state
t=100ms:  Client nhận state
t=100ms:  ──► RECONCILE: So sánh với prediction
          Nếu sai lệch > 0.5m → Sửa lại vị trí
```

**Code implementation trong NetworkSyncView.cs:**

```csharp
// 1. CLIENT PREDICTION - Xử lý input ngay lập tức
private void ClientPredictionUpdate()
{
    var inputService = _world.Services.Resolve<IInputService>();
    Vector2 moveInput = inputService.GetMoveInput();
    
    // Lưu input history để reconcile sau
    var inputState = new ClientInputState
    {
        Tick = _currentTick,
        MoveInput = moveInput,
        MouseWorldPos = inputService.GetMouseWorldPosition(),
    };
    _inputHistory.Enqueue(inputState);
    
    // Gửi input lên server
    SendInputToServerRpc(inputState);
    
    // Cập nhật ECS component để MovementSystem xử lý LOCAL
    if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
    {
        movement.InputDirection = moveInput;  // Prediction xảy ra ở đây!
    }
}

// 2. SERVER RECONCILIATION - Sửa lại nếu sai lệch
[ClientRpc]
private void AcknowledgeInputClientRpc(uint acknowledgedTick)
{
    if (!IsOwner) return;
    
    // Xóa input đã được server xác nhận
    while (_inputHistory.Count > 0 && _inputHistory.Peek().Tick <= acknowledgedTick)
    {
        _inputHistory.Dequeue();
    }
    
    // Kiểm tra sai lệch giữa prediction và server state
    if (_world.Components.TryGet(_entity, out TransformComponent trans))
    {
        float distance = Vector3.Distance(trans.Position, _netTransform.Value.Position);
        
        // Sai lệch lớn → Snap về vị trí server
        if (distance > 0.5f)
        {
            trans.Position = _netTransform.Value.Position;
            trans.Rotation = _netTransform.Value.Rotation;
        }
    }
}
```

**Diagram:**
```
┌─────────────────────────────────────────────────────────────┐
│                    CLIENT (OWNER)                            │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │ Input (WASD) │→ │ Predict Move │→ │ Store in History  │  │
│  └──────────────┘  └──────┬───────┘  └───────────────────┘  │
│                          │                                   │
│              ┌───────────▼───────────┐                      │
│              │ SendInputToServerRpc  │                      │
└──────────────┴───────────┬───────────┴──────────────────────┘
                           │
                           ▼ Network (50-100ms)
┌──────────────────────────┴──────────────────────────────────┐
│                         SERVER                               │
│  ┌──────────────────┐  ┌─────────────────────────────────┐  │
│  │ Receive Input    │→ │ Process with authority          │  │
│  │ Validate Client  │  │ (MovementSystem, DamageSystem)  │  │
│  └──────────────────┘  └────────────────┬────────────────┘  │
│                                         │                    │
│              ┌──────────────────────────▼──────────────┐    │
│              │ Sync NetworkVariables + AcknowledgeRpc  │    │
└──────────────┴──────────────────────────┬──────────────┴────┘
                                          │
                                          ▼ Network (50-100ms)
┌─────────────────────────────────────────┴───────────────────┐
│                    CLIENT (OWNER)                            │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Reconcile: Compare predicted vs server state           │ │
│  │ if (distance > 0.5f) → Snap to server position         │ │
│  │ else → Keep prediction (smooth gameplay)               │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

### Câu 8: Interpolation cho Remote Players hoạt động như thế nào?

**Trả lời:**

**Vấn đề:** Remote players (người chơi khác mà bạn nhìn thấy) chỉ nhận updates mỗi 100ms. Nếu chỉ cập nhật vị trí khi nhận packet → Animation giật, không mượt.

**Giải pháp - Interpolation + Dead Reckoning:**

```csharp
private void ClientInterpolation()
{
    if (IsOwner || IsServer) return;  // Chỉ cho remote players
    
    _interpolationTime += Time.deltaTime;
    float t = _interpolationTime / _interpolationDuration;  // 0 → 1
    
    if (t < 1f)
    {
        // INTERPOLATION: Lerp giữa vị trí cũ và mới
        trans.Position = Vector3.Lerp(_previousPosition, _targetPosition, t);
    }
    else
    {
        // DEAD RECKONING: Dự đoán vị trí tiếp theo dựa trên velocity
        float extrapolationTime = Mathf.Min(
            _interpolationTime - _interpolationDuration, 
            MAX_EXTRAPOLATION_TIME  // Cap at 150ms
        );
        
        // Dampening để tránh overshoot
        float damping = 1f - Mathf.Clamp01(extrapolationTime / MAX_EXTRAPOLATION_TIME);
        trans.Position = _targetPosition + _smoothedVelocity * extrapolationTime * damping;
    }
    
    // Rotation slerp cho mượt
    trans.Rotation = Quaternion.Slerp(_previousRotation, _targetRotation, Mathf.Clamp01(t * 1.2f));
}
```

**Timeline visualization:**
```
Server Tick Rate: ~60Hz (mỗi 16ms)
Client Receive Rate: ~10Hz (mỗi 100ms do network throttle)

Time →
0ms     100ms    200ms    300ms
 │        │        │        │
 ▼        ▼        ▼        ▼
[Pos A]  [Pos B]  [Pos C]  [Pos D]  ← Server gửi

Client renders:
0ms:   Pos A (direct)
50ms:  Lerp(A, B, 0.5)     ← Interpolation
100ms: Pos B (target reached)
150ms: B + velocity * 0.05 ← Dead Reckoning (extrapolation)
200ms: Pos C (new target)
```

---

### Câu 9: Nhóm sync những data nào qua network? Chiến lược sync?

**Trả lời:**

**Bảng chiến lược sync:**

| Data | Sync Method | Frequency | Owner | Notes |
|------|-------------|-----------|-------|-------|
| **Position** | NetworkVariable | 60Hz (throttled khi idle) | Server | Client prediction cho owner |
| **Rotation** | NetworkVariable | 60Hz (throttled > 3° change) | Server | Slerp interpolation |
| **Health** | NetworkVariable | On Change | Server only | Trigger UI update |
| **Combat State** | NetworkVariable | On Change | Server | Idle/Attack/Cast/Stunned |
| **Movement State** | NetworkVariable | ~30Hz | Server | IsMoving, IsGrounded, Direction |
| **Score** | NetworkVariable | ~6Hz | Server | Rarely changes |
| **Input** | ServerRpc | ~30Hz | Client → Server | Batched every 2 ticks |
| **Attack** | ServerRpc + ClientRpc | On Action | Bidirectional | Validate + Broadcast |
| **Skill Cast** | ServerRpc + ClientRpc | On Action | Bidirectional | Cooldown server-side |
| **Animation** | ClientRpc | On Change | Server → All | Trigger/Float params |

**Bandwidth Optimization:**

```csharp
// 1. Throttle transform updates - chỉ sync khi thay đổi đáng kể
if (Vector3.Distance(_netTransform.Value.Position, newState.Position) > 0.05f
    || Quaternion.Angle(_netTransform.Value.Rotation, newState.Rotation) > 3f)
{
    _netTransform.Value = newState;
}

// 2. Input batching - gửi mỗi 2 ticks thay vì mỗi frame
if (_currentTick % 2 == 0)
{
    SendInputToServerRpc(inputState);
}

// 3. Score sync - hiếm khi thay đổi, sync every 10 ticks
if (_currentTick % 10 != 0) return;
```

---

## Câu hỏi về Hiệu năng & Tối ưu hóa

### Câu 10: Nhóm đã tối ưu hóa game ở những điểm nào?

**Trả lời:**

**1. Giảm Physics Queries:**
```csharp
// BAD: Check mỗi frame
Physics.OverlapSphere(pos, radius); // Expensive!

// GOOD: Check mỗi 5 frames, stagger giữa các enemies
if ((Time.frameCount + entity.Id) % 5 == 0)
{
    Physics.OverlapSphereNonAlloc(pos, radius, _overlapBuffer);
}
```

**2. Giảm Network Bandwidth:**
```csharp
// Cache last synced values, chỉ sync khi thay đổi
private Dictionary<string, float> _lastSyncedAnimValues = new();
private NetworkMovementState _lastSyncedMovement;

bool changed = newState.IsMoving != _lastSyncedMovement.IsMoving
    || Vector3.SqrMagnitude(newState.MoveDirection - _lastSyncedMovement.MoveDirection) > 0.01f;

if (changed)
{
    _netMovement.Value = newState;
    _lastSyncedMovement = newState;
}
```

**3. Object Pooling cho A* Pathfinding:**
```csharp
// Reuse priority queue và collections
PriorityQueue<Node> pq = new();
Dictionary<Vector2Int, Node> openSetMap = new();
HashSet<Vector2Int> closeSet = new();
// Không allocate mỗi lần tìm đường
```

**4. Path Smoothing với Line-of-Sight:**
```csharp
// Giảm số waypoints từ 50+ xuống ~5-10
private List<Vector3> SmoothPath(List<Vector3> rawPath)
{
    // Chỉ giữ lại waypoints cần thiết
    // Skip waypoints trong tầm nhìn trực tiếp
}
```

**5. Separation Check Staggering:**
```csharp
// Mỗi enemy check vào frame khác nhau dựa trên entity.Id
if ((Time.frameCount + entity.Id) % 5 == 0)
{
    // Expensive separation behavior
    Vector3 separation = CalculateSeparation();
    // Multiply by 5 to compensate
    trans.Position += separation * 5f;
}
```

**Bảng tổng hợp tối ưu:**

| Khu vực | Trước | Sau | Cải thiện |
|---------|-------|-----|-----------|
| Enemy Physics checks | 60 calls/sec/enemy | 12 calls/sec/enemy | 80% giảm |
| Network sync | Every frame | Every 2 ticks | 50% bandwidth |
| Animation RPCs | Every change | Throttled with cache | ~70% giảm |
| A* pathfinding | Raw path | Smoothed path | 5-10x fewer waypoints |

---

### Câu 11: Game có thể xử lý bao nhiêu entities đồng thời?

**Trả lời:**

**Bottlenecks chính:**

| Component | Limit | Lý do |
|-----------|-------|-------|
| **Players** | 4 | Design choice, NGO overhead |
| **Enemies** | ~50-100 | A* pathfinding, physics queries |
| **Projectiles** | ~100 | NetworkObject spawn/despawn overhead |
| **Buffs/Pickups** | 20-30 | Network sync |

**Phân tích chi tiết:**

1. **ECS Query Performance:**
   - O(n) cho single query, n = số entity có component
   - 100 enemies × 22 systems = 2200 queries/frame
   - Mỗi query ~O(1) lookup → Acceptable

2. **Pathfinding Bottleneck:**
   - A* trên 50×50 grid = 2500 cells
   - Worst case: O(V log V) ≈ 28,000 operations
   - 50 enemies × 0.6s repath = ~83 paths/sec → Acceptable

3. **Network Bottleneck:**
   - 4 players × 10 NetworkVariables = 40 syncs
   - 50 enemies × 5 NetworkVariables = 250 syncs
   - Total: ~300 syncs at 60Hz = 18,000 packets/sec

**Recommendation cho scale lớn hơn:**
- [ ] Implement Interest Management (chỉ sync entities gần player)
- [ ] Batch enemy movement updates
- [ ] Use NetworkList thay vì individual NetworkVariables cho enemies

---

## Câu hỏi về Bảo mật & Anti-Cheat

### Câu 12: Server-Authoritative model bảo vệ game như thế nào?

**Trả lời:**

**Nguyên tắc:** Client chỉ gửi INPUT, Server quyết định RESULT.

```
┌─────────────────────────────────────────────────────────────┐
│                      CHEATER CLIENT                          │
│  Gửi: "Tôi ở vị trí (1000, 0, 1000)"  ❌ Bị bỏ qua!         │
│  Gửi: "Tôi gây 99999 damage"          ❌ Bị bỏ qua!         │
│  Gửi: "Input: WASD, Mouse: (10,5)"    ✅ Server xử lý       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                         SERVER                               │
│  1. Nhận input từ client                                    │
│  2. VALIDATE: Kiểm tra cooldown, range, state...            │
│  3. CALCULATE: Tính damage với stats THẬT từ server         │
│  4. APPLY: Cập nhật HP, position, state                     │
│  5. BROADCAST: Gửi state authoritative cho tất cả clients   │
└─────────────────────────────────────────────────────────────┘
```

**Ví dụ validation trong code:**

```csharp
[ServerRpc]
public void RequestAttackServerRpc(Vector3 mouseWorldPos)
{
    // VALIDATE 1: Entity exists và có đủ components
    if (!_world.Components.TryGet(_entity, out AttackDataComponent attack))
    {
        RejectAttackClientRpc();
        return;
    }
    
    // VALIDATE 2: Check cooldown (server-side timer)
    if (!attack.CanAttack(weapon.BaseCooldown) || attack.IsAttacking)
    {
        RejectAttackClientRpc();
        return;
    }
    
    // VALIDATE 3: Cho phép attack, nhưng dùng SERVER stats
    // Không tin tưởng damage từ client
    _world.Events.Publish(new AttackPressedInputEvent(_entity));
}
```

**System Authority Matrix:**

| System | Chạy trên Server? | Chạy trên Client? | Ai quyết định? |
|--------|------------------|------------------|----------------|
| DamageSystem | ✅ | ❌ | Server only |
| HealthSystem | ✅ | ❌ | Server only |
| SpawnSystem | ✅ | ❌ | Server only |
| MovementSystem | ✅ | ✅ (predict) | Server authority |
| AttackSystem | ✅ | ✅ (animation) | Server authority |
| InputSystem | ❌ | ✅ | Client capture |

**Các loại cheat bị chặn:**

| Cheat Type | Bị chặn? | Cách chặn |
|------------|----------|----------|
| Speed hack | ✅ | Server validate position delta |
| Damage hack | ✅ | Damage calculated server-side |
| Teleport | ✅ | Position set by server only |
| Wallhack | ⚠️ Partial | Enemy positions still synced |
| Aimbot | ❌ | Client-side input, hard to detect |

---

### Câu 13: Input validation được thực hiện như thế nào?

**Trả lời:**

**Các lớp validation:**

```csharp
[ServerRpc]
public void RequestSkillServerRpc(int skillIndex, Vector3 targetPos)
{
    // LAYER 1: Entity validation
    if (!_world.Entities.Exists(_entity))
    {
        Debug.LogWarning("Entity không tồn tại!");
        return;
    }
    
    // LAYER 2: Component validation
    if (!_world.Components.TryGet(_entity, out SkillSetComponent skills))
    {
        Debug.LogWarning("Entity không có skills!");
        return;
    }
    
    // LAYER 3: State validation
    if (_world.Components.TryGet(_entity, out CombatStateComponent state))
    {
        if (state.CurrentState == CombatState.Stunned || 
            state.CurrentState == CombatState.Dead)
        {
            RejectSkillClientRpc();
            return;
        }
    }
    
    // LAYER 4: Cooldown validation (SERVER-SIDE)
    if (skills.IsOnCooldown(skillIndex))
    {
        RejectSkillClientRpc();
        return;
    }
    
    // LAYER 5: Range validation
    if (_world.Components.TryGet(_entity, out TransformComponent trans))
    {
        float distance = Vector3.Distance(trans.Position, targetPos);
        if (distance > skills.GetMaxRange(skillIndex) * 1.1f)  // 10% tolerance
        {
            RejectSkillClientRpc();
            return;
        }
    }
    
    // All validations passed → Execute skill
    _world.Events.Publish(new SkillCastEvent(_entity, skillIndex, targetPos));
}
```

---

## Câu hỏi về State Management

### Câu 14: Combat State Machine hoạt động như thế nào?

**Trả lời:**

**States và Transitions:**

```
                    ┌─────────┐
                    │  IDLE   │◄───────────────┐
                    └────┬────┘                │
                         │                     │
           ┌─────────────┼─────────────┐       │
           │             │             │       │
           ▼             ▼             ▼       │
    ┌──────────┐  ┌──────────┐  ┌──────────┐   │
    │ ATTACKING│  │ CASTING  │  │ STUNNED  │   │
    └────┬─────┘  └────┬─────┘  └────┬─────┘   │
         │             │             │         │
         │ OnComplete  │ OnComplete  │ OnExpire│
         └─────────────┴─────────────┴─────────┘
```

**CombatStateSystem.cs:**

```csharp
public void Update(float dt)
{
    foreach (var (entity, state, attack, movement) in 
        _world.Components.Query<CombatStateComponent, AttackDataComponent, MovementDataComponent>())
    {
        switch (state.CurrentState)
        {
            case CombatState.Idle:
                // Có thể chuyển sang Attack, Cast, hoặc bị Stun
                break;
                
            case CombatState.Attacking:
                // Chờ attack animation hoàn thành
                if (!attack.IsAttacking)
                {
                    TransitionTo(entity, state, CombatState.Idle);
                }
                break;
                
            case CombatState.Casting:
                // Chờ skill cast hoàn thành
                if (!state.IsCasting)
                {
                    TransitionTo(entity, state, CombatState.Idle);
                }
                break;
                
            case CombatState.Stunned:
                // Chờ stun duration hết
                if (!movement.IsStunned)
                {
                    TransitionTo(entity, state, CombatState.Idle);
                }
                break;
        }
    }
}

private void TransitionTo(EntityId entity, CombatStateComponent state, CombatState newState)
{
    CombatState oldState = state.CurrentState;
    state.CurrentState = newState;
    
    // Publish event để các systems khác biết
    _world.Events.Publish(new CombatStateChangedEvent(entity, oldState, newState));
}
```

**State Priority (khi nhiều trigger cùng lúc):**
```
Dead > Stunned > Casting > Attacking > Idle
```

---

### Câu 15: Buff system được implement như thế nào?

**Trả lời:**

**Buff Types hiện tại:**
```csharp
public enum BuffType
{
    DefenseBoost,      // Giảm damage nhận
    DamageBoost,       // Tăng damage gây ra
    AttackSpeedBoost,  // Giảm cooldown
    MovementSlow,      // Giảm speed (debuff)
    HealthRegen,       // Hồi HP theo thời gian
}
```

**Buff Storage trong DamageSystem:**
```csharp
// Dictionary: EntityId → (value, expireTime)
private readonly Dictionary<EntityId, (float value, float expires)> _defenseBuffs = new();
private readonly Dictionary<EntityId, (float value, float expires)> _damageBoostBuffs = new();
private readonly Dictionary<EntityId, (float value, float expires)> _attackSpeedBuffs = new();
```

**Buff Application Flow:**
```
1. Player pickup BuffItem
   ├── BuffPickupComponent.ProcessPickup()
   └── BuffHandlerView.ApplyBuff(buffData)
        └── Publish(ApplyBuffEvent)

2. DamageSystem.OnApplyBuff()
   └── Store buff in dictionary with expire time

3. DamageSystem.OnDamage()
   ├── Check _defenseBuffs → Reduce incoming damage
   └── Check _damageBoostBuffs → Increase outgoing damage

4. DamageSystem.Update()
   └── CleanupExpiredBuffs() → Remove expired entries
```

**Buff Pickup với Networking:**
```csharp
// BuffPickupComponent.cs
private void Update()
{
    Collider[] colliders = Physics.OverlapSphere(transform.position, _pickupRadius);
    foreach (var collider in colliders)
    {
        var playerNetworkObject = collider.GetComponentInParent<NetworkObject>();
        if (playerNetworkObject == null) continue;
        
        // Chỉ owner của player mới có thể trigger pickup
        if (!playerNetworkObject.IsOwner) continue;
        
        if (IsServer)
        {
            ProcessPickup(buffHandler);  // Server xử lý trực tiếp
        }
        else
        {
            RequestPickupServerRpc(playerNetworkObject.OwnerClientId);
        }
    }
}
```

---

## Câu hỏi về AI & Pathfinding

### Câu 16: Enemy AI State Machine hoạt động thế nào?

**Trả lời:**

**Enemy States:**
```
┌─────────────────────────────────────────────────────────────┐
│                     ENEMY AI STATES                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│    ┌──────┐     Detect Player      ┌───────┐                │
│    │ IDLE │ ──────────────────────►│ CHASE │                │
│    └──┬───┘                        └───┬───┘                │
│       │                                │                     │
│       │ Patrol timer                   │ In attack range    │
│       ▼                                ▼                     │
│  ┌────────┐                       ┌────────┐                │
│  │ PATROL │◄─────No target───────│ ATTACK │                │
│  └────────┘                       └────────┘                │
│                                        │                     │
│                                        │ HP low (Boss)       │
│                                        ▼                     │
│                                  ┌───────────┐              │
│                                  │TAKE COVER │              │
│                                  └───────────┘              │
│                                                              │
│    All States ───HP <= 0──────► ┌──────┐                    │
│                                 │ DEAD │                    │
│                                 └──────┘                    │
└─────────────────────────────────────────────────────────────┘
```

**Mỗi State có class riêng:**
```
Assets/Scripts/ECS/AI/
├── EnemyIdleStateAI.cs      → Standing, waiting
├── EnemyPatrolStateAI.cs    → Random movement around spawn
├── EnemyChaseStateAI.cs     → Follow target player
├── EnemyAttackStateAI.cs    → Melee/Ranged attack
├── EnemyTakeCoverStateAI.cs → Boss retreats when low HP
├── EnemyDeadStateAI.cs      → Death animation, cleanup
└── Boss-specific states:
    ├── BossFlamethrowerStateAI.cs
    └── BossJumpAttackStateAI.cs
```

**EnemyAISystem - State Controller:**
```csharp
public void Update(float dt)
{
    foreach (var (entity, enemy) in _world.Components.Query<EnemyComponent>())
    {
        switch (enemy.CurrentState)
        {
            case EnemyState.Idle:
                EnemyIdleStateAI.Execute(_world, entity, enemy, dt);
                break;
            case EnemyState.Patrol:
                EnemyPatrolStateAI.Execute(_world, entity, enemy, dt);
                break;
            case EnemyState.Chase:
                EnemyChaseStateAI.Execute(_world, entity, enemy, dt);
                break;
            case EnemyState.Attack:
                EnemyAttackStateAI.Execute(_world, entity, enemy, dt);
                break;
            // ... more states
        }
    }
}
```

---

### Câu 17: A* pathfinding có những tối ưu gì so với A* cơ bản?

**Trả lời:**

| Tối ưu | A* cơ bản | Project-X Implementation |
|--------|-----------|-------------------------|
| **Heuristic** | Manhattan/Euclidean | Octile Distance (8 directions) |
| **Data Structure** | List | Priority Queue |
| **Path Output** | Raw nodes | Smoothed với Line-of-Sight |
| **Diagonal Movement** | Không/Có | Có + Anti-corner-cutting |
| **Navigation Grid** | Static | Layered (Walkable + TerrainCost) |

**1. Octile Distance Heuristic:**
```csharp
// Chính xác hơn cho 8-direction movement
private float CalcOctileDistance(Vector2Int a, Vector2Int b)
{
    int dx = Mathf.Abs(a.x - b.x);
    int dy = Mathf.Abs(a.y - b.y);
    // D + (D2 - D) * min(dx, dy) where D=1, D2=√2
    return Mathf.Max(dx, dy) + (Mathf.Sqrt(2) - 1) * Mathf.Min(dx, dy);
}
```

**2. Anti-Corner-Cutting:**
```csharp
// Không cho phép đi chéo qua góc obstacle
foreach (Vector2Int node in neighbors)
{
    if (x != 0 && y != 0)  // Diagonal
    {
        Vector2Int adjacent1 = new(position.x + x, position.y);
        Vector2Int adjacent2 = new(position.x, position.y + y);
        
        // Cả 2 ô kề phải walkable mới cho phép diagonal
        if (!IsWalkable(adjacent1) || !IsWalkable(adjacent2))
            continue;
    }
}
```

**3. Path Smoothing:**
```csharp
private List<Vector3> SmoothPath(List<Vector3> rawPath)
{
    // Bỏ qua các waypoints không cần thiết
    // Giữ lại chỉ những điểm có obstacle chắn line-of-sight
    
    // BEFORE: [A]→[B]→[C]→[D]→[E]→[F] (6 points)
    // AFTER:  [A]──────────►[D]→[F]   (3 points)
    
    for (int i = rawPath.Count - 1; i > currentIndex; i--)
    {
        if (HasLineOfSight(rawPath[currentIndex], rawPath[i]))
        {
            // Skip all intermediate points
            nextIndex = i;
            break;
        }
    }
}
```

---

## Câu hỏi về Scalability

### Câu 18: Nếu muốn tăng số người chơi lên 10-20, cần thay đổi gì?

**Trả lời:**

**Hiện tại vs Mục tiêu:**

| Aspect | Hiện tại (4 players) | 10-20 players | Thay đổi cần thiết |
|--------|---------------------|---------------|-------------------|
| Network bandwidth | ~50KB/s per client | ~200KB/s+ | Interest Management |
| Server CPU | 1 thread đủ | Cần optimize | System parallelization |
| Pathfinding | 50 enemies OK | 100+ enemies | Hierarchical/Flow Field |
| Physics | OverlapSphere OK | Bottleneck | Spatial partitioning |

**1. Interest Management (Quan trọng nhất):**
```csharp
// Chỉ sync entities trong bán kính 50 units
public class InterestManagement : NetworkBehaviour
{
    private const float INTEREST_RADIUS = 50f;
    
    public override void OnNetworkSpawn()
    {
        // Chỉ nhận updates từ entities gần mình
        foreach (var entity in AllNetworkEntities)
        {
            float distance = Vector3.Distance(transform.position, entity.Position);
            entity.NetworkObject.SetVisible(OwnerClientId, distance < INTEREST_RADIUS);
        }
    }
}
```

**2. Delta Compression:**
```csharp
// Hiện tại: Gửi full state
_netTransform.Value = new NetworkTransformState
{
    Position = trans.Position,     // 12 bytes
    Rotation = trans.Rotation,     // 16 bytes
    Tick = _currentTick,           // 4 bytes
};  // Total: 32 bytes

// Tối ưu: Gửi delta
_netTransform.Value = new DeltaTransformState
{
    DeltaPosition = trans.Position - lastPosition,  // Quantized: 6 bytes
    DeltaRotation = CompressRotation(deltaRot),     // 2 bytes
};  // Total: 8 bytes (75% reduction)
```

**3. Spatial Partitioning cho Physics:**
```csharp
// Hiện tại: Check tất cả enemies
Physics.OverlapSphereNonAlloc(pos, radius, buffer);

// Tối ưu: Grid-based spatial hash
public class SpatialHash
{
    Dictionary<Vector2Int, List<EntityId>> _cells;
    
    public IEnumerable<EntityId> GetNearby(Vector3 pos, float radius)
    {
        // Chỉ check các cells trong radius
        foreach (var cell in GetOverlappingCells(pos, radius))
        {
            foreach (var entity in _cells[cell])
                yield return entity;
        }
    }
}
```

---

## Câu hỏi về Kiểm thử

### Câu 19: Nhóm test game như thế nào? Có automated tests không?

**Trả lời:**

**Hiện tại: Chủ yếu Manual Testing**

| Test Type | Có? | Mô tả |
|-----------|-----|-------|
| Unit Tests | ❌ | Chưa có |
| Integration Tests | ❌ | Chưa có |
| Play Mode Tests | ⚠️ Manual | Chạy game và kiểm tra |
| Network Tests | ⚠️ Manual | Chạy 2-4 instances |

**Khuyến nghị thêm Unit Tests:**

```csharp
// Tests/ECS/ComponentStoreTests.cs
[TestFixture]
public class ComponentStoreTests
{
    private ComponentStore _store;
    
    [SetUp]
    public void Setup()
    {
        _store = new ComponentStore();
    }
    
    [Test]
    public void Add_And_Get_Component_Returns_Same_Instance()
    {
        var entity = new EntityId(1);
        var health = new HealthDataComponent { MaxHealth = 100 };
        
        _store.Add(entity, health);
        
        Assert.IsTrue(_store.TryGet(entity, out HealthDataComponent result));
        Assert.AreEqual(100, result.MaxHealth);
    }
    
    [Test]
    public void Query_Returns_Only_Entities_With_Component()
    {
        var entity1 = new EntityId(1);
        var entity2 = new EntityId(2);
        
        _store.Add(entity1, new HealthDataComponent());
        _store.Add(entity2, new MovementDataComponent());  // Different type
        
        var results = _store.Query<HealthDataComponent>().ToList();
        
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(entity1, results[0].Key);
    }
}
```

**Khuyến nghị thêm Play Mode Tests:**

```csharp
// Tests/PlayMode/DamageSystemTests.cs
[TestFixture]
public class DamageSystemPlayModeTests
{
    [UnityTest]
    public IEnumerator Damage_Reduces_Health()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.Components.Add(entity, new HealthDataComponent { CurrentHealth = 100, MaxHealth = 100 });
        
        var damageSystem = new DamageSystem();
        damageSystem.Initialize(world);
        
        // Act
        world.Events.Publish(new DamageEvent(entity, default, 30f));
        yield return null;  // Wait one frame
        
        // Assert
        var health = world.Components.Get<HealthDataComponent>(entity);
        Assert.AreEqual(70f, health.CurrentHealth);
    }
}
```

---

### Câu 20: Làm sao debug các vấn đề networking?

**Trả lời:**

**1. Network Debug Logs:**
```csharp
// Đã có sẵn trong code
Debug.Log($"[NetworkSyncView] SendInputToServerRpc received, mousePos: {mouseWorldPos}");
Debug.Log($"[DamageSystem] Applied {(value * 100):F0}% damage boost to {entity.Id}");
```

**2. Unity Editor Tools:**
- Network Profiler (Window → Analysis → Network Profiler)
- Multiplayer Play Mode (test 2-4 clients trong Editor)

**3. Custom Debug UI:**
```csharp
// Thêm overlay hiển thị network stats
void OnGUI()
{
    if (!NetworkManager.Singleton.IsClient) return;
    
    var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
    var rtt = transport.GetCurrentRtt(NetworkManager.Singleton.ServerClientId);
    
    GUI.Label(new Rect(10, 10, 300, 100), 
        $"RTT: {rtt}ms\n" +
        $"Tick: {_currentTick}\n" +
        $"Input Buffer: {_inputHistory.Count}");
}
```

**4. Simulate Bad Network:**
```csharp
// Trong Unity Transport settings
// Delay: 100ms (giả lập latency)
// Jitter: 20ms (biến động)
// Packet Loss: 2% (mất gói tin)
```

---

## Tổng kết

Các câu hỏi trên cover các khía cạnh quan trọng:

1. **Kiến trúc** - Tại sao chọn giải pháp này, tradeoffs
2. **Implementation** - Code hoạt động như thế nào
3. **Tối ưu** - Đã làm gì để cải thiện hiệu năng
4. **Bảo mật** - Chống cheat như thế nào
5. **Scalability** - Có thể mở rộng không
6. **Testing** - Đảm bảo chất lượng như thế nào

Chúc bảo vệ thành công! 🎓
