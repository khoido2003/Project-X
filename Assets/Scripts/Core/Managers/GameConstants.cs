using UnityEngine;

public static class GameConstants
{
    // Score Values
    public const int SCORE_ENEMY_KILL = 100;
    public const int SCORE_PLAYER_KILL = 500;
    public const int SCORE_BOSS_KILL = 2000;

    // Respawn Settings
    public const float PLAYER_RESPAWN_DELAY = 5f;
    public const float DAMAGE_ATTRIBUTION_TIMEOUT = 10f; // Time window to attribute kills

    // Phase Durations
    public const float LOBBY_WAIT_TIME = 5f;
    public const float UPGRADE_PHASE_DURATION = 15f;
    public const float COMBAT_PHASE_DURATION = 60f;
    public const float GAME_END_DURATION = 10f;

    // Player Settings
    public const int MIN_PLAYERS = 1; // For testing, set to 4 for production
    public const int MAX_PLAYERS = 4;

    // Invincibility Settings
    public const float INVINCIBILITY_DURATION = 1.5f;
}
