using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Vivox;

namespace XRMultiplayer
{
    public class FriendChatManager : MonoBehaviour
    {
        public static FriendChatManager Instance;

        [SerializeField] private ChatWindowView chatWindowView;
        [SerializeField] private int historyRequestSize = 50;
        [SerializeField] private float waitForVivoxLoginSeconds = 15f;

        private readonly ChatHistoryStore chatSstore = new();

        private readonly Dictionary<string, string> lastReadMessageIds = new();
        private readonly HashSet<string> readingInProgress = new();

        private string currentTargetPlayerId;
        private string currentTargetDisplayName;

        private bool isSubscribedToVivox;
        private Coroutine subscribeRoutine;

        private int historyLoadVersion;
        private bool isHistoryLoading;

        public event Action<string, bool> UnreadStateChanged
        {
            add => chatSstore.UnreadStateChanged += value;
            remove => chatSstore.UnreadStateChanged -= value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (chatWindowView != null)
            {
                if (chatWindowView.SendButton != null)
                    chatWindowView.SendButton.onClick.AddListener(SendMessage);

                if (chatWindowView.CloseButton != null)
                    chatWindowView.CloseButton.onClick.AddListener(CloseChat);

                if (chatWindowView.InputField != null)
                    chatWindowView.InputField.onSubmit.AddListener(OnInputSubmit);

                chatWindowView.ShowFriends();
                chatWindowView.ClearInput();
                RefreshSendButtonState();
            }

            StartVivoxSubscribeRoutine();
        }

        private void Update()
        {
            RefreshSendButtonState();
        }

        private void OnEnable()
        {
            StartVivoxSubscribeRoutine();
            RefreshSendButtonState();
        }

        private void OnDisable()
        {
            UnsubscribeFromVivox();
        }

        private void OnDestroy()
        {
            if (chatWindowView != null)
            {
                if (chatWindowView.SendButton != null)
                    chatWindowView.SendButton.onClick.RemoveListener(SendMessage);

                if (chatWindowView.CloseButton != null)
                    chatWindowView.CloseButton.onClick.RemoveListener(CloseChat);

                if (chatWindowView.InputField != null)
                    chatWindowView.InputField.onSubmit.RemoveListener(OnInputSubmit);
            }

            if (subscribeRoutine != null)
            {
                StopCoroutine(subscribeRoutine);
                subscribeRoutine = null;
            }

            UnsubscribeFromVivox();
        }

        public void OpenChat(string playerId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(playerId) || chatWindowView == null)
                return;

            if (!string.IsNullOrWhiteSpace(currentTargetPlayerId) && currentTargetPlayerId != playerId)
                chatSstore.SetConversationOpen(currentTargetPlayerId, false);

            currentTargetPlayerId = playerId;
            currentTargetDisplayName = string.IsNullOrWhiteSpace(displayName) ? playerId : displayName;
            historyLoadVersion++;

            chatSstore.SetConversationOpen(playerId, true);
            chatSstore.SetUnreadState(playerId, false);

            chatWindowView.SetHeader(currentTargetDisplayName);
            chatWindowView.ShowChat();
            chatWindowView.ClearInput();
            chatWindowView.FocusInput();

            chatWindowView.LoadMessages(chatSstore.GetHistory(playerId));
            chatWindowView.ScrollToBottom();

            _ = LoadChatHistoryAsync(playerId, historyLoadVersion);
        }

        public void CloseChat()
        {
            if (!string.IsNullOrWhiteSpace(currentTargetPlayerId))
                chatSstore.SetConversationOpen(currentTargetPlayerId, false);

            if (chatWindowView != null)
            {
                chatWindowView.ShowFriends();
                chatWindowView.ClearInput();
                chatWindowView.DeselectInput();
                chatWindowView.ClearRenderedMessages();
            }

            currentTargetPlayerId = null;
            currentTargetDisplayName = null;
            historyLoadVersion++;
        }

        public void ForceCloseChatToFriends()
        {
            CloseChat();
        }

        public bool HasUnreadMessages(string playerId)
        {
            return chatSstore.HasUnreadMessages(playerId);
        }

        public async Task UpdateUnreadFromHistoryAsync(IEnumerable<string> friendPlayerIds, int requestSizePerFriend = 10)
        {
            if (friendPlayerIds == null)
                return;

            if (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
                return;

            string myPlayerId = AuthenticationService.Instance != null
                ? AuthenticationService.Instance.PlayerId
                : null;

            foreach (string friendPlayerId in friendPlayerIds)
            {
                if (string.IsNullOrWhiteSpace(friendPlayerId))
                    continue;

                try
                {
                    var history = await VivoxService.Instance.GetDirectTextMessageHistoryAsync(friendPlayerId, requestSizePerFriend);

                    bool hasUnread = false;

                    if (history != null)
                    {
                        foreach (var message in history.OrderBy(m => m.ReceivedTime))
                        {
                            if (message == null || string.IsNullOrWhiteSpace(message.MessageText))
                                continue;

                            bool isMine = message.FromSelf;

                            if (!isMine &&
                                !string.IsNullOrWhiteSpace(myPlayerId) &&
                                message.SenderPlayerId == myPlayerId)
                            {
                                isMine = true;
                            }

                            if (isMine)
                                continue;

                            if (chatSstore.IsConversationOpen(friendPlayerId))
                                continue;

                            if (!string.IsNullOrWhiteSpace(message.MessageId) &&
                                lastReadMessageIds.TryGetValue(friendPlayerId, out string lastReadId) &&
                                string.Equals(lastReadId, message.MessageId, StringComparison.Ordinal))
                            {
                                hasUnread = false;
                                continue;
                            }

                            if (!message.IsRead)
                            {
                                hasUnread = true;
                            }
                        }
                    }

                    chatSstore.SetUnreadState(friendPlayerId, hasUnread);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);
                }
            }
        }

        public string GetDisplayName(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return string.Empty;

            if (FriendsService.Instance == null || FriendsService.Instance.Friends == null)
                return playerId;

            var relationship = FriendsService.Instance.Friends
                .FirstOrDefault(r => r.Member != null && r.Member.Id == playerId);

            if (relationship != null &&
                relationship.Member.Profile != null &&
                !string.IsNullOrWhiteSpace(relationship.Member.Profile.Name))
            {
                return relationship.Member.Profile.Name;
            }

            return playerId;
        }

        private void OnInputSubmit(string submittedText)
        {
            if (!string.IsNullOrWhiteSpace(submittedText))
                SendMessage();
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(currentTargetPlayerId))
                return;

            if (chatWindowView == null || chatWindowView.InputField == null)
                return;

            string textToSend = chatWindowView.InputField.text.Trim();
            if (string.IsNullOrWhiteSpace(textToSend))
                return;

            if (VivoxService.Instance == null)
            {
                AddSystemMessage("Chat service missing.");
                return;
            }

            if (!VivoxService.Instance.IsLoggedIn)
            {
                AddSystemMessage("Chat is not ready yet.");
                return;
            }

            string targetPlayerId = currentTargetPlayerId;
            string tempLocalId = "local_" + Guid.NewGuid().ToString("N");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            chatSstore.AddLocalPendingMessage(targetPlayerId, textToSend, tempLocalId, now);

            chatWindowView.ClearInput();
            chatWindowView.DeselectInput();

            if (IsChatOpen(targetPlayerId))
            {
                chatWindowView.AddNewMessages(chatSstore.GetHistory(targetPlayerId));
                chatWindowView.ScrollToBottom();
            }

            try
            {
                await VivoxService.Instance.SendDirectTextMessageAsync(targetPlayerId, textToSend);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                chatSstore.MarkPendingMessageFailed(targetPlayerId, tempLocalId);

                if (IsChatOpen(targetPlayerId))
                {
                    chatWindowView.LoadMessages(chatSstore.GetHistory(targetPlayerId));
                    chatWindowView.ScrollToBottom();
                }
            }
        }

        private async Task LoadChatHistoryAsync(string playerId, int loadVersion)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            if (isHistoryLoading)
                return;

            isHistoryLoading = true;

            try
            {
                bool vivoxReady = await WaitForVivoxLoginAsync(waitForVivoxLoginSeconds);
                if (!vivoxReady)
                {
                    return;
                }

                if (loadVersion != historyLoadVersion || playerId != currentTargetPlayerId)
                    return;

                var historyMessages = await VivoxService.Instance.GetDirectTextMessageHistoryAsync(playerId, historyRequestSize);

                if (loadVersion != historyLoadVersion || playerId != currentTargetPlayerId)
                    return;

                var rebuiltHistory = new List<ChatMessageData>();

                if (historyMessages != null)
                {
                    for (int i = 0; i < historyMessages.Count; i++)
                    {
                        VivoxMessage message = historyMessages[i];
                        if (message == null || string.IsNullOrWhiteSpace(message.MessageText))
                            continue;

                        bool isMine = message.FromSelf;

                        if (!isMine &&
                            AuthenticationService.Instance != null &&
                            !string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerId) &&
                            message.SenderPlayerId == AuthenticationService.Instance.PlayerId)
                        {
                            isMine = true;
                        }

                        long timestamp = new DateTimeOffset(message.ReceivedTime).ToUnixTimeSeconds();

                        rebuiltHistory.Add(new ChatMessageData(
                            message.MessageId,
                            message.MessageText,
                            isMine,
                            timestamp,
                            false));
                    }

                    rebuiltHistory = rebuiltHistory
                        .OrderBy(m => m.timestamp)
                        .ThenBy(m => m.messageId ?? string.Empty)
                        .ToList();
                }

                var existingLocalHistory = chatSstore.GetHistory(playerId);
                if (existingLocalHistory != null)
                {
                    foreach (var local in existingLocalHistory.Where(m => m.isPendingLocal))
                    {
                        bool alreadyRepresented = rebuiltHistory.Any(h =>
                            h.isMine &&
                            !string.IsNullOrWhiteSpace(h.text) &&
                            h.text == local.text &&
                            Math.Abs(h.timestamp - local.timestamp) <= 120);

                        if (!alreadyRepresented)
                            rebuiltHistory.Add(local);
                    }

                    rebuiltHistory = rebuiltHistory
                        .OrderBy(m => m.timestamp)
                        .ThenBy(m => m.messageId ?? string.Empty)
                        .ToList();
                }

                chatSstore.ReplaceHistory(playerId, rebuiltHistory);

                if (IsChatOpen(playerId))
                {
                    chatWindowView.LoadMessages(chatSstore.GetHistory(playerId));
                    chatWindowView.ScrollToBottom();
                }

                VivoxMessage newestUnreadIncoming = null;

                if (historyMessages != null)
                {
                    foreach (var message in historyMessages.OrderBy(m => m.ReceivedTime))
                    {
                        if (message == null || string.IsNullOrWhiteSpace(message.MessageText))
                            continue;

                        bool isMine = message.FromSelf;

                        if (!isMine &&
                            AuthenticationService.Instance != null &&
                            !string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerId) &&
                            message.SenderPlayerId == AuthenticationService.Instance.PlayerId)
                        {
                            isMine = true;
                        }

                        if (!isMine && !message.IsRead)
                            newestUnreadIncoming = message;
                    }
                }

                bool isOpen = chatSstore.IsConversationOpen(playerId);

                if (isOpen)
                {
                    if (newestUnreadIncoming != null)
                        await MarkChatAsReadAsync(playerId, newestUnreadIncoming);

                    chatSstore.SetUnreadState(playerId, false);
                }
                else
                {
                    bool hasUnread = newestUnreadIncoming != null;

                    if (!hasUnread &&
                        lastReadMessageIds.TryGetValue(playerId, out string lastReadId) &&
                        !string.IsNullOrWhiteSpace(lastReadId))
                    {
                        hasUnread = historyMessages != null &&
                            historyMessages.Any(m =>
                                m != null &&
                                !m.FromSelf &&
                                !string.IsNullOrWhiteSpace(m.MessageId) &&
                                !string.Equals(m.MessageId, lastReadId, StringComparison.Ordinal) &&
                                !m.IsRead);
                    }

                    chatSstore.SetUnreadState(playerId, hasUnread);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                isHistoryLoading = false;
            }
        }

        private async Task<bool> WaitForVivoxLoginAsync(float timeoutSeconds)
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
                    return true;

                await Task.Delay(100);
            }

            return VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn;
        }

        private void OnDirectedMessageReceived(VivoxMessage message)
        {
            if (message == null)
                return;

            string conversationPlayerId = GetChatPlayerId(message);
            if (string.IsNullOrWhiteSpace(conversationPlayerId))
                return;

            if (string.IsNullOrWhiteSpace(message.MessageText))
                return;

            bool isMine = message.FromSelf;

            if (!isMine &&
                AuthenticationService.Instance != null &&
                !string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerId) &&
                message.SenderPlayerId == AuthenticationService.Instance.PlayerId)
            {
                isMine = true;
            }

            long timestamp = new DateTimeOffset(message.ReceivedTime).ToUnixTimeSeconds();

            bool addedOrUpdated = chatSstore.AddOrUpdateRemoteMessage(
                conversationPlayerId,
                message.MessageId,
                message.MessageText,
                isMine,
                timestamp);

            if (!addedOrUpdated)
                return;

            bool isCurrentOpenChat = IsChatOpen(conversationPlayerId);

            if (!isMine && !isCurrentOpenChat)
            {
                chatSstore.SetUnreadState(conversationPlayerId, true);
            }

            if (!isMine && isCurrentOpenChat)
            {
                chatSstore.SetUnreadState(conversationPlayerId, false);
                _ = MarkChatAsReadAsync(conversationPlayerId, message);
            }

            if (isCurrentOpenChat)
            {
                chatWindowView.AddNewMessages(chatSstore.GetHistory(conversationPlayerId));
                chatWindowView.ScrollToBottom();
            }
        }

        private string GetChatPlayerId(VivoxMessage message)
        {
            if (message == null)
                return null;

            bool isMine = message.FromSelf;

            if (!isMine &&
                AuthenticationService.Instance != null &&
                !string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerId) &&
                message.SenderPlayerId == AuthenticationService.Instance.PlayerId)
            {
                isMine = true;
            }

            if (isMine)
            {
                if (!string.IsNullOrWhiteSpace(currentTargetPlayerId))
                    return currentTargetPlayerId;
            }

            return message.SenderPlayerId;
        }

        private void AddSystemMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(currentTargetPlayerId) || string.IsNullOrWhiteSpace(text))
                return;

            chatSstore.AddSystemMessage(currentTargetPlayerId, text);

            if (IsChatOpen(currentTargetPlayerId))
            {
                chatWindowView.AddNewMessages(chatSstore.GetHistory(currentTargetPlayerId));
                chatWindowView.ScrollToBottom();
            }
        }

        private bool IsChatOpen(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                   !string.IsNullOrWhiteSpace(currentTargetPlayerId) &&
                   playerId == currentTargetPlayerId &&
                   chatWindowView != null &&
                   chatWindowView.IsChatPanelActive;
        }

        private void RefreshSendButtonState()
        {
            if (chatWindowView == null || chatWindowView.InputField == null)
                return;

            chatWindowView.SetSendButtonInteractable(
                !string.IsNullOrWhiteSpace(currentTargetPlayerId) &&
                !string.IsNullOrWhiteSpace(chatWindowView.InputField.text));
        }

        private void StartVivoxSubscribeRoutine()
        {
            if (subscribeRoutine != null)
                return;

            subscribeRoutine = StartCoroutine(WaitForVivoxAndSubscribe());
        }

        private IEnumerator WaitForVivoxAndSubscribe()
        {
            while (!isSubscribedToVivox)
            {
                if (VivoxService.Instance != null)
                {
                    VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;
                    VivoxService.Instance.DirectedMessageReceived += OnDirectedMessageReceived;
                    isSubscribedToVivox = true;
                    break;
                }

                yield return null;
            }

            subscribeRoutine = null;
        }

        private void UnsubscribeFromVivox()
        {
            if (!isSubscribedToVivox)
                return;

            if (VivoxService.Instance != null)
                VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;

            isSubscribedToVivox = false;
        }

        private async Task MarkChatAsReadAsync(string playerId, VivoxMessage message)
        {
            if (string.IsNullOrWhiteSpace(playerId) || message == null || string.IsNullOrWhiteSpace(message.MessageId))
                return;

            if (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
                return;

            if (readingInProgress.Contains(playerId))
                return;

            if (lastReadMessageIds.TryGetValue(playerId, out string existingId) &&
                string.Equals(existingId, message.MessageId, StringComparison.Ordinal))
            {
                return;
            }

            readingInProgress.Add(playerId);

            try
            {
                await VivoxService.Instance.SetMessageAsReadAsync(message);
                lastReadMessageIds[playerId] = message.MessageId;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
            finally
            {
                readingInProgress.Remove(playerId);
            }
        }
    }
}