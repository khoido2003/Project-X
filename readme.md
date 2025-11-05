# 📚 Git Workflow (Team Rules)

## 1. Branch Rules
- **`main`**
  - Always stable, production-ready.
  - Only updated via **Pull Requests (PRs)**.
  - Protected branch (no direct commits).

- **Feature branches (`dev_xxx`)**
  - Created from the latest `main`.
  - One branch per feature/bugfix.
  - Deleted after merge.

---

## 2. Starting New Work
Always branch off the latest `main`:

```bash
git checkout main
git pull origin main
git checkout -b dev_featureName
```

---

## 3. While Working
- Commit often with clear messages.
- Push to remote frequently.
- Open a Pull Request early (for visibility).

---

## 4. Syncing With Main (Avoiding Conflicts)
If your branch is behind `main`:

```bash
git checkout main
git pull origin main
git checkout dev_featureName
git rebase main   # or merge --ff-only if you prefer
```

Resolve conflicts **only once** here → not at PR merge.

---

## 5. Merging PRs
On GitHub:
- Use **Rebase and Merge** for clean history.
- Never “Create a merge commit” (avoids messy trees).
- Never push directly to `main`.

---

## 6. After Merge
⚠️ Important: Do **not** keep working in the old feature branch after it’s merged.
Instead, reset or start fresh:

```bash
# If you want to reuse the same branch:
git checkout dev_featureName
git fetch origin
git reset --hard origin/main

# OR (preferred) delete and recreate:
git branch -D dev_featureName
git checkout -b dev_newFeature origin/main
```

This avoids duplicate commits + random conflicts.

---

## 7. Quick Commands (Cheat Sheet)

```bash
# Update main
git checkout main
git pull origin main

# Start new feature branch
git checkout -b dev_feature origin/main

# Sync feature with main
git checkout main
git pull origin main
git checkout dev_feature
git rebase main

# Save WIP before reset
git stash push -m "WIP"
git reset --hard origin/main
git stash pop
```

---

# 🎮 Roguelike Arena Game Design Document (Unity + Mirror)


## 1. Core Vision

- **Genre:** Fast-paced roguelike battle arena with PvPvE chaos.
- **Modes:**
  - **Offline:** Bots + bosses + hazards.
  - **Online:** PvP + enemies + hazards + shrinking map.
- **Pillars:** Unpredictability, chaos, skill expression, replayability.
- **Tech:** Unity + Mirror networking.

---

## 2. Match Flow

- **Wave-based structure** with global augment phases.
- **Augment choices:** 3 per match
  - Turn 1: Universal augments.
  - Turn 2: Hero-synergy augments.
  - Turn 3: Crazy/power spike augments (before boss).
- **Hazards + enemies** escalate per wave.
- **Boss** spawns in final wave.
- **Win condition:**
  - Online → last player/team alive.
  - Offline → survive boss fight.

**Timeline Example:**

0:00–1:30 → Wave 1 (weak enemies, light hazards)
Augment 1 (Universal)

1:30–3:00 → Wave 2 (harder enemies, shrinking map)
Augment 2 (Hero-specific)

3:00–4:30 → Wave 3 (hazards escalate, elites spawn)
Augment 3 (Crazy/Powerful)

4:30+ → Boss + Final Circle chaos



---

## 3. High-Level UML (text)

```
[MatchManager] 1---* [WaveSpawner]
[MatchManager] 1---* [MapController]
[MatchManager] 1---* [AIManager]
[MatchManager] 1---* [AugmentManager]

[Player] *---1 [PlayerState]
[Player] ---1 [HeroController]
[HeroController] 1---1 [SkillSystem]
[HeroController] 1--- [ActiveAugmentInstance]

[EnemyAI] *---1 [EnemyData]
[BossController] 1---1 [EnemyData]

[AugmentDataPool] used by AugmentManager
```


---

## 4. New Architecture & Refactor Roadmap

### Overview
The project will be refactored to a clean, ECS-inspired, SOLID, and data-driven architecture using classic Unity (not DOTS). This will make the codebase scalable, maintainable, and ready for networking.

### Architecture Principles
- **ECS-Inspired:** Separate data (components), logic (systems), and Unity integration (views/adapters).
- **SOLID:** Each class has a single responsibility, is open for extension, uses interfaces, and depends on abstractions.
- **Data-Driven:** All gameplay data (stats, skills, weapons, etc.) is in ScriptableObjects or data files.
- **Networking-Ready:** Core logic is agnostic to networking; Mirror is integrated as a service/adapter.


```
                   ┌────────────────────┐
                   │      World         │
                   │────────────────────│
                   │ Entities           │
                   │ Components         │
                   │ Systems            │
                   │ Services           │
                   │ Events             │
                   └────────────────────┘
                            │
        ┌───────────────────┼────────────────────┐
        │                   │                    │
        ▼                   ▼                    ▼
┌────────────┐       ┌───────────────┐     ┌──────────────┐
│ EntityMgr  │       │ ComponentStore│     │ SystemManager│
│ creates,   │       │ stores data by│     │ updates logic│
│ destroys,  │       │ entity+type   │     │ over entities│
└────────────┘       └───────────────┘     └──────────────┘
        │                   │                    │
        │                   │                    │
        ▼                   ▼                    ▼
    ┌───────────┐       ┌────────────┐       ┌───────────────┐
    │ Entity IDs│◄─────►│ Components │◄─────►│ Systems (Logic)│
    └───────────┘       └────────────┘       └───────────────┘
                                                   │
                                            ┌──────┴───────┐
                                            │ Services     │
                                            │ (Time, Input)│
                                            └──────────────┘
```

```
WorldRunner
    ↓
World
    ↓
SystemManager → MovementSystem
    ↓
ComponentStore → MovementData for entity
    ↓
EntityView (updates GameObject)
```


### Folder Structure
```
Assets/Scripts/
  Core/ECS/         # ECS core (World, Entity, System, etc.)
  Components/       # Pure data components (no logic, no MonoBehaviour)
  Systems/          # Pure logic systems (no Unity dependencies)
  Views/            # MonoBehaviours for Unity integration (EntityView, MovementView, etc.)
  Services/         # Input, Audio, Network, etc. (interfaces + implementations)
  ScriptableObjects/# Data assets for configuration
  UI/               # UI logic
```

# 🎮 Mirror Multiplayer Guide (Unity)

A complete **big-picture overview** of Mirror networking.
Use this as a cheatsheet + roadmap. If you need details → check Mirror docs, but this covers what you must know to design and implement features.

---

## 🚀 Core Concepts

### NetworkManager
- Central component of Mirror.
- Handles starting/stopping server, client, and host.
- Manages player spawning and scene transitions.

```csharp
// Start host (server + local client)
NetworkManager.singleton.StartHost();

// Start client
NetworkManager.singleton.StartClient();
```

---

### NetworkIdentity
- Required on every networked object (player, NPC, items, bullets).
- Provides a unique `netId`.
- Handles ownership (authority).

✅ If an object must sync between players → add `NetworkIdentity`.

---

### NetworkBehaviour
- Base class for network scripts.
- Unlocks `[Command]`, `[ClientRpc]`, `[TargetRpc]`, `[SyncVar]`.

```csharp
public class Player : NetworkBehaviour
{
    [SyncVar] public int health;
}
```

---

### Authority
- **Server authority (default)** → server controls object logic.
- **Client authority** → client can control specific objects (usually its player).
- Authority can be transferred (e.g., a player drives a vehicle).

```csharp
// Transfer authority to a client
netIdentity.AssignClientAuthority(conn);

// Remove authority
netIdentity.RemoveClientAuthority();
```

---

## 🖥️ Network Roles

- **Host** → Server + local client in one process.
- **Server** → Authoritative game state.
- **Client** → Connects to server, sends inputs, receives updates.

---

## 🧩 Common Mirror Components

- **NetworkManagerHUD**
  - Debug UI for quick testing (Host / Server / Client).

- **NetworkTransform**
  - Syncs position/rotation/scale.
  - Use carefully: real-time sync can be bandwidth-heavy.

- **NetworkAnimator**
  - Syncs Animator parameters (`isRunning`, `attackTrigger`).

- **NetworkRigidbody / NetworkRigidbody2D**
  - Syncs physics state.
  - Server controls physics, clients display results.

---

## 📡 Data Flow in Mirror

1. **Client input → Server** (`[Command]`).
2. **Server updates state** (SyncVar, spawn, or `[ClientRpc]`).
3. **Clients render state** (visuals, effects, UI).

---

## 🔑 Core Mirror Tools

### 🔹 SyncVar
Keeps a variable synced **server → all clients**.
Only server changes it.

```csharp
[SyncVar(hook = nameof(OnHealthChanged))]
public int health;

void OnHealthChanged(int oldValue, int newValue)
{
    Debug.Log($"Health {oldValue} → {newValue}");
}
```

---

### 🔹 Commands `[Command]`
Client → Server call.
Must be on a `NetworkBehaviour` owned by the client.

```csharp
[Command]
void CmdShoot()
{
    // Runs on server
    var bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
    NetworkServer.Spawn(bullet, connectionToClient);
}
```

---

### 🔹 ClientRpc `[ClientRpc]`
Server → All clients.

```csharp
[ClientRpc]
void RpcPlayExplosion()
{
    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
}
```

---

### 🔹 TargetRpc `[TargetRpc]`
Server → Specific client.

```csharp
[TargetRpc]
void TargetShowMessage(NetworkConnectionToClient conn, string msg)
{
    Debug.Log("Private: " + msg);
}
```

---

### 🔹 NetworkServer.Spawn
Spawns objects across all clients.

```csharp
GameObject bullet = Instantiate(bulletPrefab, pos, rot);
NetworkServer.Spawn(bullet, connectionToClient); // optional: assign ownership
```

---

## 🎯 Typical Player Flow

1. Host/Server starts.
2. Clients connect.
3. Server spawns player prefab.
4. Authority assigned to players.
5. Game loop:
   - Client sends input `[Command]`.
   - Server validates + updates state.
   - SyncVar / Rpc updates → clients see results.

---

## 🕹️ Gameplay Patterns

### Movement
- Quick way → `NetworkTransform`.
- Better way → client sends input `[Command]`, server applies movement, updates SyncVars.

### Combat
- Client presses attack → `[Command] CmdAttack()`.
- Server checks hit + damage → SyncVar `health`.
- Server sends `[ClientRpc] RpcPlayHitEffect()`.

### Inventory
- Server keeps master inventory.
- Client requests action with `[Command]`.
- Server updates, sync via SyncVar / `[TargetRpc]`.

### Chat
- Client sends `[Command] CmdSendMessage(msg)`.
- Server relays with `[ClientRpc] RpcReceiveMessage(msg)`.

---

## 🌍 Scene Management

- NetworkManager can sync scenes for all clients.
- By default → **online scene** loads when host/server starts.
- Use `ServerChangeScene("SceneName")` to change scenes.
- Clients auto-load the same scene.

---

## 📬 Custom Messages

- For lightweight data not tied to GameObjects.

```csharp
// Define message
public struct ChatMessage : NetworkMessage
{
    public string text;
}

// Register handler
NetworkClient.RegisterHandler<ChatMessage>(OnChatMessage);

// Send
NetworkClient.Send(new ChatMessage { text = "Hello" });
```

---

## 🔑 Authority Management

- Objects normally owned by server.
- You can give control to a client.
- Example: vehicles, pets, turrets.

```csharp
// Assign to client
netIdentity.AssignClientAuthority(conn);
```

---

## 🔎 Network Discovery (LAN)

- Lets clients find servers on local network.
- Add **NetworkDiscovery** component to NetworkManager.
- Clients can auto-detect servers without IP.

---

## ⚡ Performance Tips

- Don’t spam `[Command]` or RPC every frame.
- Use SyncVars for state, only send RPCs for events.
- Use **Interest Management** to limit what clients receive.
- Compress or quantize positions (floats → shorts).

---

## 🔐 Security Principles

- Never trust clients.
- Server must validate all commands (no cheating).
- Don’t let clients modify SyncVars directly.
- Keep game logic on the server, clients handle only input + visuals.

---

## 🎲 Matchmaking & Lobbies

- Mirror doesn’t have built-in matchmaking.
- Build your own lobby flow:
  - **Lobby scene** → players connect, pick teams.
  - Host starts game → `ServerChangeScene("GameScene")`.
  - Spawn players when game scene loads.
- For online matchmaking → integrate external services (Steam, PlayFab, Photon Relay, etc.).

---

## 🛠️ Debug / Testing Tools

- **ParrelSync** → multiple Unity editors for testing.
- **Editor + Build** → run one in Editor, others as builds.
- **NetworkManagerHUD** → quick UI for connect/start.
- `[Server]`, `[Client]`, `[Host]` attributes to restrict methods.

```csharp
[Server] void DoServerStuff() { }
[Client] void DoClientStuff() { }
```

---

## 📋 Feature Checklist

When adding new features, ask:

- Does object need to sync?
  → Add `NetworkIdentity`.

- Is it controlled by a client?
  → Needs authority.

- Is state permanent?
  → Use SyncVar.

- Is it client input → server logic?
  → `[Command]`.

- Is it server → all clients?
  → `[ClientRpc]`.

- Is it server → one client?
  → `[TargetRpc]`.

---

# ✅ Summary
- **Start with basics** → NetworkManager, player prefab, SyncVar, Command, Rpc.
- **Then add gameplay** → movement, combat, chat, inventory.
- **Next layer** → scenes, custom messages, authority transfer, discovery.
- **Finally** → optimize (interest management, bandwidth, security, lobbies).
