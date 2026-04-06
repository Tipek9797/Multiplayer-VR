using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XRMultiplayer
{
    public class ChatWindowView : MonoBehaviour
    {
        [SerializeField] private GameObject friendsPanel;
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private Transform messagesRoot;
        [SerializeField] private ScrollRect messagesScrollRect;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject myMessagePrefab;
        [SerializeField] private GameObject otherMessagePrefab;

        private Coroutine scrollToBottomRoutine;
        private int renderedMessageCount;

        public TMP_InputField InputField => inputField;
        public Button SendButton => sendButton;
        public Button CloseButton => closeButton;
        public bool IsChatPanelActive => chatPanel != null && chatPanel.activeInHierarchy;

        public void SetHeader(string text)
        {
            if (headerText != null)
                headerText.text = text;
        }

        public void ShowChat()
        {
            if (friendsPanel != null)
                friendsPanel.SetActive(false);

            if (chatPanel != null)
                chatPanel.SetActive(true);
        }

        public void ShowFriends()
        {
            if (chatPanel != null)
                chatPanel.SetActive(false);

            if (friendsPanel != null)
                friendsPanel.SetActive(true);
        }

        public void ClearInput()
        {
            if (inputField != null)
                inputField.text = string.Empty;
        }

        public void FocusInput()
        {
            if (inputField != null)
                inputField.ActivateInputField();
        }

        public void DeselectInput()
        {
            if (inputField != null)
                inputField.DeactivateInputField();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void SetSendButtonInteractable(bool value)
        {
            if (sendButton != null)
                sendButton.interactable = value;
        }

        public void LoadMessages(List<ChatMessageData> history)
        {
            ClearRenderedMessages();

            if (history == null)
                return;

            for (int i = 0; i < history.Count; i++)
                SpawnMessageBubble(history[i]);

            renderedMessageCount = history.Count;
        }

        public void AddNewMessages(List<ChatMessageData> history)
        {
            if (history == null)
                return;

            if (renderedMessageCount > history.Count)
            {
                LoadMessages(history);
                return;
            }

            for (int i = renderedMessageCount; i < history.Count; i++)
                SpawnMessageBubble(history[i]);

            renderedMessageCount = history.Count;
        }

        public void ClearRenderedMessages()
        {
            if (messagesRoot == null)
                return;

            for (int i = messagesRoot.childCount - 1; i >= 0; i--)
                Destroy(messagesRoot.GetChild(i).gameObject);

            renderedMessageCount = 0;
        }

        public void ScrollToBottom()
        {
            if (messagesScrollRect == null)
                return;

            if (scrollToBottomRoutine != null)
                StopCoroutine(scrollToBottomRoutine);

            scrollToBottomRoutine = StartCoroutine(ScrollToBottomCoroutine());
        }

        private IEnumerator ScrollToBottomCoroutine()
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            Canvas.ForceUpdateCanvases();

            if (messagesScrollRect != null)
                messagesScrollRect.verticalNormalizedPosition = 0f;

            yield return null;
            yield return new WaitForEndOfFrame();

            Canvas.ForceUpdateCanvases();

            if (messagesScrollRect != null)
                messagesScrollRect.verticalNormalizedPosition = 0f;

            scrollToBottomRoutine = null;
        }

        private void SpawnMessageBubble(ChatMessageData data)
        {
            if (messagesRoot == null)
                return;

            GameObject prefabToUse = data.isMine ? myMessagePrefab : otherMessagePrefab;
            if (prefabToUse == null)
                return;

            GameObject instance = Instantiate(prefabToUse, messagesRoot);
            TMP_Text textComponent = instance.GetComponentInChildren<TMP_Text>();

            if (textComponent != null)
                textComponent.text = data.text;
        }
    }
}