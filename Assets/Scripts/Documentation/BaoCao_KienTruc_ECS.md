# CHƯƠNG: KIẾN TRÚC ENTITY-COMPONENT-SYSTEM (ECS) TRONG PHÁT TRIỂN GAME

---

## 1. TỔNG QUAN VỀ KIẾN TRÚC ECS

### 1.1. Khái niệm Entity-Component-System

Entity-Component-System (ECS) là một mẫu kiến trúc phần mềm được sử dụng phổ biến trong phát triển trò chơi điện tử (video game development). Kiến trúc này được thiết kế để giải quyết các vấn đề về hiệu suất, khả năng mở rộng và bảo trì mã nguồn trong các dự án game có quy mô lớn.

ECS tuân theo nguyên tắc **"Composition over Inheritance"** (Ưu tiên Composition hơn Kế thừa), một nguyên lý thiết kế phần mềm được đề xuất trong cuốn sách "Design Patterns: Elements of Reusable Object-Oriented Software" của Gang of Four. Thay vì sử dụng cây kế thừa phức tạp như trong lập trình hướng đối tượng (OOP) truyền thống, ECS xây dựng các đối tượng game bằng cách kết hợp các thành phần dữ liệu nhỏ, độc lập với nhau.

Kiến trúc ECS bao gồm ba thành phần cốt lõi:

**1.1.1. Entity (Thực thể)**

Entity là một định danh duy nhất (unique identifier) đại diện cho một đối tượng trong game. Điểm quan trọng cần lưu ý là Entity không chứa bất kỳ dữ liệu hay logic xử lý nào. Entity đơn giản chỉ là một "thẻ nhận dạng" (identity tag) được sử dụng để liên kết các Component với nhau.

Trong triển khai thực tế, Entity thường được biểu diễn dưới dạng một số nguyên (integer) hoặc một cấu trúc dữ liệu nhẹ (lightweight struct) chứa ID. Việc sử dụng kiểu dữ liệu đơn giản này giúp tối ưu hóa bộ nhớ và tốc độ xử lý.

**1.1.2. Component (Thành phần)**

Component là các container dữ liệu thuần túy (Plain Old Data - POD), không chứa bất kỳ logic xử lý nào. Mỗi Component đại diện cho một khía cạnh hoặc thuộc tính cụ thể của Entity. Ví dụ:

- **TransformComponent**: Lưu trữ vị trí (position), góc quay (rotation) và tỷ lệ (scale)
- **HealthComponent**: Lưu trữ điểm máu hiện tại và điểm máu tối đa
- **MovementComponent**: Lưu trữ tốc độ di chuyển, hướng di chuyển
- **WeaponComponent**: Lưu trữ thông tin về vũ khí đang trang bị

Một Entity có thể có nhiều Component khác nhau, và các Component này xác định "là gì" và "có gì" của Entity đó. Ví dụ, một Entity đại diện cho nhân vật người chơi (Player) có thể bao gồm: TransformComponent + HealthComponent + MovementComponent + PlayerTagComponent + AttackComponent.

**1.1.3. System (Hệ thống)**

System là nơi chứa toàn bộ logic xử lý của game. Mỗi System chịu trách nhiệm xử lý một khía cạnh cụ thể của gameplay và hoạt động trên các Entity có chứa các Component mà System đó quan tâm.

Ví dụ:
- **MovementSystem**: Xử lý logic di chuyển cho tất cả Entity có MovementComponent và TransformComponent
- **HealthSystem**: Xử lý logic liên quan đến máu (nhận damage, hồi máu, chết) cho Entity có HealthComponent
- **AttackSystem**: Xử lý logic tấn công cho Entity có AttackComponent

Systems hoạt động theo mô hình "query-based": chúng truy vấn (query) tất cả Entity có các Component cần thiết, sau đó xử lý từng Entity trong vòng lặp. Mô hình này cho phép xử lý hàng loạt (batch processing) hiệu quả.

### 1.2. Lịch sử phát triển của ECS

Kiến trúc ECS không phải là một phát minh mới mà đã được phát triển và hoàn thiện qua nhiều năm trong ngành công nghiệp game. Một số mốc quan trọng:

- **1998**: Thymoma Dungeon Siege sử dụng kiến trúc dựa trên component
- **2002**: Game Dungeon Siege chính thức áp dụng component-based architecture
- **2007**: Bài viết "Evolve Your Hierarchy" của Mick West giới thiệu khái niệm composition over inheritance trong game development
- **2017**: Unity Technologies công bố Unity DOTS (Data-Oriented Technology Stack) với ECS là trung tâm
- **2018-2023**: ECS trở nên phổ biến với sự hỗ trợ chính thức từ các game engine lớn

---

## 2. LÝ DO SỬ DỤNG ECS TRONG LẬP TRÌNH GAME

### 2.1. Hiệu suất cao (High Performance)

#### 2.1.1. Tối ưu hóa bộ nhớ Cache (Cache-Friendly Memory Layout)

Một trong những lợi ích quan trọng nhất của ECS là cách tổ chức dữ liệu trong bộ nhớ. ECS sử dụng mô hình **Structure of Arrays (SoA)** thay vì **Array of Structures (AoS)** như trong OOP truyền thống.

**Mô hình Array of Structures (AoS) - OOP truyền thống:**
```
Bộ nhớ: [Entity1: {pos, health, speed}] [Entity2: {pos, health, speed}] [Entity3: {pos, health, speed}]...
```

Trong mô hình này, tất cả dữ liệu của một đối tượng được lưu trữ liên tục. Khi một System chỉ cần xử lý vị trí (position), nó phải load toàn bộ dữ liệu của Entity vào cache, dẫn đến lãng phí bandwidth bộ nhớ.

**Mô hình Structure of Arrays (SoA) - ECS:**
```
Positions:  [pos1,    pos2,    pos3,    ...]
Healths:    [health1, health2, health3, ...]
Speeds:     [speed1,  speed2,  speed3,  ...]
```

Trong mô hình SoA, dữ liệu cùng loại được lưu trữ liên tục. Khi MovementSystem xử lý vị trí, nó chỉ load mảng Positions vào cache. Dữ liệu nằm liên tục trong bộ nhớ giúp CPU cache có thể prefetch hiệu quả, giảm cache misses đáng kể.

Theo nghiên cứu, sự khác biệt về hiệu suất có thể lên đến 10-100 lần khi xử lý số lượng lớn entities (hàng nghìn đến hàng trăm nghìn).

#### 2.1.2. Xử lý theo lô (Batch Processing)

ECS cho phép Systems xử lý nhiều Entity cùng lúc trong một vòng lặp đơn giản:

```csharp
foreach (var (entity, movement) in _world.Components.Query<MovementDataComponent>())
{
    // Xử lý tất cả entities có MovementDataComponent
    movement.Position += movement.Velocity * deltaTime;
}
```

Mô hình này có nhiều lợi ích:
- **Giảm overhead gọi hàm**: Không cần gọi Update() cho từng đối tượng
- **Tối ưu hóa vòng lặp**: Compiler có thể tối ưu hóa vòng lặp đơn giản hiệu quả hơn
- **Khả năng song song hóa**: Dễ dàng chia việc xử lý cho nhiều CPU cores

#### 2.1.3. Hỗ trợ Multi-threading

ECS được thiết kế với multi-threading trong tâm trí. Vì Systems hoạt động độc lập và Components là dữ liệu thuần túy, việc chạy song song nhiều Systems trên các CPU cores khác nhau trở nên đơn giản hơn nhiều so với OOP.

### 2.2. Khả năng mở rộng (Scalability)

#### 2.2.1. Thêm tính năng mới dễ dàng

Trong ECS, việc thêm tính năng mới chỉ đơn giản là:
1. Tạo Component mới để lưu trữ dữ liệu cần thiết
2. Tạo System mới để xử lý logic
3. Gắn Component vào Entity cần tính năng đó

Không cần sửa đổi code hiện có, không có nguy cơ phá vỡ các tính năng đang hoạt động.

**Ví dụ thực tế**: Cần thêm tính năng "bay" cho một số nhân vật:
- Tạo `FlyingComponent` chứa: flySpeed, maxAltitude, isFlying
- Tạo `FlyingSystem` xử lý logic bay
- Gắn FlyingComponent vào các Entity có thể bay

Các Entity không có FlyingComponent sẽ không bị ảnh hưởng gì.

#### 2.2.2. Tái sử dụng code hiệu quả

Một Component có thể được sử dụng bởi nhiều Systems khác nhau. Ví dụ:
- HealthComponent được sử dụng bởi: DamageSystem, HealingSystem, HealthRegenSystem, DeathSystem
- TransformComponent được sử dụng bởi: MovementSystem, RenderSystem, CollisionSystem, CameraSystem

### 2.3. Tách biệt concerns (Separation of Concerns)

ECS thực hiện nguyên tắc Single Responsibility một cách tự nhiên:
- **Components**: Chỉ chịu trách nhiệm lưu trữ dữ liệu
- **Systems**: Chỉ chịu trách nhiệm xử lý logic một khía cạnh cụ thể
- **Views** (trong implementation của project): Chỉ chịu trách nhiệm hiển thị trực quan

Sự tách biệt này giúp:
- Code dễ đọc và hiểu hơn
- Dễ dàng phân công công việc trong team
- Giảm thiểu xung đột khi merge code

### 2.4. Dễ dàng debug và testing

#### 2.4.1. Components dễ serialize và inspect

Vì Components chỉ chứa dữ liệu thuần túy, chúng có thể:
- Được serialize/deserialize dễ dàng (lưu/load game state)
- Được hiển thị trong debug inspector
- Được so sánh giữa các frame để phát hiện bugs

#### 2.4.2. Systems có thể test độc lập

Mỗi System có thể được unit test một cách độc lập:
```csharp
[Test]
public void MovementSystem_AppliesVelocity_ToPosition()
{
    var world = new World();
    var entity = world.CreateEntity();
    world.Components.Add(entity, new MovementComponent { Velocity = new Vector3(1, 0, 0) });
    world.Components.Add(entity, new TransformComponent { Position = Vector3.zero });
    
    var movementSystem = new MovementSystem();
    movementSystem.Initialize(world);
    movementSystem.Update(1.0f); // 1 second
    
    var transform = world.Components.Get<TransformComponent>(entity);
    Assert.AreEqual(new Vector3(1, 0, 0), transform.Position);
}
```

#### 2.4.3. Game state có thể reproduce

Vì toàn bộ game state được lưu trong Components, game có thể:
- Lưu snapshot của tất cả Components tại bất kỳ thời điểm nào
- Replay lại game từ snapshot để reproduce bugs
- So sánh state giữa client và server trong multiplayer

### 2.5. Hỗ trợ Multiplayer tốt

ECS có nhiều đặc điểm phù hợp với game multiplayer:

#### 2.5.1. Deterministic execution

Vì Systems chạy theo thứ tự cố định và hoạt động trên cùng dữ liệu, kết quả xử lý sẽ consistent (nhất quán) giữa server và clients.

#### 2.5.2. State synchronization

Thay vì đồng bộ toàn bộ đối tượng, chỉ cần đồng bộ các Components cần thiết. Điều này giảm đáng kể lượng dữ liệu cần truyền qua mạng.

#### 2.5.3. Server authority

Dễ dàng tách biệt Systems chạy trên server (gameplay logic) và Systems chạy trên client (visual/audio).

---

## 3. SO SÁNH ECS VỚI CÁC KIẾN TRÚC KHÁC

### 3.1. So sánh với OOP truyền thống

#### 3.1.1. Vấn đề Diamond Problem

Trong OOP với đa kế thừa, Diamond Problem xảy ra khi một class kế thừa từ hai class cha mà cả hai đều kế thừa từ một class chung.

```
Ví dụ Diamond Problem:
          Character
           /     \
     FlyingUnit  SwimmingUnit
           \     /
       FlyingSwimmingUnit (Conflict!)
```

Khi cần tạo một nhân vật vừa bay vừa bơi được, OOP gặp nhiều vấn đề:
- Trùng lặp code từ class Character
- Không rõ nên sử dụng method nào khi có conflict
- Code trở nên phức tạp và khó bảo trì

**Giải quyết với ECS:**
```
Entity + FlyingComponent + SwimmingComponent + CharacterComponent
```

Không có inheritance, không có conflict. Entity đơn giản chỉ có tất cả Components cần thiết.

#### 3.1.2. Bảng so sánh chi tiết

| Tiêu chí | OOP Truyền Thống | ECS |
|----------|------------------|-----|
| **Cấu trúc** | Cây kế thừa (Inheritance Hierarchy) | Composition of Components |
| **Coupling** | Cao - logic và data nằm cùng class | Thấp - data và logic tách biệt hoàn toàn |
| **Hiệu suất bộ nhớ** | Scattered memory access, nhiều cache misses | Cache-friendly, batch processing |
| **Mở rộng** | Diamond problem, deep hierarchies gây khó khăn | Thêm component/system mới không ảnh hưởng code cũ |
| **Testing** | Khó mock dependencies, cần integration tests | Dễ unit test từng system độc lập |
| **Code reuse** | Thông qua inheritance hoặc abstract classes | Thông qua reusable components |
| **Flexibility** | Thay đổi behavior cần thay đổi class | Thay đổi behavior bằng thêm/bớt components runtime |

### 3.2. So sánh với Unity MonoBehaviour (Component-Based Architecture)

Unity MonoBehaviour cũng sử dụng khái niệm Component, nhưng có sự khác biệt quan trọng với Pure ECS:

| Tiêu chí | Unity MonoBehaviour | Pure ECS |
|----------|---------------------|----------|
| **Component chứa** | Dữ liệu + Logic + Unity lifecycle (Start, Update, etc.) | Chỉ dữ liệu thuần túy (POD) |
| **Update loop** | Update() gọi trên từng MonoBehaviour riêng biệt | System xử lý batch toàn bộ entities |
| **Dependencies** | GetComponent<T>() runtime lookup - chậm | Query trong ComponentStore - tối ưu |
| **Memory layout** | Scattered across GameObjects | Grouped by component type |
| **Performance** | Overhead từ Unity lifecycle | Tối ưu cho batch processing |

### 3.3. So sánh với MVC/MVP Patterns

| Tiêu chí | MVC/MVP | ECS |
|----------|---------|-----|
| **Use case chính** | UI applications | Game/simulation |
| **Data flow** | Controller/Presenter làm trung gian | Direct component access qua Query |
| **Scalability** | Tốt cho UI với vài chục views | Xuất sắc cho game với hàng nghìn entities |
| **Update model** | Event-driven | Frame-based update loop |
| **State management** | Thường qua View binding | Centralized trong ComponentStore |

---

## 4. KIẾN TRÚC ECS TÙY CHỈNH TRONG PROJECT

### 4.1. Tổng quan kiến trúc

Project sử dụng kiến trúc ECS tùy chỉnh được xây dựng trên nền tảng Unity. Kiến trúc này bao gồm các thành phần chính sau:

```
┌─────────────────────────────────────────────────────────────────┐
│                        WORLD (Container chính)                   │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐        │
│  │ EntityManager │  │ComponentStore │  │ SystemManager │        │
│  │ - CreateEntity│  │ - Add<T>()    │  │ - AddSystem() │        │
│  │ - DestroyEntity│ │ - Get<T>()   │  │ - UpdateAll() │        │
│  │ - ID Recycling│  │ - Query<T>() │  │ - FixedUpdate │        │
│  └───────────────┘  └───────────────┘  └───────────────┘        │
│  ┌───────────────┐  ┌───────────────┐                           │
│  │   EventBus    │  │ServiceLocator │                           │
│  │ - Subscribe() │  │ - Register()  │                           │
│  │ - Publish()   │  │ - Resolve()   │                           │
│  └───────────────┘  └───────────────┘                           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              WORLDRUNNER (Unity MonoBehaviour Integration)       │
│  - Awake(): Khởi tạo World, Services, Systems                   │
│  - Update(): Gọi Systems.UpdateAll(deltaTime)                    │
│  - FixedUpdate(): Gọi Systems.FixedUpdateAll(fixedDeltaTime)    │
│  - OnNetworkSpawn(): Khởi tạo Server-only Systems               │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2. Chi tiết các Core Classes

#### 4.2.1. World - Container chính

World là class trung tâm, đóng vai trò là container cho tất cả các subsystem của ECS.

**Cấu trúc:**
```csharp
public class World
{
    private static World _instance;
    public static World Instance { get; }
    
    public readonly ComponentStore Components = new();
    public readonly EventBus Events = new();
    public readonly ServiceLocator Services = new();
    public readonly EntityManager Entities = new();
    public readonly SystemManager Systems = new();
    
    public EntityId CreateEntity() => Entities.CreateEntity();
    
    public void DestroyEntity(EntityId id)
    {
        Components.RemoveAllComponents(id);
        Entities.DestroyEntity(id);
        Events.Publish(new EntityDestroyedEvent(id));
    }
}
```

**Chức năng chính:**
1. **Singleton Pattern**: Đảm bảo chỉ có một World instance trong game
2. **Facade Pattern**: Cung cấp API đơn giản để tương tác với các subsystem
3. **Entity Lifecycle Management**: Quản lý tạo và hủy entities

**Quy trình tạo Entity:**
1. EntityManager tạo EntityId mới (hoặc tái sử dụng ID cũ)
2. Components được thêm vào ComponentStore với EntityId đó
3. Views được bind để hiển thị Entity

**Quy trình hủy Entity:**
1. Tất cả Components của Entity được xóa khỏi ComponentStore
2. EntityId được đánh dấu là "free" để tái sử dụng
3. Event EntityDestroyedEvent được publish để thông báo các Systems

#### 4.2.2. EntityId - Lightweight Identifier

**Cấu trúc:**
```csharp
[Serializable]
public struct EntityId : IEquatable<EntityId>
{
    public readonly int Id;
    
    public EntityId(int id) => Id = id;
    
    public bool Equals(EntityId other) => Id == other.Id;
    public override int GetHashCode() => Id;
    
    public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
    public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
}
```

**Đặc điểm thiết kế:**
1. **Value Type (struct)**: Tránh GC allocation, copy by value
2. **IEquatable<T>**: Cho phép Dictionary lookup hiệu quả
3. **Operator Overloading**: Cho phép so sánh bằng == và !=
4. **Lightweight**: Chỉ chứa một integer, tối ưu bộ nhớ

#### 4.2.3. EntityManager - Quản lý Entity Lifecycle

**Cấu trúc:**
```csharp
public class EntityManager
{
    private int _nextId = 1;
    private readonly HashSet<EntityId> _live = new();
    private readonly Queue<int> _freeIds = new();
    
    public EntityId CreateEntity()
    {
        int idValue;
        if (_freeIds.Count > 0)
        {
            idValue = _freeIds.Dequeue();  // Tái sử dụng ID cũ
        }
        else
        {
            idValue = _nextId++;  // Tạo ID mới
        }
        var id = new EntityId(idValue);
        _live.Add(id);
        return id;
    }
    
    public bool DestroyEntity(EntityId id)
    {
        if (_live.Remove(id))
        {
            _freeIds.Enqueue(id.Id);  // Đưa ID vào queue để tái sử dụng
            return true;
        }
        return false;
    }
    
    public bool Exists(EntityId id) => _live.Contains(id);
}
```

**Đặc điểm thiết kế:**
1. **ID Recycling**: IDs được tái sử dụng sau khi entity bị hủy, tránh integer overflow
2. **O(1) Operations**: HashSet cho phép lookup nhanh
3. **Memory Efficient**: Queue lưu trữ IDs chờ tái sử dụng

#### 4.2.4. ComponentStore - Lưu trữ và Query Components

ComponentStore là class quan trọng nhất trong ECS, chịu trách nhiệm lưu trữ và truy xuất Components.

**Cấu trúc lưu trữ:**
```csharp
public class ComponentStore
{
    private readonly Dictionary<Type, IDictionary<EntityId, object>> _storage = new();
    
    public void Add<T>(EntityId entity, T component) where T : class
    {
        Type type = typeof(T);
        if (!_storage.TryGetValue(type, out var dict))
        {
            dict = new Dictionary<EntityId, object>(64);  // Pre-allocated capacity
            _storage[type] = dict;
        }
        dict[entity] = component;
        OnComponentAdded?.Invoke(entity, type);
    }
    
    public bool TryGet<T>(EntityId entity, out T component) where T : class
    {
        component = null;
        if (_storage.TryGetValue(typeof(T), out var dict) && 
            dict.TryGetValue(entity, out var obj))
        {
            component = obj as T;
            return component != null;
        }
        return false;
    }
}
```

**Query Methods - Truy vấn Components:**

ComponentStore cung cấp các phương thức Query cho phép Systems truy vấn entities có các components cụ thể:

```csharp
// Query 1 component type
public IEnumerable<KeyValuePair<EntityId, T>> Query<T>() where T : class
{
    if (_storage.TryGetValue(typeof(T), out var dict))
    {
        foreach (var kvp in dict)
        {
            yield return new KeyValuePair<EntityId, T>(kvp.Key, (T)kvp.Value);
        }
    }
}

// Query 2 component types (Entities phải có CẢ HAI components)
public IEnumerable<(EntityId, T1, T2)> Query<T1, T2>() where T1, T2 : class
{
    if (!_storage.TryGetValue(typeof(T1), out var dict1)) yield break;
    if (!_storage.TryGetValue(typeof(T2), out var dict2)) yield break;
    
    foreach (var kvp in dict1)
    {
        if (dict2.TryGetValue(kvp.Key, out var obj2))
        {
            yield return (kvp.Key, (T1)kvp.Value, (T2)obj2);
        }
    }
}

// Query 3 và 4 component types tương tự...
```

**Đặc điểm thiết kế:**
1. **Type-keyed Storage**: Mỗi loại Component có Dictionary riêng
2. **Generic Methods**: Type-safe API với generics
3. **Lazy Enumeration**: yield return cho phép lazy evaluation
4. **Event Notification**: OnComponentAdded event khi component được thêm

#### 4.2.5. SystemManager - Điều phối Systems

**Cấu trúc:**
```csharp
public interface ISystem
{
    void Initialize(World world);
    void Update(float dt);
    void FixedUpdate(float dt);
    void Shutdown();
}

public class SystemManager
{
    private readonly List<ISystem> _systems = new();
    
    public void AddSystem(ISystem sys, World world)
    {
        _systems.Add(sys);
        sys.Initialize(world);
    }
    
    public void UpdateAll(float dt)
    {
        foreach (var s in _systems)
        {
            try
            {
                s.Update(dt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);  // Exception isolation
            }
        }
    }
    
    public void ShutdownAll()
    {
        foreach (var s in _systems)
        {
            s.Shutdown();
        }
        _systems.Clear();
    }
}
```

**Đặc điểm thiết kế:**
1. **ISystem Interface**: Định nghĩa lifecycle chuẩn cho tất cả Systems
2. **Exception Isolation**: Một System crash không làm crash toàn bộ game
3. **Ordered Execution**: Systems chạy theo thứ tự được thêm vào
4. **Two Update Loops**: Update() cho logic phụ thuộc frame, FixedUpdate() cho physics

#### 4.2.6. EventBus - Hệ thống Pub/Sub

EventBus cho phép các Systems giao tiếp với nhau mà không cần biết về sự tồn tại của nhau (loose coupling).

**Cấu trúc:**
```csharp
public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly Dictionary<Type, object> _lastEvents = new();
    
    public void Subscribe<T>(Action<T> handler)
    {
        Type t = typeof(T);
        if (!_subscribers.TryGetValue(t, out List<Delegate> list))
        {
            list = new List<Delegate>();
            _subscribers[t] = list;
        }
        list.Add(handler);
        
        // Replay last event cho late subscriber
        if (_lastEvents.TryGetValue(t, out object lastEvent))
        {
            handler((T)lastEvent);
        }
    }
    
    public void Publish<T>(T evt)
    {
        Type t = typeof(T);
        _lastEvents[t] = evt;  // Cache để replay cho late subscribers
        
        if (!_subscribers.TryGetValue(t, out List<Delegate> list)) return;
        
        var subscribersCopy = list.ToArray();  // Copy để tránh modification issues
        foreach (Delegate d in subscribersCopy)
        {
            try
            {
                ((Action<T>)d)?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
```

**Đặc điểm thiết kế:**
1. **Decoupled Communication**: Systems không cần reference đến nhau
2. **Late Subscriber Support**: Subscriber mới nhận ngay event cuối cùng
3. **Type-safe**: Generic methods đảm bảo type safety
4. **Safe Iteration**: Copy list trước khi iterate để tránh modification issues

#### 4.2.7. ServiceLocator - Dependency Injection

ServiceLocator cung cấp cơ chế dependency injection đơn giản cho các services.

**Cấu trúc:**
```csharp
public class ServiceLocator : IServiceLocator
{
    private readonly Dictionary<Type, object> _services = new();
    
    public void Register<T>(T instance) where T : class
    {
        if (_services.ContainsKey(typeof(T)))
        {
            Debug.LogWarning($"Service {typeof(T).Name} already registered — overwriting.");
        }
        _services[typeof(T)] = instance;
    }
    
    public T Resolve<T>() where T : class
    {
        _services.TryGetValue(typeof(T), out object obj);
        return obj as T;
    }
    
    public bool TryResolve<T>(out T instance) where T : class
    {
        if (_services.TryGetValue(typeof(T), out object obj))
        {
            instance = obj as T;
            return instance != null;
        }
        instance = null;
        return false;
    }
}
```

**Các Services được đăng ký trong Project:**
- ITimeService: Cung cấp deltaTime, fixedDeltaTime
- IInputService: Xử lý input từ keyboard/mouse
- ICameraService: Điều khiển camera
- IAudioService: Quản lý audio
- IObjectPoolService: Object pooling

### 4.3. WorldRunner - Tích hợp với Unity

WorldRunner là MonoBehaviour đóng vai trò cầu nối giữa ECS và Unity game loop.

**Cấu trúc:**
```csharp
[DefaultExecutionOrder(-90)]  // Chạy sớm trong Unity execution order
public class WorldRunner : NetworkBehaviour
{
    [SerializeField] private SpawnConfigSO spawnConfig;
    [SerializeField] private EntityViewRegistry entityViewRegistry;
    [SerializeField] private InputService inputService;
    [SerializeField] private CinemachineCameraService cameraService;
    [SerializeField] private AudioService audioService;
    
    public World World { get; private set; }
    public static WorldRunner Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        World = new World();
        InitServices();
        InitSystems();
    }
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitServerSystems();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }
    
    private void Update()
    {
        var time = World.Services.Resolve<ITimeService>();
        World.Systems.UpdateAll(time.DeltaTime);
    }
    
    private void FixedUpdate()
    {
        var time = World.Services.Resolve<ITimeService>();
        World.Systems.FixedUpdateAll(time.FixedDeltaTime);
    }
}
```

**Quy trình khởi tạo:**

1. **Awake() - Khởi tạo cơ bản:**
   - Tạo World instance mới
   - Đăng ký tất cả Services (Time, Input, Camera, Audio, ObjectPool)
   - Đăng ký Client Systems (Camera, TransformSync, Audio, Input)

2. **OnNetworkSpawn() - Khởi tạo Network:**
   - Chỉ chạy trên Server
   - Đăng ký Server-only Systems (Movement, Health, Attack, Damage, Skills, Enemy AI)
   - Đăng ký callback cho client connection

3. **Update() - Frame Loop:**
   - Gọi UpdateAll() trên tất cả Systems
   - Truyền deltaTime cho tính toán phụ thuộc thời gian

4. **FixedUpdate() - Physics Loop:**
   - Gọi FixedUpdateAll() trên tất cả Systems
   - Truyền fixedDeltaTime cho physics calculations

### 4.4. Cấu trúc thư mục Project

```
Assets/Scripts/
├── Core/
│   └── ECS/
│       ├── World.cs              # Container chính
│       ├── WorldRunner.cs        # Unity integration
│       ├── EntityId.cs           # Entity identifier
│       ├── EntityManager.cs      # Entity lifecycle
│       ├── EntityView.cs         # Unity GameObject binding
│       ├── ComponentStore.cs     # Component storage
│       ├── SystemManager.cs      # System orchestration
│       ├── EventBus.cs           # Event pub/sub
│       └── ServiceLocator.cs     # Dependency injection
│
└── ECS/
    ├── Components/               # 15 component types
    │   ├── TransformDataComponent.cs
    │   ├── MovementDataComponent.cs
    │   ├── HealthDataComponent.cs
    │   ├── AttackDataComponent.cs
    │   ├── WeaponDataComponent.cs
    │   ├── NetworkComponent.cs
    │   ├── SkillSetComponent.cs
    │   ├── CombatStateComponent.cs
    │   ├── EnemyComponent.cs
    │   └── ...
    │
    ├── Systems/                  # 20 system types
    │   ├── MovementSystem.cs
    │   ├── AttackSystem.cs
    │   ├── HealthSystem.cs
    │   ├── DamageSystem.cs
    │   ├── SkillSystem.cs
    │   ├── CombatStateSystem.cs
    │   ├── EnemyAISystem.cs
    │   ├── EnemyMovementSystem.cs
    │   ├── SpawnSystem.cs
    │   └── ...
    │
    ├── Views/                    # Unity visualization
    │   ├── NetworkSyncView.cs    # NGO integration
    │   ├── AnimationView.cs
    │   ├── MovementView.cs
    │   ├── AttackExecutionView.cs
    │   └── ...
    │
    ├── Events/                   # Event definitions
    │   ├── InputEvent.cs
    │   ├── AttackEvent.cs
    │   ├── HealthEvent.cs
    │   ├── AnimationEvent.cs
    │   └── ...
    │
    ├── Interfaces/               # Service contracts
    │   ├── IInputService.cs
    │   ├── IAudioService.cs
    │   ├── ITimeService.cs
    │   └── ...
    │
    └── Services/                 # Service implementations
        ├── InputService.cs
        ├── AudioService.cs
        └── ...
```

---

## 5. TÍCH HỢP ECS VỚI UNITY NETCODE FOR GAMEOBJECTS (NGO)

### 5.1. Thách thức khi kết hợp ECS và Networking

Việc tích hợp kiến trúc ECS tùy chỉnh với Unity Netcode for GameObjects (NGO) đặt ra nhiều thách thức:

**5.1.1. Ownership Model khác biệt**

- NGO sử dụng `NetworkObject` và `OwnerClientId` để xác định ownership
- ECS sử dụng `EntityId` đơn giản

**5.1.2. State Synchronization**

- NGO sử dụng `NetworkVariable<T>` để đồng bộ state tự động
- ECS lưu state trong Components

**5.1.3. Authority Model**

- NGO hoạt động theo mô hình Client-Server với Server authority
- ECS nguyên bản không có khái niệm network authority

**5.1.4. Remote Procedure Calls**

- NGO sử dụng ServerRpc/ClientRpc cho communication
- ECS sử dụng EventBus cho internal communication

### 5.2. Giải pháp: Bridge Pattern với Views

Project sử dụng **Bridge Pattern** với `NetworkSyncView` làm cầu nối giữa ECS World và NGO Network layer.

**Kiến trúc tổng quan:**

```
┌───────────────────────────────────────────────────────────────────────────┐
│                              SERVER                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                    ECS WORLD (Authoritative)                          │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                   │ │
│  │  │ Components  │  │  Systems    │  │  Events     │                   │ │
│  │  │ (Ground     │  │ (Gameplay   │  │             │                   │ │
│  │  │  Truth)     │  │  Logic)     │  │             │                   │ │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                   │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                │                                           │
│                                ▼                                           │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                  NetworkSyncView (Bridge)                              │ │
│  │  - Subscribe ECS Events → Update NetworkVariables                     │ │
│  │  - Receive ServerRpcs → Publish ECS Events                            │ │
│  │  - Server authority validation                                         │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                │                                           │
│                    NetworkVariables + RPCs                                 │
└────────────────────────────────┼───────────────────────────────────────────┘
                                 │
                    ═════════════════════════════ NETWORK
                                 │
┌────────────────────────────────┼───────────────────────────────────────────┐
│                    NetworkVariables + RPCs                                 │
│                                │                                           │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                  NetworkSyncView (Bridge)                              │ │
│  │  - Listen NetworkVariable changes → Update ECS Components             │ │
│  │  - Send ServerRpcs for actions (attack, move input)                   │ │
│  │  - Client-side prediction + Reconciliation                            │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                                │                                           │
│                                ▼                                           │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                    ECS WORLD (Predicted)                               │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                   │ │
│  │  │ Components  │  │  Systems    │  │   Views     │                   │ │
│  │  │ (Predicted) │  │ (Visual/    │  │ (Unity GO)  │                   │ │
│  │  │             │  │  Audio)     │  │             │                   │ │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                   │ │
│  └──────────────────────────────────────────────────────────────────────┘ │
│                              CLIENT                                        │
└───────────────────────────────────────────────────────────────────────────┘
```

### 5.3. NetworkSyncView - Chi tiết Implementation

NetworkSyncView là class quan trọng nhất trong việc tích hợp ECS với NGO.

**5.3.1. NetworkVariables để đồng bộ State**

```csharp
public class NetworkSyncView : NetworkBehaviour
{
    private World _world;
    private EntityId _entity;
    
    // NetworkVariables - Server ghi, Client đọc
    private NetworkVariable<NetworkTransformState> _netTransform = new(
        writePerm: NetworkVariableWritePermission.Server
    );
    private NetworkVariable<NetworkHealthState> _netHealth = new(
        writePerm: NetworkVariableWritePermission.Server
    );
    private NetworkVariable<CombatState> _netCombatState = new(
        writePerm: NetworkVariableWritePermission.Server
    );
    private NetworkVariable<NetworkMovementState> _netMovement = new(
        writePerm: NetworkVariableWritePermission.Server
    );
}
```

**5.3.2. Server-side: ECS → NetworkVariables**

Server đọc state từ ECS Components và ghi vào NetworkVariables:

```csharp
private void ServerUpdate()
{
    // Đồng bộ Transform từ ECS sang NetworkVariable
    if (_world.Components.TryGet(_entity, out TransformComponent trans))
    {
        var newState = new NetworkTransformState
        {
            Position = trans.Position,
            Rotation = trans.Rotation,
            Tick = _currentTick,
        };
        
        // Chỉ update khi thay đổi đáng kể để tiết kiệm bandwidth
        if (Vector3.Distance(_netTransform.Value.Position, newState.Position) > 0.01f ||
            Quaternion.Angle(_netTransform.Value.Rotation, newState.Rotation) > 1f)
        {
            _netTransform.Value = newState;
        }
    }
    
    // Tương tự cho Health, Movement, Combat State...
}
```

**5.3.3. Client-side: NetworkVariables → ECS**

Client lắng nghe thay đổi NetworkVariables và update ECS Components:

```csharp
private void Start()
{
    if (IsClient && !IsServer)
    {
        // Subscribe vào NetworkVariable changes
        _netTransform.OnValueChanged += OnNetTransformChanged;
        _netHealth.OnValueChanged += OnNetHealthChanged;
        _netCombatState.OnValueChanged += OnNetCombatStateChanged;
        _netMovement.OnValueChanged += OnNetMovementChanged;
    }
}

private void OnNetTransformChanged(NetworkTransformState old, NetworkTransformState newState)
{
    // Update ECS Component từ NetworkVariable mới
    _previousPosition = old.Position;
    _targetPosition = newState.Position;
    _lerpProgress = 0f;  // Reset interpolation
}

private void OnNetHealthChanged(NetworkHealthState old, NetworkHealthState newState)
{
    if (_world.Components.TryGet(_entity, out HealthDataComponent health))
    {
        health.CurrentHealth = newState.CurrentHealth;
        health.MaxHealth = newState.MaxHealth;
    }
}
```

### 5.4. Input Flow: Client → Server → ECS

**5.4.1. Client gửi Input lên Server**

```csharp
private void ClientPredictionUpdate()
{
    // 1. Thu thập input từ InputService
    var inputService = _world.Services.Resolve<IInputService>();
    Vector2 moveInput = inputService.GetMoveInput();
    
    // 2. Tạo input state với tick number
    var inputState = new ClientInputState
    {
        Tick = _currentTick,
        MoveInput = moveInput,
        MouseWorldPos = inputService.GetMouseWorldPosition(),
    };
    
    // 3. Lưu vào history để reconciliation
    _inputHistory.Enqueue(inputState);
    if (_inputHistory.Count > 60) _inputHistory.Dequeue();
    
    // 4. Gửi lên server
    SendInputToServerRpc(inputState);
    
    // 5. Apply local prediction ngay lập tức
    if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
    {
        movement.InputDirection = moveInput;
    }
}
```

**5.4.2. Server xử lý Input và Update ECS**

```csharp
[ServerRpc]
private void SendInputToServerRpc(ClientInputState input)
{
    if (_world == null || _entity.Equals(default)) return;
    
    // 1. Update rotation từ mouse position
    if (_world.Components.TryGet(_entity, out TransformComponent trans))
    {
        Vector3 aimDir = (input.MouseWorldPos - trans.Position).normalized;
        aimDir.y = 0;
        if (aimDir.sqrMagnitude > 0.01f)
        {
            trans.Rotation = Quaternion.LookRotation(aimDir);
        }
    }
    
    // 2. Update movement component
    if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
    {
        movement.InputDirection = input.MoveInput;
        
        // 3. Publish event để MovementSystem xử lý
        if (input.MoveInput.sqrMagnitude > 0.01f)
        {
            _world.Events.Publish(new MovePressedInputEvent(_entity, input.MoveInput));
        }
    }
    
    // 4. Acknowledge input đã xử lý
    AcknowledgeInputClientRpc(input.Tick);
}
```

### 5.5. Client-Side Prediction và Reconciliation

**5.5.1. Client-Side Prediction**

Client không chờ đợi server confirm mà apply input ngay lập tức:

```csharp
// Trong ClientPredictionUpdate()
if (_world.Components.TryGet(_entity, out MovementDataComponent movement))
{
    movement.InputDirection = moveInput;  // Immediate local application
}
```

Điều này giúp game cảm thấy responsive dù có network latency.

**5.5.2. Reconciliation khi có sai lệch**

Khi server gửi acknowledged tick, client so sánh với server state:

```csharp
[ClientRpc]
private void AcknowledgeInputClientRpc(uint acknowledgedTick)
{
    if (!IsOwner) return;
    
    // 1. Xóa các input đã được server xử lý
    while (_inputHistory.Count > 0 && _inputHistory.Peek().Tick <= acknowledgedTick)
    {
        _inputHistory.Dequeue();
    }
    
    // 2. So sánh vị trí local với server
    if (_world.Components.TryGet(_entity, out TransformComponent trans))
    {
        float distance = Vector3.Distance(trans.Position, _netTransform.Value.Position);
        
        // 3. Nếu sai lệch lớn, snap về vị trí server
        if (distance > 0.5f)
        {
            trans.Position = _netTransform.Value.Position;
            trans.Rotation = _netTransform.Value.Rotation;
            
            // Sync Unity Transform
            var registry = _world.Services.Resolve<EntityViewRegistry>();
            if (registry.TryGet(_entity, out EntityView view))
            {
                view.transform.position = trans.Position;
                view.transform.rotation = trans.Rotation;
            }
        }
    }
}
```

### 5.6. Interpolation cho Remote Players

Đối với remote players (không phải local player), client interpolate giữa các state updates:

```csharp
private void ClientInterpolation()
{
    if (_world == null || IsOwner || IsServer) return;
    
    // Tăng lerp progress
    _lerpProgress += Time.deltaTime * 10f;
    
    // Interpolate position và rotation
    if (_world.Components.TryGet(_entity, out TransformComponent trans))
    {
        trans.Position = Vector3.Lerp(_previousPosition, _targetPosition, _lerpProgress);
        trans.Rotation = Quaternion.Slerp(_previousRotation, _targetRotation, _lerpProgress);
    }
}
```

### 5.7. Phân chia Systems theo Authority

**5.7.1. Systems chạy trên TẤT CẢ Clients**

```csharp
private void InitSystems()
{
    // Visual/Audio/Camera - chạy local, không cần network
    World.Systems.AddSystem(new CameraFollowSystem(), World);
    World.Systems.AddSystem(new TransformSyncSystem(), World);
    World.Systems.AddSystem(new AudioSystem(), World);
    World.Systems.AddSystem(new AudioProfileSystem(), World);
    
    // InputSystem xử lý input local, gửi RPC lên server
    World.Systems.AddSystem(new InputSystem(), World);
}
```

**5.7.2. Systems chạy CHỈ trên Server**

```csharp
private void InitServerSystems()
{
    // Core gameplay - Server làm authoritative
    World.Systems.AddSystem(new SpawnSystem(spawnConfig), World);
    World.Systems.AddSystem(new MovementSystem(), World);
    World.Systems.AddSystem(new HealthSystem(), World);
    World.Systems.AddSystem(new AttackSystem(), World);
    World.Systems.AddSystem(new DamageSystem(), World);
    World.Systems.AddSystem(new SkillSystem(), World);
    World.Systems.AddSystem(new CombatStateSystem(), World);
    
    // Status effects
    World.Systems.AddSystem(new StunSystem(), World);
    World.Systems.AddSystem(new KnockbackSystem(), World);
    World.Systems.AddSystem(new HealthRegenSystem(), World);
    World.Systems.AddSystem(new PlayerRespawnSystem(), World);
    
    // Enemy AI - Chỉ server chạy AI
    World.Systems.AddSystem(new EnemyVisionSystem(), World);
    World.Systems.AddSystem(new EnemyPathfindingSystem(), World);
    World.Systems.AddSystem(new EnemyMovementSystem(), World);
    World.Systems.AddSystem(new EnemyAISystem(), World);
}
```

### 5.8. Attack Flow - Ví dụ Multiplayer Hoàn Chỉnh

**5.8.1. Client Request Attack**

```csharp
// Trong InputSystem, khi player nhấn attack
if (inputService.AttackButtonPressed())
{
    Vector3 mousePos = inputService.GetMouseWorldPosition();
    
    // Gửi request lên server
    if (_world.Components.TryGet(entity, out NetworkSyncComponent sync))
    {
        sync.SyncView.RequestAttackServerRpc(mousePos);
    }
}
```

**5.8.2. Server Validates và Processes Attack**

```csharp
[ServerRpc]
public void RequestAttackServerRpc(Vector3 mouseWorldPos)
{
    // 1. Validate attack có thể thực hiện
    if (!_world.Components.TryGet(_entity, out AttackDataComponent attack)) return;
    if (!_world.Components.TryGet(_entity, out WeaponDataComponent weapon)) return;
    
    if (!attack.CanAttack(weapon.BaseCooldown) || attack.IsAttacking)
    {
        RejectAttackClientRpc();  // Từ chối attack
        return;
    }
    
    // 2. Tính toán attack direction từ mouse position
    attack.AttackDirection = CalculateAttackDirection(mouseWorldPos);
    
    // 3. Publish event vào ECS để AttackSystem xử lý
    _world.Events.Publish(new AttackPressedInputEvent(_entity));
    
    // 4. Broadcast cho tất cả clients để play animation
    BroadcastAttackClientRpc();
}
```

**5.8.3. Server AttackSystem xử lý**

```csharp
// Trong AttackSystem
private void OnAttackRequest(AttackPressedInputEvent evt)
{
    var attack = _world.Components.Get<AttackDataComponent>(evt.Entity);
    var weapon = _world.Components.Get<WeaponDataComponent>(evt.Entity);
    
    // Đặt cooldown
    attack.LastAttackTime = Time.time;
    attack.IsAttacking = true;
    
    // Tạo AttackExecutionRequest event
    _world.Events.Publish(new AttackExecutionRequestEvent
    {
        Attacker = evt.Entity,
        Type = weapon.AttackType,
        Direction = attack.AttackDirection,
        Damage = weapon.BaseDamage,
        Range = weapon.AttackRange,
        // ...
    });
}
```

**5.8.4. Broadcast Visual Effects cho Clients**

```csharp
[ClientRpc]
private void BroadcastAttackExecutionClientRpc(
    AttackExecutionType type,
    Vector3 direction,
    float damage,
    float projectileSpeed,
    // ...)
{
    if (IsServer) return;  // Server đã xử lý
    
    // Play animation
    _world.Events.Publish(new AnimationParameterEvent(
        _entity, 
        weapon.AttackAnimationTrigger, 
        AnimationParameterType.Trigger, 
        null
    ));
    
    // Spawn visual-only projectile (không có damage trên client)
    if (type == AttackExecutionType.Projectile)
    {
        SpawnClientProjectile(direction, projectileSpeed, ...);
    }
}
```

---

## 6. KẾT LUẬN

### 6.1. Tóm tắt lợi ích của kiến trúc ECS

Qua quá trình nghiên cứu và phát triển, kiến trúc ECS đã chứng minh nhiều ưu điểm vượt trội cho phát triển game:

1. **Hiệu suất cao**: Data-oriented design, cache-friendly memory layout, batch processing
2. **Khả năng mở rộng**: Dễ dàng thêm features mà không phá vỡ code hiện có
3. **Bảo trì dễ dàng**: Logic tách biệt, dễ debug và unit test
4. **Hỗ trợ Multiplayer**: Tách biệt rõ ràng giữa authoritative logic và visual prediction
5. **Linh hoạt**: Composition over inheritance giải quyết các vấn đề của OOP truyền thống

### 6.2. Kiến trúc Hybrid ECS-NGO

Project sử dụng kiến trúc **Hybrid ECS-NGO** kết hợp:
- **ECS Layer**: Quản lý gameplay logic thuần túy với ComponentStore, Systems, EventBus
- **View Layer**: Bridge giữa ECS và Unity MonoBehaviours (EntityView, TransformSyncView)
- **Network Layer**: NGO handles networking, states được đồng bộ giữa server ECS và client ECS

### 6.3. Best Practices đã áp dụng

- ✅ Components chỉ chứa dữ liệu, không chứa logic xử lý
- ✅ Systems xử lý logic, subscribe vào Events để nhận thông báo
- ✅ EventBus cho loose coupling giữa các Systems
- ✅ ServiceLocator cho dependency injection
- ✅ Views làm bridge giữa ECS và Unity MonoBehaviours
- ✅ Server-authoritative architecture với client-side prediction
- ✅ NetworkVariables cho state synchronization, RPCs cho action requests
- ✅ Interpolation cho smooth remote player movement
- ✅ Reconciliation để xử lý prediction errors

### 6.4. Hướng phát triển

Kiến trúc ECS tùy chỉnh này có thể được mở rộng thêm:
- Job System integration cho parallel processing
- Burst Compiler optimization
- NativeArray cho memory optimization
- Rollback netcode cho competitive multiplayer
