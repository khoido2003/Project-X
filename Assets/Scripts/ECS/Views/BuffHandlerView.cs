using UnityEngine;

public class BuffHandlerView : EntityView
{
    private NetworkSyncView _networkSyncView;

    public override void Bind(World world, EntityId entity)
    {
        base.Bind(world, entity);
        _networkSyncView = GetComponentInParent<NetworkSyncView>();
    }

    public void ApplyBuff(BuffSO buff)
    {
        if (_networkSyncView == null)
        {
            _networkSyncView = GetComponentInParent<NetworkSyncView>();
        }
        
        if (_networkSyncView == null) return;

        switch (buff.Type)
        {
            case BuffSO.BuffType.Health:
                ApplyHealthBuff(buff.Value);
                break;
            case BuffSO.BuffType.Speed:
                ApplySpeedBuff(buff.Value, buff.Duration);
                break;
        }
    }

    private void ApplyHealthBuff(float percentage)
    {
        if (WorldInstance == null) return;
        
        if (WorldInstance.Components.TryGet(EntityInstance, out HealthDataComponent health))
        {
            float healAmount = health.MaxHealth * (percentage / 100f);
            _networkSyncView.ApplyHealthBuffFromServer(healAmount);
        }
    }

    private void ApplySpeedBuff(float percentage, float duration)
    {
        if (WorldInstance == null) return;
        _networkSyncView.ApplySpeedBuffFromServer(percentage, duration);
    }
}
