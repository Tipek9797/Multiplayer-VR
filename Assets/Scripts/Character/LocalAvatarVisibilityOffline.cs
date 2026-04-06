using UnityEngine;

public class LocalAvatarVisibilityOffline : MonoBehaviour
{
    [SerializeField] private bool isLocalPlayer = true;
    [SerializeField] private Renderer[] hideForLocalPlayer;

    private void Start()
    {
        ApplyVisibility();
    }

    public void ApplyVisibility()
    {
        if (hideForLocalPlayer == null) return;

        foreach (Renderer r in hideForLocalPlayer)
        {
            if (r != null)
                r.enabled = !isLocalPlayer;
        }
    }
}