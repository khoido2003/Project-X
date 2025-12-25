using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles enemy visual movement on SERVER ONLY.
/// On CLIENT, EnemyNetworkSyncView handles interpolation directly.
/// </summary>
public class EnemyMovementView : EntityView
{
    public bool SmoothPosition = true;
    public float PositionLerpSpeed = 10f;
    public float RotationLerpSpeed = 10f;
    public float SnapThreshold = 0.5f;

    private Transform _tranform;
    private bool _isServer;

    private void Awake()
    {
        _tranform = transform;
    }
    
    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);
        
        // PERFORMANCE: Disable this script on clients
        // EnemyNetworkSyncView handles client-side interpolation
        _isServer = NetworkManager.Singleton?.IsServer == true;
        if (!_isServer)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        // Only run on server
        if (!_isServer)
            return;
            
        if (WorldInstance == null || EntityInstance.Equals(default))
            return;

        if (!WorldInstance.Components.TryGet(EntityInstance, out TransformComponent tf))
            return;

        // SERVER: instant sync, no interpolation
        _tranform.SetPositionAndRotation(tf.Position, tf.Rotation);
    }
}

