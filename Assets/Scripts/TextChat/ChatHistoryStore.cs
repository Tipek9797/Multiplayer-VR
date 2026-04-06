using System;
using System.Collections.Generic;
using System.Linq;

namespace XRMultiplayer
{
    [Serializable]
    public class ChatMessageData
    {
        public string messageId;
        public string text;
        public bool isMine;
        public long timestamp;
        public bool isPendingLocal;

        public ChatMessageData(string messageId, string text, bool isMine, long timestamp, bool isPendingLocal = false)
        {
            this.messageId = messageId;
            this.text = text;
            this.isMine = isMine;
            this.timestamp = timestamp;
            this.isPendingLocal = isPendingLocal;
        }
    }

    [Serializable]
    public class ChatHistoryStore
    {
        private readonly Dictionary<string, List<ChatMessageData>> chatHistory = new();
        private readonly HashSet<string> unreadPlayerIds = new();
        private readonly HashSet<string> openConversations = new();

        public event Action<string, bool> UnreadStateChanged;

        public bool HasUnreadMessages(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            return unreadPlayerIds.Contains(playerId);
        }

        public void SetUnreadState(string playerId, bool hasUnread)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            bool changed;

            if (hasUnread)
                changed = unreadPlayerIds.Add(playerId);
            else
                changed = unreadPlayerIds.Remove(playerId);

            if (changed)
                UnreadStateChanged?.Invoke(playerId, hasUnread);
        }

        public List<ChatMessageData> GetHistory(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return null;

            if (!chatHistory.TryGetValue(playerId, out var history))
                return null;

            return history;
        }

        public void ReplaceHistory(string playerId, List<ChatMessageData> newHistory)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            chatHistory[playerId] = newHistory ?? new List<ChatMessageData>();
            SortHistory(playerId);
        }

        public void AddLocalPendingMessage(string playerId, string text, string tempLocalId, long timestamp)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(text))
                return;

            var history = GetOrCreateHistory(playerId);

            history.Add(new ChatMessageData(
                tempLocalId,
                text,
                true,
                timestamp,
                true));

            SortHistory(playerId);
        }

        public void AddSystemMessage(string playerId, string text)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(text))
                return;

            var history = GetOrCreateHistory(playerId);

            history.Add(new ChatMessageData(
                "system_" + Guid.NewGuid().ToString("N"),
                text,
                false,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                false));

            SortHistory(playerId);
        }

        public void MarkPendingMessageFailed(string playerId, string tempLocalId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(tempLocalId))
                return;

            if (!chatHistory.TryGetValue(playerId, out var history))
                return;

            ChatMessageData msg = history.FirstOrDefault(m => m.messageId == tempLocalId);
            if (msg == null)
                return;

            msg.isPendingLocal = false;
            msg.text += "  [not sent]";
        }

        public bool AddOrUpdateRemoteMessage(
            string playerId,
            string messageId,
            string messageText,
            bool isMine,
            long timestamp)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(messageText))
                return false;

            var history = GetOrCreateHistory(playerId);

            if (!string.IsNullOrWhiteSpace(messageId) &&
                history.Any(m => m.messageId == messageId))
            {
                return false;
            }

            if (isMine)
            {
                ChatMessageData pendingMatch = history.FirstOrDefault(m =>
                    m.isMine &&
                    m.isPendingLocal &&
                    m.text == messageText &&
                    Math.Abs(m.timestamp - timestamp) <= 120);

                if (pendingMatch != null)
                {
                    pendingMatch.messageId = messageId;
                    pendingMatch.timestamp = timestamp;
                    pendingMatch.isPendingLocal = false;
                    SortHistory(playerId);
                    return true;
                }
            }

            history.Add(new ChatMessageData(
                messageId,
                messageText,
                isMine,
                timestamp,
                false));

            SortHistory(playerId);
            return true;
        }

        public void SetConversationOpen(string playerId, bool isOpen)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            if (isOpen)
                openConversations.Add(playerId);
            else
                openConversations.Remove(playerId);
        }

        public bool IsConversationOpen(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            return openConversations.Contains(playerId);
        }

        public static int SortByTime(ChatMessageData a, ChatMessageData b)
        {
            int timestampCompare = a.timestamp.CompareTo(b.timestamp);
            if (timestampCompare != 0)
                return timestampCompare;

            return string.CompareOrdinal(a.messageId ?? string.Empty, b.messageId ?? string.Empty);
        }

        private List<ChatMessageData> GetOrCreateHistory(string playerId)
        {
            if (!chatHistory.TryGetValue(playerId, out var history))
            {
                history = new List<ChatMessageData>();
                chatHistory[playerId] = history;
            }

            return history;
        }

        private void SortHistory(string playerId)
        {
            if (!chatHistory.TryGetValue(playerId, out var history))
                return;

            history.Sort(SortByTime);
        }
    }
}