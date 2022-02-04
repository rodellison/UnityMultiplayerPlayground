using System;
using DilmerGames.Core.Singletons;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

//Note: See Unity information about the Networking 'Relay' service here:
//https://docs.unity.com/relay/introduction.htm


public class RelayManager : Singleton<RelayManager>
{
    //This field provides a way to specify which environment to use for the Relay server, allowing for different
    //environments for dev, staging, test, production, etc.
    //The values are found here, in your Unity dashboard:
    //https://dashboard.unity3d.com/organizations/<yourorgvalue>/settings/projects
    //If left column of page, under project settings is Environments
    [SerializeField] private string environment = "production";

    [SerializeField] private int maxNumberOfConnections = 10;

    public bool IsRelayEnabled =>
        Transport != null && Transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport;

    public UnityTransport Transport => NetworkManager.Singleton.gameObject.GetComponentInChildren<UnityTransport>();
//    public UnityTransport Transport => NetworkManager.Singleton.gameObject.GetComponent<UnityTransport>();

    /// <summary>
    /// The purpose of 'Setting up a Relay' is to establish a remote running Game (data relay) on Unity Cloud servers
    /// that will handle 'relaying' game data to internet connected clients. There is still the premise that
    /// one player is the host, and the others are clients, the relay just facilitates data movement from a central
    /// location.
    /// </summary>
    /// <returns>RelayHostData is returned after this awaitable task completes, containing information about the
    /// newly 'allocated' Unity cloud server instance for this game. Contained within the RelayHostData is a 'joinCode'
    /// that can be shared by the host to other clients who want to join this specific game.</returns>
    public async Task<RelayHostData> SetupRelay()
    {
        //The steps in having a Host set up a Unity hosted remote (data relay) for a new game instance is:
        //1. Indicate the environment the instance we want the game relay to run in and wait until UnityServices
        //completes that asynchronous initialization.
        //2. Authenticate to Unity Services
        //3. Send an allocation request (i.e. request for a new instance of a game data relay to be setup) on
        //Unity Cloud servers and await for the request to be completed. Use the response to populate the
        //RelayHostData data struct with the key information about the allocation (i.e. IP, Port, ID, etc. - all the info
        //about where the game data relay will run on Unity cloud servers.
        //4. Use the Allocation ID to get a sharable 'Join Code'
        //5. Update the Transport component (in the NetworkManager game object) with all the key allocation data.

        Logger.Instance.LogInfo($"Relay Server Starting With Max Connections: {maxNumberOfConnections}");


        //1.
        InitializationOptions options = new InitializationOptions()
            .SetEnvironmentName(environment);
        await UnityServices.InitializeAsync(options);

        //2.
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

 

        try
        {
            //3.
            Allocation allocation = await Relay.Instance.CreateAllocationAsync(maxNumberOfConnections);
            RelayHostData relayHostData = new RelayHostData
            {
                Key = allocation.Key,
                Port = (ushort) allocation.RelayServer.Port,
                AllocationID = allocation.AllocationId,
                AllocationIDBytes = allocation.AllocationIdBytes,
                IPv4Address = allocation.RelayServer.IpV4,
                ConnectionData = allocation.ConnectionData
            };
            
            //4.
            relayHostData.JoinCode = await Relay.Instance.GetJoinCodeAsync(relayHostData.AllocationID);

            //5.
            Transport.SetRelayServerData(relayHostData.IPv4Address, relayHostData.Port, relayHostData.AllocationIDBytes,
                relayHostData.Key, relayHostData.ConnectionData);
            
            Logger.Instance.LogInfo($"Relay Server Generated Join Code: {relayHostData.JoinCode}");
            return relayHostData;

        }
        catch (Exception e)
        {
            Logger.Instance.LogError($"RelayManager SetupRelay exception: {e.Message}");
        }

        return new RelayHostData();
    }

    public async Task<RelayJoinData> JoinRelay(string joinCode)
    {
        //The steps in having a Client join a specific remote Unity data relay instance is:
        //1. Indicate the environment the instance of the game data relay we want to join is running and wait until
        //UnityServices completes that asynchronous initialization.
        //2. Authenticate to Unity Services
        //3. Get the allocation information (i.e. a game instance data relay is running on Unity Cloud Servers, and the
        //client needs to know the allocation's information details so it's local NetworkManager transport can know
        //the IP/Port/ID, etc to connect to. The JoinCode is all that the Client has. Use the response to populate the
        //RelayHostData data struct.
        //4. Update the Transport component (in the NetworkManager game object) with all the key allocation data
        //so it knows where to send its data.

        Logger.Instance.LogInfo($"Client Joining Game With Join Code: {joinCode}");

        //1.
        InitializationOptions options = new InitializationOptions()
            .SetEnvironmentName(environment);
        await UnityServices.InitializeAsync(options);

        //2.
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }


        try
        {
            //3.
            JoinAllocation allocation = await Relay.Instance.JoinAllocationAsync(joinCode);
            RelayJoinData relayJoinData = new RelayJoinData
            {
                Key = allocation.Key,
                Port = (ushort) allocation.RelayServer.Port,
                AllocationID = allocation.AllocationId,
                AllocationIDBytes = allocation.AllocationIdBytes,
                ConnectionData = allocation.ConnectionData,
                HostConnectionData = allocation.HostConnectionData,
                IPv4Address = allocation.RelayServer.IpV4,
                JoinCode = joinCode
            };
            //4.
            Transport.SetRelayServerData(relayJoinData.IPv4Address, relayJoinData.Port, relayJoinData.AllocationIDBytes,
                relayJoinData.Key, relayJoinData.ConnectionData, relayJoinData.HostConnectionData);

            Logger.Instance.LogInfo($"Client Joined Game With Join Code: {joinCode}");
            return relayJoinData;
        }
        catch (Exception e)
        {
            Logger.Instance.LogError($"RelayManager JoinRelay exception: {e.Message}");
        }

        return new RelayJoinData();
    }
}