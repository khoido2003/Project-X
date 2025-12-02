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

# 🎮 Roguelike Arena Game Design Document (Unity + NGO)


## 1. Core Vision

- **Genre:** Fast-paced roguelike battle arena with PvPvE chaos.
- **Modes:**
  - **Offline:** Bots + bosses + hazards.
  - **Online:** PvP + enemies + hazards + shrinking map.
- **Pillars:** Unpredictability, chaos, skill expression, replayability.
- **Tech:** Unity + NGO networking.

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

## 4. Architecture & Refactor Roadmap

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


### Network architecture
````
┌─────────────────────────────────────────────────────────────┐
│                         CLIENT                               │
├─────────────────────────────────────────────────────────────┤
│  Input System (Local Only)                                   │
│       ↓                                                       │
│  [Input Events] ──→ NetworkInputSync ──RPC──→ SERVER        │
│                                                               │
│  [State Updates] ←──RPC── NetworkStateSync ←── SERVER       │
│       ↓                                                       │
│  ECS Components (Predicted/Synced)                           │
│       ↓                                                       │
│  View Layer (Rendering)                                      │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                         SERVER                               │
├─────────────────────────────────────────────────────────────┤
│  NetworkInputSync ←──RPC── CLIENT INPUTS                    │
│       ↓                                                       │
│  [Apply Inputs to ECS]                                       │
│       ↓                                                       │
│  Movement System (Authority)                                 │
│  Attack System (Authority)                                   │
│  Skill System (Authority)                                    │
│  Health System (Authority)                                   │
│  Damage System (Authority)                                   │
│       ↓                                                       │
│  NetworkStateSync ──RPC──→ CLIENT STATE UPDATES             │
└─────────────────────────────────────────────────────────────┘
````


```
┌─────────────────────────────────────────────────────────────┐
│                     LOCAL PLAYER CLIENT                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  1. Input System → Collect Input                             │
│  2. Predict Locally (Movement, Animation)                    │
│  3. NetworkSyncView.SendInputToServerRpc()                   │
│                                                               │
│  4. ← NetworkSyncView.AcknowledgeInputClientRpc()            │
│  5. Reconciliation: Check if prediction matches server       │
│  6. If mismatch > threshold → Snap to server position        │
│                                                               │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                          SERVER                              │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  1. ← Receive Input via SendInputToServerRpc()               │
│  2. Apply Input to MovementDataComponent                     │
│  3. MovementSystem → Calculate Movement                      │
│  4. AttackSystem/SkillSystem → Validate Actions              │
│  5. DamageSystem → Apply Damage (Authority)                  │
│  6. HealthSystem → Check Death (Authority)                   │
│                                                               │
│  7. NetworkSyncView → Sync State to All Clients              │
│     - Transform (60Hz)                                        │
│     - Movement (30Hz)                                         │
│     - Health (On Change)                                      │
│     - Combat State (On Change)                                │
│     - Animations (On Change)                                  │
│                                                               │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                     REMOTE PLAYER CLIENT                     │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  1. ← Receive State Updates via NetworkVariables             │
│  2. OnNetTransformChanged → Update TransformComponent        │
│  3. OnNetHealthChanged → Update HealthDataComponent          │
│  4. OnNetCombatStateChanged → Update CombatStateComponent    │
│  5. SyncAnimationClientRpc → Play Animations                 │
│                                                               │
│  6. Render Remote Player (No Prediction)                     │
│                                                               │
└─────────────────────────────────────────────────────────────┘

```

#### Component sync strategy

|        Component       | Server Authority |  Client Prediction  |           Sync Method           |   Frequency   |
|:----------------------:|:----------------:|:-------------------:|:-------------------------------:|:-------------:|
| TransformComponent     | ✓                | ✓ (Local Player)    | NetworkVariable + Interpolation | Every tick    |
| HealthDataComponent    | ✓                | ✗                   | NetworkVariable                 | On Change     |
| MovementDataComponent  | ✓                | ✓ (Local Player)    | Input → Server, State → Client  | Every tick    |
| AttackDataComponent    | ✓                | ✗                   | RPC (Events)                    | On Attack     |
| CombatStateComponent   | ✓                | ✗                   | NetworkVariable                 | On Change     |
| SkillSetComponent      | ✓                | ✗                   | RPC (Events)                    | On Skill Cast |
| AnimationDataComponent | ✓                | ✓ (Predict locally) | RPC (Parameters)                | On Change     |



|        System        |       Server      |     Client (Owner)    | Client (Remote) |      Authority      |
|:-------------------:|:-----------------:|:---------------------:|:---------------:|:-------------------:|
| InputSystem         | ❌                 | ✅                     | ❌               | Client              |
| MovementSystem      | ✅                 | ✅ (Predict)           | ❌               | Server              |
| AttackSystem        | ✅                 | ✅ (Predict Animation) | ❌               | Server              |
| DamageSystem        | ✅                 | ❌                     | ❌               | Server Only         |
| HealthSystem        | ✅                 | ❌                     | ❌               | Server Only         |
| SkillSystem         | ✅                 | ✅ (Preview)           | ❌               | Server              |
| CombatStateSystem   | ✅                 | ✅ (Read)              | ✅ (Read)        | Server              |
| AnimationView       | ✅                 | ✅                     | ✅               | All                 |
| TransformSyncSystem | ✅                 | ✅                     | ✅               | All                 |
| AttackExecutionView | ✅                 | ❌                     | ❌               | Server Only         |
| SkillPreviewView    | ❌                 | ✅                     | ❌               | Client (Owner) Only |
| ProjectileView      | ✅ (Hit Detection) | ❌                     | ❌               | Server Only         |



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


