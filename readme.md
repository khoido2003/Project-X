
# 🎮 Roguelike Arena Game Design Document (Unity + Mirror)

---

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

## 4. ScriptableObject / Data Models

### HeroData
```csharp
class HeroData : ScriptableObject {
  string HeroID;
  string DisplayName;
  Sprite Portrait;

  int MaxHP;
  float MoveSpeed;
  float BaseAttackDamage;
  float AttackRange;
  float AttackSpeed;

  WeaponData Weapon;
  List<SkillData> Skills;
  List<string> Tags; // e.g. ["melee","mobility"]
}
```

### WeaponData
```csharp
class WeaponData : ScriptableObject {
  string WeaponID;
  string DisplayName;
  enum WeaponType { Melee, Projectile, Hitscan }
  WeaponType Type;
  float Damage;
  float AttackRate;
  float Range;
  GameObject ProjectilePrefab;
  List<string> Tags;
}
```

### SkillData
```csharp
class SkillData : ScriptableObject {
  string SkillID;
  string DisplayName;
  string Description;
  SkillCategory Category;   // Active, Passive, Ultimate
  SkillType Type;           // Dash, Projectile, AoE, Buff
  float Cooldown;
  float CastTime;
  float Range;
  EffectDefinition[] Effects;
  List<string> Tags;        // for augment filtering
}
```

###  AugmentData
```csharp
class AugmentData : ScriptableObject {
  string AugmentID;
  string DisplayName;
  string Description;
  AugmentCategory Category; // universal, melee, ranged, caster, etc.
  float Weight;
  Modifier[] Modifiers;
  Condition[] Conditions;
  float Duration;
  bool Exclusive;
  string[] Tags;
  int InternalCooldownMs;
  bool ShowOnTurn1;
  bool ShowOnTurn2;
  bool ShowOnTurn3;
}
```

### EnemyData
```csharp
class EnemyData : ScriptableObject {
  string EnemyID;
  string Name;
  int MaxHP;
  float MoveSpeed;
  float Damage;
  float AttackRange;
  EnemyAIType AIType;     // Hunter, Ranged, Summoner, Exploder
  SpawnWeight SpawnWeight;
  List<SkillData> Abilities;
  List<string> Tags;
}
```

### BossData
```csharp
class BossData : ScriptableObject {
  string BossID;
  string Name;
  int MaxHP;
  List<BossPhase> Phases; // HPThreshold, Abilities, Modifiers
  float AggroRadius;
  List<string> TargetingRules;
}
```

### MapData & HazardData
```csharp
class MapData : ScriptableObject {
  string MapID;
  string DisplayName;
  int SizeX; int SizeZ;
  float ShrinkPerWavePct;
  List<HazardData> Hazards;
  List<SpawnNode> EnemySpawnNodes;
}

class HazardData {
  string HazardID;
  string DisplayName;
  HazardType Type;          // Lava, Ice, Ghost, Lightning
  float DamagePerSecond;
  float ActiveDuration;
  float Cooldown;
  float Radius;
  float TelegraphTime;
}
```


## 5.Interfaces
```csharp

interface IDamageable {
  int CurrentHP { get; }
  void TakeDamage(int amount, DamageContext ctx);
  void Heal(int amount);
  bool IsAlive();
}

interface IAugmentable {
  void ApplyAugment(AugmentInstance instance);
  void RemoveAugment(string augmentID);
}

interface ISkillUser {
  bool CanCast(string skillID);
  void CastSkill(string skillID, SkillTarget target);
}

interface IMovable {
  Vector3 Position { get; }
  void Move(Vector3 dir, float deltaTime);
}
```

## 6. Runtime Components
- MatchManager: controls waves, augments, map shrink.

- AugmentManager: samples augments, validates picks, applies effects.

- PlayerState: holds HP, augments, score (SyncVars).

- HeroController: runtime instance from HeroData, executes attacks/skills.

- SkillSystem: executes SkillData into effects/projectiles.

- EnemyAIController: runs FSM based on EnemyData.

- AggroManager: calculates lowest-aggression player to pressure.

## 7. Networking(Mirror)

### Commands(Client -> Server)
```
CmdRequestMove(Vector3 dir, float timeStamp);
CmdCastSkill(string skillID, Vector3 aimPos);
CmdPickAugment(string augmentID);
```


### RPCs (Server -> Client)
```
RpcSpawnProjectile(int netId, Vector3 pos, Vector3 dir, float speed);
RpcPlayVFX(string vfxKey, Vector3 pos);
TargetShowAugmentChoices(conn, List<AugmentDTO> choices);
```

## 8. Sequence Flows

### Augment Phase

1.MatchManager ends wave → start augment phase.
2.AugmentManager samples 3 augments per player.
3.Send to client via TargetShowAugmentChoices.
4.Client picks → CmdPickAugment.
5.Server validates + applies augment.
6.Resume combat.

### Wave progression

- Server spawns enemies per wave config.
- Hazards intensify over time.
- Every few waves → boss spawn.
- Map shrinks gradually.

## 9. Example Data (JSON)

Hero Example

```json
{
  "HeroID":"blademaster",
  "DisplayName":"Blademaster",
  "MaxHP":1000,
  "MoveSpeed":6.0,
  "BaseAttackDamage":120,
  "AttackRange":1.5,
  "AttackSpeed":1.0,
  "Weapon":"katana_01",
  "Skills":["dash_strike","whirlwind","parry"],
  "Tags":["melee","mobility"]
}
```

Augment Example

```json
{
  "AugmentID":"uni_swift_feet",
  "DisplayName":"Swift Feet",
  "Description":"+15% movement speed",
  "Category":"universal",
  "Weight":1.2,
  "Modifiers":[{"type":"MoveSpeedPct","value":15}],
  "Duration":0,
  "Exclusive":false,
  "ShowOnTurn1":true,
  "ShowOnTurn2":true,
  "ShowOnTurn3":false
}
```

## 10. Augment Pool

### Universal

- Swift Feet → +15% movement speed.
- Tough Skin → +20% max HP.
- Berserker Rage → +25% attack speed when HP < 50%.
- Sharpened Instincts → +15% crit chance.
- Energy Overload → cooldowns reduced by 15%.
- Momentum → kills grant +10% move speed for 3s.
- Resilient Core → take 20% less hazard damage.
- Blood Pact → heal 15% on kill.

### Melee

- Cleave → attacks hit in arc.
- Vampiric Edge → heal 10% of melee damage dealt.
- Heavy Strikes → +30% melee damage, -10% attack speed.
- Chain Slash → melee hits can chain to 1 extra enemy.

### Ranged

- Piercing Shot → projectiles pierce 1 enemy.
- Explosive Tips → projectiles deal AoE splash.
- Rapid Fire → +25% fire rate.
- Longshot → +20% range, +10% crit.

### Caster

- Mana Surge → cooldown -25%.
- Arcane Echo → spells repeat at 50% power.
- Elemental Infusion → random element adds burn/freeze.
- Mystic Overload → +40% skill damage, takes +15% damage.

### Defensive

- Iron Wall → -25% dmg taken for 2s after skill cast.
- Guardian’s Aura → allies within 5m take -10% damage.
- Adaptive Shell → gain 15% resist after hazard hit.
- Second Wind → revive with 30% HP once per match.

### Mobility

- Double Dash → +1 dash charge.
- Blinkstrike → dash resets basic attack cooldown.
- Slippery → immune to slows.
- Wall Cling → jump off walls for bonus height.

### Chaos / Power Spikes

- Overdrive → +100% dmg for 10s, then stunned 3s.
- Last Stand → gain +50% dmg when below 25% HP.
- Chaos Storm → spawn random hazard around you every 15s.
- Juggernaut → become unstoppable, -20% move speed.

## 11. Dev Checklist

- Define SOs for heroes, weapons, skills, augments, enemies, bosses, maps.

- Implement MatchManager (server state machine).

- Implement HeroFactory + HeroController.

- Implement SkillSystem (data-driven).

- Implement AugmentManager + UI flow.

- Add AI controllers & AggroManager.

- Add MapController + hazards.

- Implement Mirror networking.

- Add boss controllers + phases.

- Playtest & balance via SO values.

## 12. Key Design Principles

- Data-driven: All stats in SO/JSON, no har
- SOLID: Separate responsibilities (e.g., SkillSystem vs HeroController).
- Server-authoritative: All combat validated by server.
- Replayable: Random augments, hazards, spawn patterns.
- Anti-hide: Aggro system, hazard pressure, map shrink.


