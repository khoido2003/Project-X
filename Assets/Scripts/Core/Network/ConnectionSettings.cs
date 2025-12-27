/// <summary>
/// Static settings passed between scenes during connection.
/// Used to communicate spectator intent before network connection.
/// </summary>
public static class ConnectionSettings
{
    /// <summary>
    /// If true, the connecting client wants to spectate, not play.
    /// Set before calling NetworkManager.StartClient().
    /// Reset after entering game scene.
    /// </summary>
    public static bool IsSpectator { get; set; } = false;

    /// <summary>
    /// The IP address to connect to (for join/spectate).
    /// </summary>
    public static string TargetIP { get; set; } = "127.0.0.1";

    /// <summary>
    /// Reset all settings to defaults.
    /// Call when returning to menu or on disconnect.
    /// </summary>
    public static void Reset()
    {
        IsSpectator = false;
        TargetIP = "127.0.0.1";
    }
}
