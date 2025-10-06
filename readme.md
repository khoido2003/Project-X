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

### How the New Architecture Works
- **Entities:** IDs managed by the ECS core, not MonoBehaviours.
- **Components:** Pure C# classes holding data (e.g., HealthComponent, MovementComponent).
- **Systems:** Pure C# classes operating on entities with specific components (e.g., MovementSystem, HealthSystem).
- **Views:** MonoBehaviours that sync ECS data to Unity objects (e.g., position, animation, rendering).
- **Services:** Input, audio, and networking are abstracted behind interfaces and injected into systems.
- **ScriptableObjects:** All gameplay configuration and data is stored in SOs, referenced by components/systems.

### Refactor Checklist
- [ ] Finalize and polish ECS core (World, EntityManager, ComponentStore, SystemManager, EventBus).
- [ ] Organize folders as per new structure.
- [ ] Migrate movement to ECS (MovementComponent, MovementSystem, MovementView).
- [ ] Migrate health to ECS (HealthComponent, HealthSystem, HealthView).
- [ ] Migrate attack to ECS (AttackComponent, AttackSystem, AttackView).
- [ ] Migrate skills to ECS (SkillComponent, SkillSystem, SkillView).
- [ ] Migrate status effects to ECS (StatusEffectComponent, StatusEffectSystem, StatusEffectView).
- [ ] Refactor input, audio, and networking as services (IInputService, IAudioService, INetworkService).
- [ ] Move all gameplay data/config to ScriptableObjects.
- [ ] Refactor UI to use ECS data and events.
- [ ] Remove old tightly-coupled MonoBehaviour logic after migration.
- [ ] Add Mirror networking as a service/adapter.
- [ ] Test and validate after each migration step.

### Roadmap (Step-by-Step)
1. **ECS Core:** Finalize ECS core and ensure it supports all needed operations.
2. **Folder Structure:** Reorganize scripts into the new folder structure.
3. **Movement:** Migrate movement logic/data to ECS (MovementComponent, MovementSystem, MovementView).
4. **Health:** Migrate health logic/data to ECS.
5. **Attack:** Migrate attack logic/data to ECS.
6. **Skills:** Migrate skill logic/data to ECS.
7. **Status Effects:** Migrate status effect logic/data to ECS.
8. **Services:** Refactor input, audio, and networking as services and inject into systems.
9. **ScriptableObjects:** Move all gameplay data/config to SOs and refactor systems/components to use them.
10. **UI:** Refactor UI to use ECS data/events.
11. **Cleanup:** Remove old MonoBehaviour logic and test thoroughly.
12. **Networking:** Integrate Mirror as a service/adapter, keeping core logic agnostic to networking.

---

**After this refactor, the project will be clean, modular, scalable, and ready for rapid feature development and networking.**
  bool Exclusive;

  string[] Tags;

  int InternalCooldownMs;
