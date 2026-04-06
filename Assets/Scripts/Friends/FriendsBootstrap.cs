using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Notifications;

namespace XRMultiplayer
{
    public class FriendsBootstrap : MonoBehaviour
    {
        public static bool IsFriendsReady { get; private set; }
        public static FriendsEventConnectionState CurrentConnectionState { get; private set; } =
            FriendsEventConnectionState.Unsynced;

        [SerializeField] private float checkInterval = 0.5f;
        [SerializeField] private int retryCount = 3;

        private bool isInitializing;

        private void Start()
        {
            StartCoroutine(InitFlow());
        }

        private IEnumerator InitFlow()
        {
            while (UnityServices.State != ServicesInitializationState.Initialized ||
                   !AuthenticationService.Instance.IsSignedIn ||
                   string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
            {
                yield return new WaitForSeconds(checkInterval);
            }

            var task = InitializeWithRetry();
            while (!task.IsCompleted)
                yield return null;
        }

        private async Task InitializeWithRetry()
        {
            if (IsFriendsReady || isInitializing)
                return;

            isInitializing = true;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    await FriendsService.Instance.InitializeAsync();

                    FriendsService.Instance.NotificationsConnectivityChanged += OnConnectivityChanged;

                    IsFriendsReady = true;

                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    await Task.Delay(1000);
                }
            }

            isInitializing = false;
        }

        private void OnConnectivityChanged(INotificationsStateChangedEvent e)
        {
            CurrentConnectionState = e.State;
        }
    }
}