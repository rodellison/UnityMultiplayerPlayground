using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerHud : NetworkBehaviour
{
    [SerializeField]
    private NetworkVariable<NetworkString> playerNetworkName = new NetworkVariable<NetworkString>();

    private bool overlaySet = false;

    public override void OnNetworkSpawn()
    {
        if(IsServer)
        {
            playerNetworkName.Value = $"Player {OwnerClientId}";
        }
    }

    IEnumerator SetOverlay()
    {
        overlaySet = true;
        
        yield return new WaitForSeconds(0.5f);
        var localPlayerOverlay = gameObject.GetComponentInChildren<TextMeshProUGUI>();
        localPlayerOverlay.text = $"{playerNetworkName.Value}";
        
        //Attempt to match the COLOR of the text to what was set as the BODY color, when the 
        //PlayerControl script started.
        localPlayerOverlay.color =
            gameObject.transform.root.GetComponentInChildren<SkinnedMeshRenderer>().materials[0].color;
        
        //If this hud is being displayed for a non-owned client, then
        //rotate it 180 degrees so it appears correct to other players
         if (!IsOwner)
            localPlayerOverlay.gameObject.transform.parent.transform.Rotate(Vector3.up, 180f);
        
        
    }

    public void Update()
    {
        if(!overlaySet && !string.IsNullOrEmpty(playerNetworkName.Value))
        {
            StartCoroutine(SetOverlay());
        }
    }
}
