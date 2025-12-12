# Audio Integration Guide

This guide explains how to set up and use the integrated audio system for background music, sound effects, and voiceovers.

## Overview

The audio system consists of:
- **SceneAudioConfig**: ScriptableObject for scene-specific background music
- **VoiceoverConfig**: ScriptableObject for game event voiceovers (countdown, phases, etc.)
- **UISoundConfig**: ScriptableObject for UI interaction sounds
- **AudioService**: Persistent singleton that manages all audio playback
- **AudioHelper**: Static helper methods for easy audio playback

## Setup Instructions

### Step 1: Create Audio Configuration Assets

1. **Create SceneAudioConfig**:
   - Right-click in Project window → `Create → Audio → Scene Audio Config`
   - Name it `SceneAudioConfig`
   - Assign music clips for each scene:
     - Menu Music
     - Character Selection Music
     - Map_1 Music
     - Map_2 Music
     - Map_3 Music
     - Victory Music
     - Defeat Music

2. **Create VoiceoverConfig**:
   - Right-click in Project window → `Create → Audio → Voiceover Config`
   - Name it `VoiceoverConfig`
   - Assign voiceover clips:
     - Countdown: 3, 2, 1
     - Ready
     - Game Start
     - Round Announcement
     - Upgrade Phase
     - Combat Phase
     - Boss Fight
     - Game Over
     - Victory
     - Defeat

3. **Create UISoundConfig**:
   - Right-click in Project window → `Create → Audio → UI Sound Config`
   - Name it `UISoundConfig`
   - Assign UI sound clips:
     - Button Click
     - Button Cancel
     - Button Hover (optional)
     - Upgrade Card Appear
     - Upgrade Selected

### Step 2: Assign Configurations to Managers

1. **LoadingSceneManager**:
   - Find `LoadingSceneManager` in your Bootstrap scene (or persistent scene)
   - Assign `SceneAudioConfig` to the `Scene Audio Config` field

2. **CharacterSelectionManager**:
   - Find `CharacterSelectionManager` in Character Selection scene
   - Assign `UISoundConfig` to the `UI Sound Config` field
   - Assign `VoiceoverConfig` to the `Voiceover Config` field

3. **NetworkGameStateManager**:
   - Find `NetworkGameStateManager` in your game scenes (Map_1, Map_2, Map_3)
   - Assign `VoiceoverConfig` to the `Voiceover Config` field

4. **WorldRunner**:
   - Find `WorldRunner` in your game scenes
   - Assign `SceneAudioConfig` to the `Scene Audio Config` field

5. **UpgradeCardContainerUI**:
   - Find `UpgradeCardContainerUI` in your game UI
   - Assign `UISoundConfig` to the `UI Sound Config` field

6. **MenuManager**:
   - Find `MenuManager` in Menu scene
   - Assign `UISoundConfig` to the `UI Sound Config` field

### Step 3: Ensure AudioService Exists

The `AudioService` should be set up in your Bootstrap scene using `AudioBootstrapper`:
- Add `AudioBootstrapper` component to a GameObject in Bootstrap scene
- Assign an `AudioService` prefab (or it will auto-create one)
- The `AudioService` will persist across all scenes automatically

## How It Works

### Scene Music

- **Automatic**: Music plays automatically when scenes load via `LoadingSceneManager`
- **Per Scene**: Each scene can have its own background music
- **Fade Transitions**: Music fades in/out smoothly between scenes

### Voiceovers

- **Countdown**: Plays "3", "2", "1" during character selection countdown
- **Ready**: Plays when countdown starts
- **Game Start**: Plays when game begins
- **Phases**: Plays voiceovers for Upgrade Phase, Combat Phase, Boss Fight, Game Over
- **Results**: Plays Victory or Defeat based on game results

### UI Sounds

- **Button Clicks**: Plays when buttons are clicked (Host, Join, Ready, Cancel, etc.)
- **Upgrade Cards**: Plays when upgrade cards appear and when selected

## Usage Examples

### Playing Sounds from Code (Non-ECS Scenes)

```csharp
// Play UI sound
AudioHelper.PlaySound(uiClickClip, AudioCategory.UI);

// Play 3D sound at position
AudioHelper.PlaySound3D(weaponClip, AudioCategory.Weapon, transform.position);

// Play music
AudioHelper.PlayMusic(menuMusic, fadeIn: 1f);
```

### Playing Sounds from Code (ECS Scenes)

```csharp
// Get World from WorldRunner
World world = WorldRunner.Instance.World;

// Play sound via ECS events
AudioHelper.PlaySound(world, clip, AudioCategory.UI);
AudioHelper.PlaySound3D(world, clip, AudioCategory.Weapon, position);
AudioHelper.PlayMusic(world, musicClip, fadeIn: 1f);
```

### Direct AudioService Access

```csharp
if (AudioService.Instance != null)
{
    AudioService.Instance.PlaySound(clip, AudioCategory.UI);
    AudioService.Instance.PlayMusic(musicClip, fadeIn: 1f);
    AudioService.Instance.SetCategoryVolume(AudioCategory.Music, 0.7f);
}
```

## Audio Categories

The system supports these categories:
- `Master`: Overall volume control
- `Music`: Background music
- `UI`: Interface sounds
- `Character`: Character-related sounds
- `Weapon`: Weapon sounds
- `Skill`: Skill sounds
- `Enemy`: Enemy sounds
- `Environment`: Environmental sounds
- `Footstep`: Footstep sounds

## Tips

1. **Volume Settings**: Adjust volumes in `AudioService` inspector or via `SetCategoryVolume`
2. **Fade Times**: Configure fade in/out times in `SceneAudioConfig`
3. **Voiceover Volume**: Adjust in `VoiceoverConfig`
4. **UI Sound Volume**: Adjust in `UISoundConfig`
5. **Testing**: Use `AudioService.Instance` to test audio without WorldRunner

## Troubleshooting

- **No Music Playing**: Check that `SceneAudioConfig` is assigned to `LoadingSceneManager` and `WorldRunner`
- **No Voiceovers**: Check that `VoiceoverConfig` is assigned to `CharacterSelectionManager` and `NetworkGameStateManager`
- **No UI Sounds**: Check that `UISoundConfig` is assigned to relevant UI managers
- **AudioService Missing**: Ensure `AudioBootstrapper` is in Bootstrap scene or `AudioService` exists in scene

## File Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── Audio/
│   │   │   ├── SceneAudioConfig.cs
│   │   │   ├── VoiceoverConfig.cs
│   │   │   └── UISoundConfig.cs
│   │   ├── Managers/
│   │   │   ├── LoadingSceneManager.cs (updated)
│   │   │   ├── CharacterSelectionManager.cs (updated)
│   │   │   ├── NetworkGameStateManager.cs (updated)
│   │   │   └── MenuManager.cs (updated)
│   │   └── ECS/
│   │       └── WorldRunner.cs (updated)
│   ├── ECS/
│   │   ├── Helpers/
│   │   │   └── AudioHelper.cs (updated)
│   │   └── Services/
│   │       └── AudioService.cs
│   └── UI/
│       └── GameWorldUI/
│           └── UpgradeCardContainerUI.cs (updated)
└── Data/
    └── Audios/
        ├── Musics/
        ├── InterfaceSound/
        └── VoiceOver/
```

## Next Steps

1. Create the three ScriptableObject assets (`SceneAudioConfig`, `VoiceoverConfig`, `UISoundConfig`)
2. Assign your audio clips to each configuration
3. Assign the configurations to the appropriate managers in each scene
4. Test each scene to ensure music and sounds play correctly
5. Adjust volumes and fade times as needed

