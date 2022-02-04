using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class UIManager : DilmerGames.Core.Singletons.Singleton<UIManager>
{
    [SerializeField] private Button startServerButton;

    [SerializeField] private Button startHostButton;

    [SerializeField] private Button startClientButton;

    [SerializeField] private Button disconnectClientButton;

    [SerializeField] private TextMeshProUGUI playersInGameText;

    [SerializeField] private TMP_InputField joinCodeInput;

    [SerializeField] private Button executePhysicsButton;

    private bool hasServerStarted;

    private GameObject vCamDefault;

    private void Awake()
    {
        Cursor.visible = true;
    }

    public void UpdatePlayerCount(int PlayerCountValue)
    {
        playersInGameText.text = $"Players in game: {PlayerCountValue}";
    }

    void Start()
    {
        //The UIManager is an interested (observer) of the PlayersManager since its responsible for displaying
        //the value of Player count.
        //Rather than check the value every frame, just pass a method handler as an Action<int> to the 
        //PlayersManager, and have it updated only when someone joins or disconnects..
        //  PlayersManager.Instance.AddPlayerCountChangeObserver(UpdatePlayerCount);

        //A default Cinemachine VCam can be used so there is a 'starting' view that can be different than 
        //the in-game view. 
        vCamDefault = GameObject.FindWithTag("DefaultVCam");

        if (RelayManager.Instance.IsRelayEnabled)
            joinCodeInput?.gameObject.SetActive(true);

        // START SERVER
        startServerButton?.onClick.AddListener(async () =>
        {
            // this allows the UnityMultiplayer and UnityMultiplayerRelay scene to work with and without
            // relay features - if the Unity transport is found and is relay protocol then we redirect all the 
            // traffic through the relay, else it just uses a LAN type (UNET) communication.
            if (RelayManager.Instance.IsRelayEnabled)
                await RelayManager.Instance.SetupRelay();

            if (NetworkManager.Singleton.StartServer())
                Logger.Instance.LogInfo("Server started...");
            else
                Logger.Instance.LogInfo("Unable to start server...");
        });

        // START HOST
        startHostButton?.onClick.AddListener(async () =>
        {
            // this allows the UnityMultiplayer and UnityMultiplayerRelay scene to work with and without
            // relay features - if the Unity transport is found and is relay protocol then we redirect all the 
            // traffic through the relay, else it just uses a LAN type (UNET) communication.
            if (RelayManager.Instance.IsRelayEnabled)
            {
                var relayHostData = await RelayManager.Instance.SetupRelay();
                if (relayHostData.JoinCode == null)
                {
                    Logger.Instance.LogInfo("Unable to start host...");
                    return;
                }
            }

            if (NetworkManager.Singleton.StartHost())
            {
                Logger.Instance.LogInfo("Host started...");
                //Reduce priority of vCamDefault camera if it is present..
                //This will allow any other Cinemachine vCam with higher priority (i.e. followCam)
                //to take over
                if (vCamDefault != null)
                    vCamDefault.GetComponent<CinemachineVirtualCamera>().Priority = 0;
            }
            else
                Logger.Instance.LogInfo("Unable to start host...");
        });

        // START CLIENT
        startClientButton?.onClick.AddListener(async () =>
        {
            if (RelayManager.Instance.IsRelayEnabled)
                if (!string.IsNullOrEmpty(joinCodeInput.text))
                {
                    var relayJoinData = await RelayManager.Instance.JoinRelay(joinCodeInput.text);
                    if (relayJoinData.JoinCode == null)
                    {
                        Logger.Instance.LogInfo("Could not connect to host...");
                        return;
                    }
                }
                else
                {
                    Logger.Instance.LogWarning("No Join Code provided");
                    return;
                }


            if (NetworkManager.Singleton.StartClient())
            {
                Logger.Instance.LogInfo("Client started...");
                //Set the Execute Physics button to be inactive as only the 
                //server/host can control networked physics.
                executePhysicsButton.gameObject.SetActive(false);

                //Reduce priority of vCamDefault camera if it is present..
                //This will allow any other Cinemachine vCam with higher priority (i.e. followCam)
                //to take over
                if (vCamDefault != null)
                    vCamDefault.GetComponent<CinemachineVirtualCamera>().Priority = 0;
            }
            else
                Logger.Instance.LogInfo("Unable to start client...");
        });

        // STATUS TYPE CALLBACKS
        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            Logger.Instance.LogInfo($"{id} just connected...");

            //Hide Start (Host/Server/Client) buttons once we've connected to a server..
            startHostButton?.gameObject.SetActive(false);
            startServerButton?.gameObject.SetActive(false);
            startClientButton?.gameObject.SetActive(false);
            if (RelayManager.Instance.IsRelayEnabled)
                joinCodeInput?.gameObject.SetActive(false);
            disconnectClientButton?.gameObject.SetActive(true);
        };

        // STATUS TYPE CALLBACKS
        //Only the HOST/Server gets this callback when OTHER players disconnect
        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                Logger.Instance.LogInfo($"{id} just disconnected...");
            }
            else
            {
                ShutdownClient();
            }
        };

        NetworkManager.Singleton.OnServerStarted += () =>
        {
            hasServerStarted = true;

            //Hide Start (Host/Server/Client) buttons if we've just started a server..
            startHostButton?.gameObject.SetActive(false);
            startServerButton?.gameObject.SetActive(false);
            startClientButton?.gameObject.SetActive(false);
            if (RelayManager.Instance.IsRelayEnabled)
                joinCodeInput?.gameObject.SetActive(false);
            executePhysicsButton?.gameObject.SetActive(true);
            disconnectClientButton?.gameObject.SetActive(true);
        };

        executePhysicsButton?.onClick.AddListener(() =>
        {
            if (!hasServerStarted)
            {
                Logger.Instance.LogWarning("Server has not started...");
                return;
            }

            SpawnerControl.Instance.SpawnObjects();
        });

        disconnectClientButton?.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                StartCoroutine(GracefulServerShutdown());
            else
            {
                ShutdownClient();
            }
        });
    }

    public void ShutdownClient()
    {
        NetworkManager.Singleton.Shutdown();
        //After clients disconnected, ensure the camera and UI for the last client (host if there was one) is reset
        if (vCamDefault != null)
            vCamDefault.GetComponent<CinemachineVirtualCamera>().Priority = 100;

        startHostButton?.gameObject.SetActive(true);
        startServerButton?.gameObject.SetActive(true);
        startClientButton?.gameObject.SetActive(true);
        if (RelayManager.Instance.IsRelayEnabled)
            joinCodeInput?.gameObject.SetActive(true);
        executePhysicsButton.gameObject.SetActive(false);
        disconnectClientButton?.gameObject.SetActive(false);
        playersInGameText.text = $"Players in game: 0";
    }

    IEnumerator GracefulServerShutdown()
    {
        Logger.Instance.LogInfo("Graceful server shutdown initiated...");
        //First get the ulong list of connected clients
        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        ulong[] targetClients = new ulong[connectedClients.Count];
        var clientCount = 0;
        var hostClient = false;

        //load the clients into an array, skipping client 0 (server/host) as we want that loaded LAST
        foreach (var client in connectedClients)
        {
            if (client.Key == 0)
                hostClient = true;
            else
                targetClients[clientCount++] = client.Key;
        }

        //Set the Host (Client 0) as the LAST in the list if there was one.
        //If not, then we were just running in pure server mode which does not have its own client
        if (hostClient)
            targetClients[clientCount] = 0;

        //Now for each connected non-server client, send a targeted request to disconnect
        //with a slight delay between each request
        foreach (var targetClient in targetClients)
        {
            yield return new WaitForSeconds(0.5f);
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] {targetClient}
                }
            };
            PlayersManager.Instance.RequestClientDisconnectClientRpc(clientRpcParams);
        }

        yield return null;
    }
}