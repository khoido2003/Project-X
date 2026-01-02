/// <summary>
/// Component to track invincibility state for an entity.
/// Used during respawn and upgrade selection for temporary damage immunity.
/// </summary>
public class InvincibilityComponent
{
    /// <summary>
    /// Whether invincibility is currently active.
    /// </summary>
    public bool IsActive;

    /// <summary>
    /// Remaining duration of invincibility in seconds.
    /// </summary>
    public float RemainingDuration;

    /// <summary>
    /// Total duration of invincibility when it was activated.
    /// </summary>
    public float TotalDuration;
}
