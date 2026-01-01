using System;
using Unity.Netcode;
using UnityEngine;

public class BuffPickupComponent : NetworkBehaviour
{
    private BuffSO _buffData;
    private SpawnPoint _spawnPoint;
    private Action<BuffSO, SpawnPoint> _onPickupCallback;
    private bool _isPickedUp = false;
    
    [SerializeField] private float _pickupRadius = 0.5f;
    [SerializeField] private LayerMask _playerLayerMask = -1;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        InitializeVisuals();
    }

    private void InitializeVisuals()
    {
        var particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            ps.Clear();
            ps.Play();
        }
        
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }
        
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
            }
        }
    }

    public void Initialize(BuffSO buffData, SpawnPoint spawnPoint, Action<BuffSO, SpawnPoint> onPickupCallback)
    {
        _buffData = buffData;
        _spawnPoint = spawnPoint;
        _onPickupCallback = onPickupCallback;
    }

    private void Update()
    {
        if (_isPickedUp) return;
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, _pickupRadius, _playerLayerMask);
        
        foreach (var collider in colliders)
        {
            var buffHandler = collider.GetComponent<BuffHandlerView>();
            if (buffHandler == null)
            {
                buffHandler = collider.GetComponentInParent<BuffHandlerView>();
            }
            
            if (buffHandler == null) continue;
            
            var playerNetworkObject = collider.GetComponentInParent<NetworkObject>();
            if (playerNetworkObject == null) continue;
            
            if (!playerNetworkObject.IsOwner) continue;
            
            if (IsServer)
            {
                ProcessPickup(buffHandler);
            }
            else
            {
                _isPickedUp = true;
                RequestPickupServerRpc(playerNetworkObject.OwnerClientId);
            }
            
            break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;
        if (!IsServer) return;
        
        var buffHandler = other.GetComponent<BuffHandlerView>();
        if (buffHandler == null)
        {
            buffHandler = other.GetComponentInParent<BuffHandlerView>();
        }
        
        if (buffHandler != null)
        {
            ProcessPickup(buffHandler);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != clientId) return;
        if (_isPickedUp) return;
        
        var playerObjects = FindObjectsOfType<NetworkObject>();
        foreach (var playerObj in playerObjects)
        {
            if (playerObj.OwnerClientId == clientId)
            {
                var buffHandler = playerObj.GetComponent<BuffHandlerView>();
                if (buffHandler == null)
                {
                    buffHandler = playerObj.GetComponentInChildren<BuffHandlerView>();
                }
                
                if (buffHandler != null)
                {
                    ProcessPickup(buffHandler);
                    return;
                }
            }
        }
    }

    private void ProcessPickup(BuffHandlerView buffHandler)
    {
        if (_isPickedUp) return;
        _isPickedUp = true;
        
        if (_buffData == null) return;
        
        buffHandler.ApplyBuff(_buffData);
        _onPickupCallback?.Invoke(_buffData, _spawnPoint);
        
        GetComponent<NetworkObject>().Despawn();
    }
}
