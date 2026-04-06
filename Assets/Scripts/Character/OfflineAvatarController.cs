using UnityEngine;
using Unity.Netcode;

public class OfflineAvatarController : MonoBehaviour
{
    [Header("Offline avatars in scene")]
    [SerializeField] private GameObject femaleOfflineAvatar;
    [SerializeField] private GameObject maleOfflineAvatar;

    [Header("Character IDs")]
    [SerializeField] private string femaleCharacterId = "female";
    [SerializeField] private string maleCharacterId = "male";
    [SerializeField] private string fallbackCharacterId = "female";

    private void Start()
    {
        ApplyState();

        if (PlayerProfileManager.Instance != null)
            PlayerProfileManager.Instance.OnProfileLoaded += ApplyState;
    }

    private void OnDestroy()
    {
        if (PlayerProfileManager.Instance != null)
            PlayerProfileManager.Instance.OnProfileLoaded -= ApplyState;
    }

    private void Update()
    {
        ApplyState();
    }

    private void ApplyState()
    {
        bool multiplayerRunning = false;

        if (NetworkManager.Singleton != null)
            multiplayerRunning = NetworkManager.Singleton.IsListening;

        if (multiplayerRunning)
        {
            SetAvatarActive(femaleOfflineAvatar, false);
            SetAvatarActive(maleOfflineAvatar, false);
            return;
        }

        string characterId = fallbackCharacterId;

        if (PlayerProfileManager.Instance != null &&
            !string.IsNullOrWhiteSpace(PlayerProfileManager.Instance.CurrentCharacterId))
        {
            characterId = PlayerProfileManager.Instance.CurrentCharacterId;
        }

        bool useFemale = characterId == femaleCharacterId;
        bool useMale = characterId == maleCharacterId;

        if (!useFemale && !useMale)
        {
            useFemale = fallbackCharacterId == femaleCharacterId;
            useMale = fallbackCharacterId == maleCharacterId;
        }

        SetAvatarActive(femaleOfflineAvatar, useFemale);
        SetAvatarActive(maleOfflineAvatar, useMale);
    }

    private void SetAvatarActive(GameObject avatar, bool shouldBeActive)
    {
        if (avatar != null && avatar.activeSelf != shouldBeActive)
            avatar.SetActive(shouldBeActive);
    }
}