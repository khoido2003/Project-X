# Audio System Usage Guide

## Overview

The Audio System provides a centralized way to play sounds and music throughout your game. It supports:
- **Multiple audio categories** (Music, UI, Character, Weapon, Skill, Enemy, etc.)
- **Independent volume controls** for each category
- **2D and 3D sounds** (positional audio)
- **Background music** with fade in/out
- **Easy integration** with your ECS architecture

## Setup

### 1. Add AudioService to WorldRunner

1. In Unity Editor, select your `WorldRunner` GameObject
2. In the Inspector, find the `AudioService` field
3. Create an empty GameObject named "AudioService" in your scene
4. Add the `AudioService` component to it
5. Assign it to the `WorldRunner`'s `audioService` field

The AudioService will automatically:
- Create a music source for background music
- Create a pool of audio sources for sound effects
- Initialize all volume settings

### 2. Configure AudioService

In the AudioService component inspector:
- **Music Source**: Leave null (auto-created) or assign your own AudioSource
- **Sound Source Pool Size**: Number of simultaneous sounds (default: 10)
- **Default Volumes**: Set initial volumes for each category
- **3D Sound Settings**: Configure min/max distance for positional audio

## Usage Examples

### Method 1: Using AudioHelper (Recommended - Easiest)

```csharp
// Play a UI sound (2D)
AudioHelper.PlaySound(world, uiClickClip, AudioCategory.UI);

// Play a weapon sound at player position (3D)
Vector3 playerPos = transform.position;
AudioHelper.PlaySound3D(world, weaponFireClip, AudioCategory.Weapon, playerPos);

// Play background music
AudioHelper.PlayMusic(world, combatMusicClip, fadeIn: 2f);

// Stop music
AudioHelper.StopMusic(world, fadeOut: 1f);

// Change volume
AudioHelper.SetVolume(world, AudioCategory.Weapon, 0.5f);
```

### Method 2: Using Events Directly

```csharp
// Play sound
_world.Events.Publish(new PlaySoundEvent(
    clip: weaponClip,
    category: AudioCategory.Weapon,
    position: transform.position, // null for 2D
    volume: null // null to use category volume
));

// Play music
_world.Events.Publish(new PlayMusicEvent(combatMusicClip, fadeIn: 1f));

// Stop music
_world.Events.Publish(new StopMusicEvent(fadeOut: 1f));
```

### Method 3: Using IAudioService Directly

```csharp
var audioService = _world.Services.Resolve<IAudioService>();

// Play sound
audioService.PlaySound(weaponClip, AudioCategory.Weapon, transform.position);

// Play music
audioService.PlayMusic(combatMusicClip, fadeIn: 2f);

// Set volume
audioService.SetCategoryVolume(AudioCategory.Music, 0.7f);
```

## Integration Examples

### Weapon Attack Sound

In `AnimationEventRelayView.cs` or `AttackExecutionView.cs`:

```csharp
private void HandleAttackHit()
{
    // ... existing attack code ...
    
    // Play weapon sound
    if (weapon.AttackSound != null)
    {
        var registry = _world.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(_entityView.EntityInstance, out EntityView view))
        {
            AudioHelper.PlaySound3D(
                _world,
                weapon.AttackSound,
                AudioCategory.Weapon,
                view.transform.position
            );
        }
    }
}
```

### Skill Sound

In `SkillExecutorView.cs`:

```csharp
protected override void ExecuteSkill(SkillConfirmExecutionEvent @event)
{
    // Play skill sound
    if (@event.Skill.activateSound != null)
    {
        var registry = WorldInstance.Services.Resolve<EntityViewRegistry>();
        if (registry.TryGet(EntityInstance, out EntityView view))
        {
            AudioHelper.PlaySound3D(
                WorldInstance,
                @event.Skill.activateSound,
                AudioCategory.Skill,
                view.transform.position
            );
        }
    }
    
    // ... rest of skill execution ...
}
```

### UI Button Click

In your UI button handler:

```csharp
public void OnButtonClick()
{
    // Play UI click sound
    if (WorldRunner.Instance != null && WorldRunner.Instance.World != null)
    {
        AudioHelper.PlaySound(
            WorldRunner.Instance.World,
            buttonClickClip, // Your AudioClip reference
            AudioCategory.UI
        );
    }
}
```

### Background Music (Game State Manager)

In `NetworkGameStateManager.cs`:

```csharp
private void OnPhaseChanged(GamePhase newPhase, int round)
{
    switch (newPhase)
    {
        case GamePhase.CombatPhase:
            AudioHelper.PlayMusic(_world, combatMusicClip, fadeIn: 2f);
            break;
            
        case GamePhase.BossPhase:
            AudioHelper.PlayMusic(_world, bossMusicClip, fadeIn: 2f);
            break;
            
        case GamePhase.UpgradePhase:
            AudioHelper.PlayMusic(_world, upgradeMusicClip, fadeIn: 1f);
            break;
    }
}
```

### Enemy Death Sound

In `EnemyDeadStateAI.cs`:

```csharp
public void OnEnter(World world, EntityId entity)
{
    // ... existing death code ...
    
    // Play death sound
    if (world.Components.TryGet(entity, out TransformComponent trans))
    {
        AudioHelper.PlaySound3D(
            world,
            enemyDeathClip, // Your AudioClip
            AudioCategory.Enemy,
            trans.Position
        );
    }
}
```

### Footstep Sound

In `MovementSystem.cs` or `EnemyMovementSystem.cs`:

```csharp
// When character is moving
if (movement.IsMoving && movement.IsGrounded)
{
    // Play footstep sound periodically
    if (Time.time - lastFootstepTime > footstepInterval)
    {
        AudioHelper.PlaySound3D(
            _world,
            footstepClip,
            AudioCategory.Footstep,
            trans.Position,
            volume: 0.3f // Lower volume for footsteps
        );
        lastFootstepTime = Time.time;
    }
}
```

## Audio Categories

Use these categories to organize your sounds:

- **Master**: Controls all audio (master volume)
- **Music**: Background music
- **UI**: User interface sounds (clicks, notifications)
- **Character**: Player character sounds (voice, grunts)
- **Weapon**: Weapon attack sounds
- **Skill**: Skill activation sounds
- **Enemy**: Enemy sounds (attacks, death, etc.)
- **Environment**: Ambient sounds (wind, water, etc.)
- **Footstep**: Footstep sounds

## Volume Control

### Runtime Volume Control

```csharp
// Set volume for a category (0-1)
AudioHelper.SetVolume(world, AudioCategory.Music, 0.5f);

// Get volume
var audioService = world.Services.Resolve<IAudioService>();
float musicVol = audioService.GetCategoryVolume(AudioCategory.Music);

// Set master volume
audioService.SetMasterVolume(0.8f);
```

### Save/Load Volume Settings

```csharp
// Save volumes
PlayerPrefs.SetFloat("MasterVolume", audioService.GetMasterVolume());
PlayerPrefs.SetFloat("MusicVolume", audioService.GetCategoryVolume(AudioCategory.Music));
PlayerPrefs.Save();

// Load volumes
audioService.SetMasterVolume(PlayerPrefs.GetFloat("MasterVolume", 1f));
audioService.SetCategoryVolume(AudioCategory.Music, PlayerPrefs.GetFloat("MusicVolume", 0.7f));
```

## Best Practices

1. **Use AudioHelper for simplicity** - It's the easiest way to play sounds
2. **Use 3D sounds for gameplay** - Weapons, skills, enemies should use positional audio
3. **Use 2D sounds for UI** - UI sounds should always be 2D
4. **Don't play sounds every frame** - Use timers or events to prevent spam
5. **Preload important clips** - Keep frequently used clips in memory
6. **Use appropriate categories** - Helps with volume control and organization
7. **Test volume levels** - Make sure sounds are balanced

## Troubleshooting

**No sound playing?**
- Check that AudioService is assigned in WorldRunner
- Check that AudioSystem is added to World.Systems
- Verify AudioClip is not null
- Check volume settings (might be muted)

**Sounds cutting off?**
- Increase Sound Source Pool Size in AudioService
- Reduce number of simultaneous sounds

**Music not fading?**
- Ensure AudioService has a valid music source
- Check that fade coroutine isn't being interrupted

