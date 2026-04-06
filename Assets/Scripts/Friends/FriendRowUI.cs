using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XRMultiplayer
{
    public class FriendRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button chatButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Image onlineStatusDot;
        [SerializeField] private Image unreadDot;
        [SerializeField] private Color onlineColor = Color.green;
        [SerializeField] private Color offlineColor = Color.red;

        public void Setup(
            string displayText,
            bool isOnline,
            bool hasUnreadMessages,
            UnityAction onRemove,
            UnityAction onChat,
            UnityAction onJoin)
        {
            if (nameText != null)
                nameText.text = displayText;

            if (onlineStatusDot != null)
                onlineStatusDot.color = isOnline ? onlineColor : offlineColor;

            if (unreadDot != null)
                unreadDot.gameObject.SetActive(hasUnreadMessages);

            if (removeButton != null)
            {
                removeButton.onClick.RemoveAllListeners();
                if (onRemove != null)
                    removeButton.onClick.AddListener(onRemove);
            }

            if (chatButton != null)
            {
                chatButton.onClick.RemoveAllListeners();
                if (onChat != null)
                    chatButton.onClick.AddListener(onChat);
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                if (onJoin != null)
                    joinButton.onClick.AddListener(onJoin);
            }
        }
    }
}