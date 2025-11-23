using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerCharSelection : NetworkBehaviour
{
    public int CharSelected => m_charSelected.Value;

    private const int k_noCharacterSelectedValue = -1;

    [SerializeField]
    private NetworkVariable<int> m_charSelected = new(k_noCharacterSelectedValue);

    [SerializeField]
    private NetworkVariable<int> m_playerId = new(k_noCharacterSelectedValue);

    [SerializeField]
    private AudioClip _changedCharacterCClip;

    private void Start()
    {
        if (IsServer)
        {
            m_playerId.Value = CharacterSelectionManager.Instance.GetPlayerId(OwnerClientId);
        }
        else if (!IsOwner && HasCharacterSelected())
        {
            CharacterSelectionManager.Instance.SetPlayableChar(m_playerId.Value, m_charSelected.Value, IsOwner);
        }

        gameObject.name = $"Player{m_playerId.Value + 1}";
    }

    private void OnEnable()
    {
        m_playerId.OnValueChanged += OnPlayerIdSet;
        m_charSelected.OnValueChanged += OnCharacterChanged;
        OnButtonPress.a_OnButtonPress += OnUIButtonPress;
    }

    private void OnDisable()
    {
        m_playerId.OnValueChanged -= OnPlayerIdSet;
        m_charSelected.OnValueChanged -= OnCharacterChanged;
        OnButtonPress.a_OnButtonPress -= OnUIButtonPress;
    }

    private void Update()
    {
        if (IsOwner && CharacterSelectionManager.Instance.GetConnectionState(m_playerId.Value) != ConnectionState.ready)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                ChangeCharacterSelection(-1);
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                ChangeCharacterSelection(1);
            }
        }

        if (IsOwner)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!CharacterSelectionManager.Instance.IsReady(m_charSelected.Value))
                {
                    CharacterSelectionManager.Instance.SetPlayerReadyUIButtons(true, m_charSelected.Value);

                    ReadyServerRpc();
                }
                else
                {
                    if (CharacterSelectionManager.Instance.IsSelectedByPlayer(m_playerId.Value, m_charSelected.Value))
                    {
                        CharacterSelectionManager.Instance.SetPlayerReadyUIButtons(false, m_charSelected.Value);

                        NotReadyServerRpc();
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (m_playerId.Value == 0)
                {
                    StartCoroutine(HostShutdown());
                }
                else
                {
                    Shutdown();
                }
            }
        }
    }

    ///////////////////////////////////////////////////////////////

    #region Callbacks


    private void OnUIButtonPress(ButtonActions actions)
    {
        if (!IsOwner)
        {
            return;
        }

        switch (actions)
        {
            case ButtonActions.lobby_ready:

                CharacterSelectionManager.Instance.SetPlayerReadyUIButtons(true, m_charSelected.Value);

                ReadyServerRpc();

                break;

            case ButtonActions.lobby_not_ready:

                CharacterSelectionManager.Instance.SetPlayerReadyUIButtons(false, m_charSelected.Value);

                NotReadyServerRpc();
                break;
        }
    }

    private void OnCharacterChanged(int previousValue, int newValue)
    {
        if (!IsOwner && HasCharacterSelected())
        {
            CharacterSelectionManager.Instance.SetCharacterUI(m_playerId.Value, newValue);
        }
    }

    private void OnPlayerIdSet(int previousValue, int newValue)
    {
        CharacterSelectionManager.Instance.SetPlayableChar(newValue, newValue, IsOwner);

        if (IsServer)
        {
            m_charSelected.Value = newValue;
        }
    }

    #endregion

    ////////////////////////////////////////////////////////

    #region Utitls


    private bool HasCharacterSelected()
    {
        return m_playerId.Value != k_noCharacterSelectedValue;
    }

    private void ChangeCharacterSelection(int value)
    {
        int charTemp = m_charSelected.Value;
        charTemp += value;

        if (charTemp >= CharacterSelectionManager.Instance.characterData.Length)
        {
            charTemp = 0;
        }
        else if (charTemp < 0)
        {
            charTemp = CharacterSelectionManager.Instance.characterData.Length - 1;
        }

        if (IsOwner)
        {
            ChangeCharacterSelectionServerRpc(charTemp);

            CharacterSelectionManager.Instance.SetPlayableChar(m_playerId.Value, charTemp, IsOwner);
        }
    }

    public void Despawn()
    {
        NetworkObjectDespawner.DespawnNetworkObject(NetworkObject);
    }

    private IEnumerator HostShutdown()
    {
        ShutdownClientRpc();

        yield return new WaitForSeconds(0.5f);

        Shutdown();
    }

    private void Shutdown()
    {
        NetworkManager.Singleton.Shutdown();
        LoadingSceneManager.Instance.LoadScene(SceneName.Menu, false);
    }
    #endregion

    /////////////////////////////////////////////////////////////////

    #region Network RPC

    [ClientRpc]
    private void ShutdownClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        Shutdown();
    }

    [ServerRpc]
    private void ChangeCharacterSelectionServerRpc(int newValue)
    {
        m_charSelected.Value = newValue;
    }

    [ServerRpc]
    private void ReadyServerRpc()
    {
        CharacterSelectionManager.Instance.PlayerReady(OwnerClientId, m_playerId.Value, m_charSelected.Value);
    }

    [ServerRpc]
    private void NotReadyServerRpc()
    {
        CharacterSelectionManager.Instance.PlayerNotReady(OwnerClientId, m_charSelected.Value);
    }

    #endregion
}
