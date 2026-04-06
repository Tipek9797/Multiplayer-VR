using UnityEngine;
using Unity.Netcode;

public class OfflineAvatarAutoToggle : MonoBehaviour
{
    [SerializeField] private GameObject offlineAvatar;

    private void Update()
    {
        bool multiplayerRunning = false;

        if (NetworkManager.Singleton != null)
            multiplayerRunning = NetworkManager.Singleton.IsListening;

        bool shouldBeActive = !multiplayerRunning;

        if (offlineAvatar != null && offlineAvatar.activeSelf != shouldBeActive)
            offlineAvatar.SetActive(shouldBeActive);
    }
}