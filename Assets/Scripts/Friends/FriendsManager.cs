using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using System.Linq;

namespace XRMultiplayer
{
    public class FriendsManager : MonoBehaviour
    {
        [SerializeField] private TMP_InputField playerIdInput;

        [SerializeField] private Button sendRequestButton;

        [SerializeField] private TMP_Text addFriendStatusText;

        [SerializeField] private Button refreshButton;

        [SerializeField] private Transform friendsListRoot;

        [SerializeField] private Transform incomingListRoot;

        [SerializeField] private Transform outgoingListRoot;

        [SerializeField] private TMP_Text relationshipsStatusText;

        [SerializeField] private FriendRowUI friendRowPrefab;

        [SerializeField] private IncomingRequestRowUI incomingRequestRowPrefab;

        [SerializeField] private OutgoingRequestRowUI outgoingRequestRowPrefab;

        [SerializeField] private bool autoRefresh = true;

        [SerializeField] private float autoRefreshInterval = 60f;

        private bool isInitialized;

        private bool isRefreshing;

        private bool eventsRegistered;

        private FriendsEventConnectionState currentConnectionState = FriendsEventConnectionState.Unsynced;

        private void Start()
        {
            if (sendRequestButton != null)
                sendRequestButton.onClick.AddListener(OnSendFriendRequestClicked);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshFriends);

            SetAddFriendStatus("Waiting for Friends...");
            SetRelationshipsStatus("Waiting for Friends...");

            StartCoroutine(InitFriends());
        }
        
        private IEnumerator InitFriends()
        {
            while (!FriendsBootstrap.IsFriendsReady)
                yield return null;

            RegisterFriendsEvents();

            if (FriendChatManager.Instance != null)
                FriendChatManager.Instance.UnreadStateChanged += OnUnreadStateChanged;

            isInitialized = true;

            SetAddFriendStatus("Ready");
            SetRelationshipsStatus("Ready");

            RefreshFriends();

            if (autoRefresh)
                InvokeRepeating(nameof(RefreshFriends), autoRefreshInterval, autoRefreshInterval);
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(RefreshFriends));

            if (sendRequestButton != null)
                sendRequestButton.onClick.RemoveListener(OnSendFriendRequestClicked);

            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(RefreshFriends);

            if (FriendChatManager.Instance != null)
                FriendChatManager.Instance.UnreadStateChanged -= OnUnreadStateChanged;
        }

        private void OnUnreadStateChanged(string playerId, bool hasUnread)
        {
            RefreshFriends();
        }

        private void RegisterFriendsEvents()
        {
            if (eventsRegistered)
                return;

            try
            {
                FriendsService.Instance.RelationshipAdded += e => RefreshFriends();
                FriendsService.Instance.MessageReceived += e => RefreshFriends();
                FriendsService.Instance.PresenceUpdated += e => RefreshFriends();
                FriendsService.Instance.RelationshipDeleted += e => RefreshFriends();

                FriendsService.Instance.NotificationsConnectivityChanged += e =>
                {
                    currentConnectionState = e.State;

                    if (e.State == FriendsEventConnectionState.Subscribed)
                        RefreshFriends();
                };

                eventsRegistered = true;
            }
            catch (FriendsServiceException e)
            {
                Debug.LogError(e);
            }
        }

        private async void OnSendFriendRequestClicked()
        {
            if (!isInitialized || !FriendsBootstrap.IsFriendsReady)
            {
                SetAddFriendStatus("Friends service not ready.");
                return;
            }

            string targetNickname = playerIdInput != null ? playerIdInput.text.Trim() : string.Empty;

            if (string.IsNullOrEmpty(targetNickname))
            {
                SetAddFriendStatus("Enter a public nickname with #numbers.");
                return;
            }

            try
            {
                string myPublicName = await AuthenticationService.Instance.GetPlayerNameAsync();

                if (!string.IsNullOrEmpty(myPublicName) &&
                    string.Equals(targetNickname, myPublicName, StringComparison.OrdinalIgnoreCase))
                {
                    SetAddFriendStatus("You cannot add yourself.");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }

            try
            {
                SetAddFriendStatus("Sending request...");
                await FriendsService.Instance.AddFriendByNameAsync(targetNickname);
                SetAddFriendStatus($"Request sent to:\n{targetNickname}");
                await RefreshAfterDelay();
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("409"))
                    SetAddFriendStatus("Already friends or request already exists.");
                else
                    SetAddFriendStatus("Send failed. Check the full nickname.");

                await RefreshAfterDelay();
            }
        }

        public async void RefreshFriends()
        {
            if (!isInitialized || !FriendsBootstrap.IsFriendsReady || isRefreshing)
                return;

            isRefreshing = true;

            try
            {
                SetRelationshipsStatus("Refreshing...");

                await FriendsService.Instance.ForceRelationshipsRefreshAsync();
                await Task.Delay(150);

                var friends = FriendsService.Instance.Friends;
                var incoming = FriendsService.Instance.IncomingFriendRequests;
                var outgoing = FriendsService.Instance.OutgoingFriendRequests;

                if (FriendChatManager.Instance != null && friends != null)
                {
                    var friendIds = friends
                        .Where(r => r != null && r.Member != null && !string.IsNullOrWhiteSpace(r.Member.Id))
                        .Select(r => r.Member.Id)
                        .ToList();

                    await FriendChatManager.Instance.UpdateUnreadFromHistoryAsync(friendIds, 10);
                }

                RebuildFriendsList(friends);
                RebuildIncomingList(incoming);
                RebuildOutgoingList(outgoing);

                SetRelationshipsStatus("Refresh complete.");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                SetRelationshipsStatus("Refresh failed.");
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private void RebuildFriendsList(IReadOnlyList<Relationship> friends)
        {
            ClearChildren(friendsListRoot);

            if (friendsListRoot == null)
                return;

            if (friendRowPrefab == null)
                return;

            if (friends == null || friends.Count == 0)
                return;

            foreach (var relationship in friends)
            {
                string id = GetMemberId(relationship);
                string displayName = GetDisplayName(relationship);
                bool isOnline = IsFriendOnline(relationship);
                bool hasUnread = FriendChatManager.Instance != null && FriendChatManager.Instance.HasUnreadMessages(id);

                var row = Instantiate(friendRowPrefab, friendsListRoot);
                row.Setup(
                    displayName,
                    isOnline,
                    hasUnread,
                    () => RemoveFriend(id),
                    () => OpenFriendChat(id),
                    () => JoinFriend(id)
                );
            }
        }

        private bool IsFriendOnline(Relationship relationship)
        {
            if (relationship == null || relationship.Member == null || relationship.Member.Presence == null)
                return false;

            return relationship.Member.Presence.Availability == Availability.Online;
        }

        private void RebuildIncomingList(IReadOnlyList<Relationship> incoming)
        {
            ClearChildren(incomingListRoot);

            if (incomingListRoot == null || incomingRequestRowPrefab == null || incoming == null)
                return;

            foreach (var relationship in incoming)
            {
                string id = GetMemberId(relationship);
                string displayName = GetDisplayName(relationship);

                var row = Instantiate(incomingRequestRowPrefab, incomingListRoot);
                row.Setup(displayName, () => AcceptFriend(id), () => DenyFriend(id));
            }
        }

        private void RebuildOutgoingList(IReadOnlyList<Relationship> outgoing)
        {
            ClearChildren(outgoingListRoot);

            if (outgoingListRoot == null || outgoingRequestRowPrefab == null || outgoing == null)
                return;

            foreach (var relationship in outgoing)
            {
                string id = GetMemberId(relationship);
                string displayName = GetDisplayName(relationship);

                var row = Instantiate(outgoingRequestRowPrefab, outgoingListRoot);
                row.Setup(displayName, () => CancelOutgoingRequest(id));
            }
        }

        private string GetMemberId(Relationship relationship)
        {
            return relationship != null && relationship.Member != null
                ? relationship.Member.Id
                : "Unknown";
        }

        private string GetDisplayName(Relationship relationship)
        {
            if (relationship == null || relationship.Member == null)
                return "Unknown";

            if (relationship.Member.Profile != null &&
                !string.IsNullOrWhiteSpace(relationship.Member.Profile.Name))
                return relationship.Member.Profile.Name;

            return relationship.Member.Id;
        }

        private void ClearChildren(Transform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private async void AcceptFriend(string playerId)
        {
            try
            {
                SetRelationshipsStatus($"Accepting {playerId}...");
                await FriendsService.Instance.AddFriendAsync(playerId);
                SetRelationshipsStatus($"Accepted {playerId}");
                await RefreshAfterDelay();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                SetRelationshipsStatus("Accept failed.");
            }
        }

        private async void DenyFriend(string playerId)
        {
            try
            {
                SetRelationshipsStatus($"Denying {playerId}...");
                await FriendsService.Instance.DeleteIncomingFriendRequestAsync(playerId);
                SetRelationshipsStatus($"Denied {playerId}");
                await RefreshAfterDelay();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                SetRelationshipsStatus("Deny failed.");
            }
        }

        private async void RemoveFriend(string playerId)
        {
            try
            {
                SetRelationshipsStatus($"Removing {playerId}...");
                await FriendsService.Instance.DeleteFriendAsync(playerId);
                SetRelationshipsStatus($"Removed {playerId}");
                await RefreshAfterDelay();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                SetRelationshipsStatus("Remove failed.");
            }
        }

        private async void CancelOutgoingRequest(string playerId)
        {
            try
            {
                SetRelationshipsStatus($"Canceling {playerId}...");
                await FriendsService.Instance.DeleteOutgoingFriendRequestAsync(playerId);
                SetRelationshipsStatus($"Canceled {playerId}");
                await RefreshAfterDelay();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                SetRelationshipsStatus("Cancel failed.");
            }
        }

        private void OpenFriendChat(string playerId)
        {
            var relationship = FriendsService.Instance.Friends
                .FirstOrDefault(r => r.Member != null && r.Member.Id == playerId);

            string displayName = relationship.Member.Profile != null &&
                                 !string.IsNullOrWhiteSpace(relationship.Member.Profile.Name)
                ? relationship.Member.Profile.Name
                : playerId;

            if (FriendChatManager.Instance == null)
            {
                SetRelationshipsStatus("Chat manager not found.");
                return;
            }

            FriendChatManager.Instance.OpenChat(playerId, displayName);
        }

        private void JoinFriend(string playerId)
        {
            try
            {
                var relationship = FriendsService.Instance.Friends
                    .FirstOrDefault(r => r.Member != null && r.Member.Id == playerId);

                if (relationship == null)
                {
                    SetRelationshipsStatus("Friend not found.");
                    return;
                }

                var presence = relationship.Member.Presence;
                if (presence == null)
                {
                    SetRelationshipsStatus("Friend has no presence.");
                    return;
                }

                FriendSessionActivity activity = null;

                try
                {
                    activity = presence.GetActivity<FriendSessionActivity>();
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);
                }

                if (activity == null || string.IsNullOrWhiteSpace(activity.sessionId))
                {
                    SetRelationshipsStatus("Friend is not in a joinable room.");
                    return;
                }

                SetRelationshipsStatus("Joining friend's room...");
                XRINetworkGameManager.Instance.JoinLobbyBySessionId(activity.sessionId);
            }
             catch (Exception e)
            {
                Debug.LogError(e);
                SetRelationshipsStatus("Join failed.");
            }
        }

        private async Task RefreshAfterDelay()
        {
            await Task.Delay(150);
            RefreshFriends();
        }

        private void SetAddFriendStatus(string message)
        {
            if (addFriendStatusText != null)
                addFriendStatusText.text = message;
        }

        private void SetRelationshipsStatus(string message)
        {
            if (relationshipsStatusText != null)
                relationshipsStatusText.text = message;
        }
    }
}