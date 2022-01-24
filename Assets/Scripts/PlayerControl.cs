using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PlayerControl : NetworkBehaviour
{
    [SerializeField] private float spawnSyncWait = 0.25f;

    [SerializeField] private float walkSpeed = 3.5f;

    [SerializeField] private float runSpeedOffset = 2.0f;

    [SerializeField] private float rotationSpeed = 3.5f;

    [SerializeField] private Vector2 defaultInitialPositionOnPlane = new Vector2(-4, 4);

    [SerializeField] private NetworkVariable<Vector3> networkPositionDirection = new NetworkVariable<Vector3>();

    [SerializeField] private NetworkVariable<Vector3> networkRotationDirection = new NetworkVariable<Vector3>();

    [SerializeField] private NetworkVariable<PlayerState> networkPlayerState = new NetworkVariable<PlayerState>();

    [SerializeField] private NetworkVariable<Vector3> networkCurrentLocation = new NetworkVariable<Vector3>();
    
    [SerializeField] private NetworkVariable<Vector3> networkCurrentRotation = new NetworkVariable<Vector3>();

    private CharacterController characterController;

    // client caches positions
    private Vector3 oldInputPosition = Vector3.zero;
    private Vector3 oldInputRotation = Vector3.zero;
    private PlayerState oldPlayerState = PlayerState.Idle;

    private Animator animator;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (IsClient && IsOwner)
        {
            transform.position = new Vector3(
                Random.Range(defaultInitialPositionOnPlane.x, defaultInitialPositionOnPlane.y), 0,
                Random.Range(defaultInitialPositionOnPlane.x, defaultInitialPositionOnPlane.y));
            
            //Start a coroutine that updates the current position of this Client's player.
            StartCoroutine("UpdateCurrentLocation");
            
        }

        //Launch a coroutine that ensures the initial starting spawn position for non-owned instances
        //is updated for this client
        if (!IsOwner)
            StartCoroutine(SetStartingLocationForNonOwnedPlayers());
    }

    void Update()
    {
        if (IsClient && IsOwner)
        {
            ClientInput();
        }

        ClientMoveAndRotate();
        ClientVisuals();
    }
  

    private void ClientMoveAndRotate()
    {
        if (networkPositionDirection.Value != Vector3.zero)
        {
            characterController.SimpleMove(networkPositionDirection.Value);
        }

        if (networkRotationDirection.Value != Vector3.zero)
        {
            transform.Rotate(networkRotationDirection.Value, Space.World);
        }
    }

    private void ClientVisuals()
    {
        if (oldPlayerState != networkPlayerState.Value)
        {
            oldPlayerState = networkPlayerState.Value;
            animator.SetTrigger($"{networkPlayerState.Value}");
        }
    }

    private void ClientInput()
    {
        // left & right rotation
        Vector3 inputRotation = new Vector3(0, Input.GetAxis("Horizontal"), 0);

        // forward & backward direction
        Vector3 direction = transform.TransformDirection(Vector3.forward);
        float forwardInput = Input.GetAxis("Vertical");
        Vector3 inputPosition = direction * forwardInput;

        // change animation states
        if (forwardInput == 0)
            UpdatePlayerStateServerRpc(PlayerState.Idle);
        else if (!ActiveRunningActionKey() && forwardInput > 0 && forwardInput <= 1)
            UpdatePlayerStateServerRpc(PlayerState.Walk);
        else if (ActiveRunningActionKey() && forwardInput > 0 && forwardInput <= 1)
        {
            inputPosition = direction * runSpeedOffset;
            UpdatePlayerStateServerRpc(PlayerState.Run);
        }
        else if (forwardInput < 0)
            UpdatePlayerStateServerRpc(PlayerState.ReverseWalk);

        // let server know about position and rotation client changes
        if (oldInputPosition != inputPosition ||
            oldInputRotation != inputRotation)
        {
            oldInputPosition = inputPosition;
            UpdateClientPositionAndRotationServerRpc(inputPosition * walkSpeed, inputRotation * rotationSpeed);
        }

    }

    private static bool ActiveRunningActionKey()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    [ServerRpc]
    public void UpdateClientPositionAndRotationServerRpc(Vector3 newPosition, Vector3 newRotation)
    {
        networkPositionDirection.Value = newPosition;
        networkRotationDirection.Value = newRotation;
    }

    [ServerRpc]
    public void UpdatePlayerStateServerRpc(PlayerState state)
    {
        networkPlayerState.Value = state;
    }

    /// <summary>
    /// This ServerRPC method is used by the UpdateCurrentLocation coroutine to help provide this (owned) client's
    /// player current location so other players can have updated/current info for the Non-owned client/players that
    /// show up in their session.
    /// </summary>
    /// <param name="currentLocation"></param>
    /// <param name="currentRotation"></param>
    [ServerRpc]
    public void UpdatePlayerCurrentPositionAndRotationServerRpc(Vector3 currentLocation, Quaternion currentRotation)
    {
        networkCurrentLocation.Value = currentLocation;
        networkCurrentRotation.Value = currentRotation.eulerAngles;
    }
    
    /// <summary>
    /// This coroutine is started in the Start method, only IF this instance is NOT the owned client, i.e. this instance
    /// is owned by some other player. It is responsible for ensuring it's position is exactly where the real player is
    /// at the moment of startup.
    /// </summary>
    /// <returns></returns>
    IEnumerator SetStartingLocationForNonOwnedPlayers()
    {
        //This very slight delay gives the (Server/Host) enough time to relay the other non-owned player/client's
        //current location, for this just-starting player client.
        yield return new WaitForSeconds(spawnSyncWait);
        transform.position = networkCurrentLocation.Value;
        transform.rotation = Quaternion.Euler(networkCurrentRotation.Value);
    }
    
    /// <summary>
    /// This is a looping coroutine, that periodically updates the server with the (owned) client's location.
    /// This helps in ensuring that when other client's join the game (late), that they will be able to have
    /// updated info on where this (owned) client is in the game world. The information updated to the server here
    /// will be used by other clients via the 'SetStartingLocationForNonOwnedPlayers' method.
    /// </summary>
    /// <returns></returns>
    IEnumerator UpdateCurrentLocation()
    {
        //At short intervals, server sync this player's position and rotation so that it can be
        //used when other players connect to the host/server. 
        while (true)
        {
            UpdatePlayerCurrentPositionAndRotationServerRpc(transform.position, transform.rotation);
            yield return new WaitForSeconds(spawnSyncWait);
        }
    }
}