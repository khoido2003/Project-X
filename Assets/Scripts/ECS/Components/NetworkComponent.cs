using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkSyncComponent
{
    [Flags]
    public enum DirtyFlags
    {
        None,
        Transform,
        Health,
        Movement,
        Combat,
        Animation,
        All,
    }

    public NetworkSyncView SyncView;

    public Vector3 LastSyncedPosition;
    public Quaternion LastSyncedRotation;
    public float LastSyncedHealth;
    public Vector3 LastSyncedMoveDirection;

    public uint LastProcessedInputTick;
    public Queue<ClientInputState> InputBuffer;

    public DirtyFlags Dirty = DirtyFlags.None;

    public void MarkDirty(DirtyFlags flags)
    {
        Dirty |= flags;
    }

    public bool IsDirty(DirtyFlags flags)
    {
        return (Dirty & flags) != 0;
    }

    public void ClearDirty()
    {
        Dirty = DirtyFlags.None;
    }
}

public class NetworkOwnerComponent
{
    public ulong ClientId;
    public bool IsLocalPlayer;
}

public class NetworkObjectComponent
{
    public NetworkObject NetworkObject;
}

public class CharacterSelectionComponent
{
    public int characterIndex;
    public CharacterDefinitionSO CharacterData;
}
