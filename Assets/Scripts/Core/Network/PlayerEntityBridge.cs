using Mirror;
using NUnit.Framework.Internal.Execution;
using Unity.Services.Authentication;
using UnityEngine;

public class PlayerEntityBridge : NetworkBehaviour
{
    [SerializeField]
    private string defaultCharacterAssetId;

    private EntityId myEntity;

    public override void OnStartServer()
    {
        base.OnStartServer();

        myEntity = World.Instance.CreateEntity();

        string characterAssetId = defaultCharacterAssetId;

        var lobby = LobbyController.Instance?.GetCurrentLobby();
        if (lobby != null)
        {
            var player = lobby.Players.Find(p => p.Id == AuthenticationService.Instance.PlayerId);

            if (
                player != null
                && player.Data != null
                && player.Data.TryGetValue("Character", out var cd)
                && !string.IsNullOrEmpty(cd.Value)
            )
            {
                characterAssetId = cd.Value;
            }
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        NetworkEntityRegistry.Instance.Unregister(connectionToClient.connectionId);

        World.Instance.Components.RemoveAllComponents(myEntity);
        World.Instance.Entities.DestroyEntity(myEntity);
    }

    private CharacterDefinitionSO FindCharacterDefinition(string assetId)
    {
        if (string.IsNullOrEmpty(assetId))
        {
            return null;
        }

        var all = Resources.LoadAll<CharacterDefinitionSO>("");
        foreach (var c in all)
        {
            if (c.assetId == assetId)
            {
                return c;
            }
        }
        return null;
    }
}
