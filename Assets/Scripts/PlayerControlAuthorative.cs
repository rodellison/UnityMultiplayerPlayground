using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Samples;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ClientNetworkTransform))]
public class PlayerControlAuthorative : NetworkBehaviour
{
    [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;
    private Material skinMeshBodyMat;
    
    [SerializeField] private float spawnSyncWait = 0.25f;

    [SerializeField]
    private float walkSpeed = 3.5f;

    [SerializeField]
    private float runSpeedOffset = 2.0f;

    [SerializeField]
    private float rotationSpeed = 3.5f;

    [SerializeField]
    private Vector2 defaultInitialPositionOnPlane = new Vector2(-4, 4);

    [SerializeField]
    private NetworkVariable<PlayerState> networkPlayerState = new NetworkVariable<PlayerState>();

    [SerializeField] private NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>();

    private CharacterController characterController;

    private Animator animator;

    // client caches animation states
    private PlayerState oldPlayerState = PlayerState.Idle;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        skinMeshBodyMat = _skinnedMeshRenderer.materials[0];
    }

    void Start()
    {
        if (IsClient && IsOwner)
        {
            transform.position = new Vector3(Random.Range(defaultInitialPositionOnPlane.x, defaultInitialPositionOnPlane.y), 0,
                   Random.Range(defaultInitialPositionOnPlane.x, defaultInitialPositionOnPlane.y));
            PlayerCameraFollow.Instance.FollowPlayer(transform.Find("PlayerCameraRoot"));
            
            //Update the player with a random color, and share it with the server so that the other clients
            //know this Owner players color.
            var colorToUse = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
            skinMeshBodyMat.SetColor("_Color", colorToUse);
            UpdatePlayerColorServerRpc(colorToUse);
        }

        if (!IsOwner)
            StartCoroutine(SetStartingForNonOwnedPlayers());
            
            
    }

    void Update()
    {
        if (IsClient && IsOwner)
        {
            ClientInput();
        }

        ClientVisuals();
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
        // y axis client rotation
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

        // client is responsible for moving itself
        characterController.SimpleMove(inputPosition * walkSpeed);
        transform.Rotate(inputRotation * rotationSpeed, Space.World);
    }
    private static bool ActiveRunningActionKey()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    [ServerRpc]
    public void UpdatePlayerStateServerRpc(PlayerState state)
    {
        networkPlayerState.Value = state;
    }
    
    /// <summary>
    /// This ServerRPC method is used to update the Color to apply to the Non owned player's Material
    /// </summary>
    [ServerRpc]
    public void UpdatePlayerColorServerRpc(Color colorToUse)
    {
        networkPlayerColor.Value = colorToUse;
    }
    
    /// <summary>
    /// This coroutine is started in the Start method, only IF this instance is NOT the owned client, i.e. this instance
    /// is owned by some other player. 
    /// </summary>
    /// <returns></returns>
    IEnumerator SetStartingForNonOwnedPlayers()
    {
        //This very slight delay gives the (Server/Host) enough time to relay the other non-owned player/client's
        //current location, for this just-starting player client.
        yield return new WaitForSeconds(spawnSyncWait);
        skinMeshBodyMat.SetColor("_Color", networkPlayerColor.Value);

    }
}
