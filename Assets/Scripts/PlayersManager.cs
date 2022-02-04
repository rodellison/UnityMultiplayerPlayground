using System;
using System.Collections;
using System.Collections.Generic;
using DilmerGames.Core.Singletons;
using Unity.Netcode;
using UnityEngine;

public class PlayersManager : NetworkSingleton<PlayersManager>
{
    NetworkVariable<int> playersInGame = new NetworkVariable<int>();

    private void OnEnable()
    {
        playersInGame.OnValueChanged += (value, newValue) =>
        {
            Logger.Instance.LogInfo("PlayerCount changed");
            UIManager.Instance.UpdatePlayerCount(newValue);
        };
    }
    
    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            if (IsServer)
            {
                playersInGame.Value = NetworkManager.Singleton.ConnectedClients.Count;
            }
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            if (IsServer)
            {
                playersInGame.Value = NetworkManager.Singleton.ConnectedClients.Count;
            }
        };
    }

    [ClientRpc]
    public void RequestClientDisconnectClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Logger.Instance.LogWarning("Client requested to shutdown...");
        UIManager.Instance.ShutdownClient();
    }
}