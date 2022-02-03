using System;
using System.Collections;
using System.Collections.Generic;
using DilmerGames.Core.Singletons;
using Unity.Netcode;
using UnityEngine;

public class PlayersManager : NetworkSingleton<PlayersManager>
{
    NetworkVariable<int> playersInGame = new NetworkVariable<int>();
    
    //Observer(s) that are waiting for something to happen
    private List<Action<int>> PlayerCountChangedEventObservers;

    public int PlayersInGame
    {
        get
        {
            return playersInGame.Value;
        }
    }

    void Start()
    {
        
        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            if (IsServer)
            {
                playersInGame.Value = NetworkManager.Singleton.ConnectedClients.Count;
 //               playersInGame.Value++;
                StartCoroutine(UpdateClients());
            }
            
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            if (IsServer)
            {
                playersInGame.Value = NetworkManager.Singleton.ConnectedClients.Count;
                //               playersInGame.Value++;
                StartCoroutine(UpdateClients());
            }
        };
       
    }
    
    public void AddPlayerCountChangeObserver(Action<int> handler)
    {
        if (PlayerCountChangedEventObservers == null)
            PlayerCountChangedEventObservers = new List<Action<int>>();
        PlayerCountChangedEventObservers.Add(handler);
    }

    IEnumerator UpdateClients()
    {
        //There is always some latency, wait just a second for the Server's NetworkVariable to propagate to Clients before
        //calling a ClientRpc method to have the Clients update the value on their side
        yield return new WaitForSeconds(1f);
        if (IsServer)
            UpdatePlayerCountClientRpc();
    }

    [ClientRpc]
    public void UpdatePlayerCountClientRpc()
    {
        foreach (var observer in PlayerCountChangedEventObservers)
        {
            observer?.Invoke(PlayersInGame);
        }
        
    }
    
    [ClientRpc]
    public void RequestClientDisconnectClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Logger.Instance.LogWarning("Client requested to shutdown...");
        UIManager.Instance.ShutdownClient();
    }


}
