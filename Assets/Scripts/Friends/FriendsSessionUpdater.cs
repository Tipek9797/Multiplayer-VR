using System;
using System.Collections;
using UnityEngine;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;

namespace XRMultiplayer
{
    public class FriendSessionUpdater : MonoBehaviour
    {
        [SerializeField] private float checkInterval = 2f;

        private string lastSessionId = "";
        private bool hadSessionBefore = false;

        private void Start()
        {
            StartCoroutine(CheckSessionLoop());
        }

        private IEnumerator CheckSessionLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkInterval);
                UpdateFriendSession();
            }
        }

        private async void UpdateFriendSession()
        {
            try
            {
                if (!FriendsBootstrap.IsFriendsReady)
                    return;

                if (XRINetworkGameManager.Instance == null ||
                    XRINetworkGameManager.Instance.sessionManager == null)
                    return;

                var currentSession = XRINetworkGameManager.Instance.sessionManager.currentSession;
                bool hasSessionNow = currentSession != null;
                string currentSessionId = hasSessionNow ? currentSession.Id : string.Empty;

                if (hasSessionNow == hadSessionBefore && currentSessionId == lastSessionId)
                    return;

                var activity = new FriendSessionActivity
                {
                    sessionId = currentSessionId
                };

                await FriendsService.Instance.SetPresenceAsync(Availability.Online, activity);

                hadSessionBefore = hasSessionNow;
                lastSessionId = currentSessionId;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private async void OnDestroy()
        {
            try
            {
                if (!FriendsBootstrap.IsFriendsReady)
                    return;

                await FriendsService.Instance.SetPresenceAsync(
                    Availability.Online,
                    new FriendSessionActivity { sessionId = string.Empty }
                );
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}