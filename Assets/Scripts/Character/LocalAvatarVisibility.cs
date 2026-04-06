using UnityEngine;
using Unity.Netcode;

public class LocalAvatarVisibility : MonoBehaviour
{
    [Header("Renderers hidden for the owning player")]
    [SerializeField] private Renderer[] hideForLocalPlayer;

    private NetworkObject parentNetworkObject;

    private void Awake()
    {
        parentNetworkObject = GetComponentInParent<NetworkObject>(true);
    }

    private void OnEnable()
    {
        ApplyVisibility();
    }

    public void ApplyVisibility()
    {
        if (hideForLocalPlayer == null || hideForLocalPlayer.Length == 0)
            return;

        if (parentNetworkObject == null)
        {
            parentNetworkObject = GetComponentInParent<NetworkObject>(true);
            if (parentNetworkObject == null)
            {
                return;
            }
        }

        bool isLocalOwner = parentNetworkObject.IsOwner;

        foreach (Renderer r in hideForLocalPlayer)
        {
            if (r != null)
                r.enabled = !isLocalOwner;
        }
    }
}