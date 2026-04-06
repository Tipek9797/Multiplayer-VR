using UnityEngine;

namespace XRMultiplayer
{
    public class PlayerMenuChatCloser : MonoBehaviour
    {
        [SerializeField] private FriendChatManager friendChatManager;

        private void Reset()
        {
            if (friendChatManager == null)
                friendChatManager = FindFirstObjectByType<FriendChatManager>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (friendChatManager != null)
                friendChatManager.ForceCloseChatToFriends();
        }
    }
}