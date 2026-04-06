using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Services.Vivox;
using Unity.XR.CoreUtils.Bindings.Variables;
using UnityEngine;
using UnityEngine.Android;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XRMultiplayer
{
    public class VoiceChatManager : MonoBehaviour
    {
        const string k_MicrophonePersmissionDialogue = "Microphone Permissions Required.";

        public static BindableVariable<bool> s_HasMicrophonePermission = new(false);
        public static Dictionary<string, XRINetworkPlayer> m_PlayersDictionary = new();

        public IReadOnlyBindableVariable<bool> selfMuted
        {
            get => m_SelfMuted;
        }
        readonly BindableVariable<bool> m_SelfMuted = new(false);

        public IReadOnlyBindableVariable<string> connectionStatus
        {
            get => m_ConnectionStatus;
        }
        readonly BindableVariable<string> m_ConnectionStatus = new();

        [SerializeField] ChatCapability m_ChatCapability = ChatCapability.AudioOnly;
        [SerializeField] ParticipantPropertyUpdateFrequency m_UpdateFrequency = ParticipantPropertyUpdateFrequency.TenPerSecond;

        public int AudibleDistance
        {
            get => m_AudibleDistance;
            set => m_AudibleDistance = value;
        }

        [Header("Voice Chat Properties")]
        [SerializeField] int m_AudibleDistance = 32;

        public int ConversationalDistance
        {
            get => m_ConversationalDistance;
            set => m_ConversationalDistance = value;
        }
        [SerializeField] int m_ConversationalDistance = 7;

        public float AudioFadeIntensity
        {
            get => m_AudioFadeIntensity;
            set => m_AudioFadeIntensity = value;
        }
        [SerializeField] float m_AudioFadeIntensity = 1.0f;

        public AudioFadeModel AudioFadeModel
        {
            get => m_AudioFadeModel;
            set => m_AudioFadeModel = value;
        }
        [SerializeField] AudioFadeModel m_AudioFadeModel = AudioFadeModel.LinearByDistance;

        [SerializeField] Vector2 m_MinMaxVoiceOutputVolume = new Vector2(-10.0f, 10.0f);
        [SerializeField] Vector2 m_MinMaxVoiceInputVolume = new Vector2(-10.0f, 10.0f);

        VivoxParticipant m_LocalParticpant;
        string m_CurrentLobbyId;
        bool m_ConnectedToRoom;
        bool m_IsInitialized;
        bool m_IsLoggingIn;
        bool m_IsLeaving;
        bool m_IsJoining;
        Coroutine m_RetryJoinCoroutine;

        const string k_DebugPrepend = "<color=#0CFAFA>[Voice Chat Manager]</color> ";

        private void Awake()
        {
            m_ConnectedToRoom = false;
            m_CurrentLobbyId = string.Empty;
        }

        void Start()
        {
            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
                return;

            XRINetworkGameManager.CurrentConnectionState.Subscribe(ConnectionStateUpdated);
            XRINetworkGameManager.Connected.Subscribe(ConnectedToGame);

            ConnectionStateUpdated(XRINetworkGameManager.CurrentConnectionState.Value);
            ConnectedToGame(XRINetworkGameManager.Connected.Value);
        }

        private void OnDestroy()
        {
            if (VivoxService.Instance != null)
            {
                VivoxService.Instance.LoggedIn -= LocalUserLoggedIn;
                UnbindParticipantEvents();
            }

            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
                return;

            XRINetworkGameManager.CurrentConnectionState.Unsubscribe(ConnectionStateUpdated);
            XRINetworkGameManager.Connected.Unsubscribe(ConnectedToGame);
        }

        void ConnectedToGame(bool connected)
        {
            Debug.Log("[Vivox] ConnectedToGame connected=" + connected +
                      " sessionId=" +
                      (XRINetworkGameManager.Instance != null &&
                       XRINetworkGameManager.Instance.sessionManager != null &&
                       XRINetworkGameManager.Instance.sessionManager.currentSession != null
                        ? XRINetworkGameManager.Instance.sessionManager.currentSession.Id
                        : "NULL"));

            if (!m_IsInitialized || VivoxService.Instance == null)
                return;

            if (connected)
            {
                RefreshCurrentLobbyId();

                if (!VivoxService.Instance.IsLoggedIn)
                {
                    Login();
                }
                else
                {
                    TryJoinVoiceChannelNow();
                }
            }
            else
            {
                LeaveRoomChannelOnly();
            }
        }

        void ConnectionStateUpdated(XRINetworkGameManager.ConnectionState connectionState)
        {
            Debug.Log("[Vivox] ConnectionStateUpdated = " + connectionState);

            if (!m_IsInitialized && connectionState == XRINetworkGameManager.ConnectionState.Authenticated)
            {
                m_ConnectionStatus.Value = "Initializing Voice Service";
                m_IsInitialized = true;
                EnableVoiceChat();

                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    StartCoroutine(ShowPermissionsAfterDelay());
                else
                    MicrophonePermissionGranted();
            }
        }

        IEnumerator ShowPermissionsAfterDelay(float delay = 1.0f)
        {
            PlayerHudNotification.Instance.ShowText("Requesting Microphone Permissions", 3.0f);
            yield return new WaitForSeconds(delay);

            PermissionCallbacks permissionCallbacks = new();
            permissionCallbacks.PermissionDenied += PermissionDeniedCallback;
            permissionCallbacks.PermissionGranted += PermissionGrantedCallback;
            Permission.RequestUserPermission(Permission.Microphone, permissionCallbacks);
        }

        void PermissionGrantedCallback(string permissionName)
        {
            if (permissionName == Permission.Microphone)
                MicrophonePermissionGranted();
        }

        void PermissionDeniedCallback(string permissionName)
        {
            if (permissionName == Permission.Microphone)
                PlayerHudNotification.Instance.ShowText("Microphone Permissions Denied", 3.0f);
        }

        void MicrophonePermissionGranted()
        {
            s_HasMicrophonePermission.Value = true;
            PlayerHudNotification.Instance.ShowText("Microphone Permissions Granted", 3.0f);
        }

        async void EnableVoiceChat()
        {
            try
            {
                Debug.Log("[Vivox] InitializeAsync start");
                await VivoxService.Instance.InitializeAsync();
                Debug.Log("[Vivox] InitializeAsync success");

                m_ConnectionStatus.Value = "Voice Service Initialized";
                VivoxService.Instance.LoggedIn -= LocalUserLoggedIn;
                VivoxService.Instance.LoggedIn += LocalUserLoggedIn;
                BindToParticipantEvents();

                if (XRINetworkGameManager.CurrentConnectionState.Value ==
                    XRINetworkGameManager.ConnectionState.Authenticated &&
                    !VivoxService.Instance.IsLoggedIn)
                {
                    Login();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Vivox] InitializeAsync failed: " + e);
#if UNITY_EDITOR
                EditorGUI.hyperLinkClicked += HyperlinkClicked;
                Utils.Log($"{k_DebugPrepend}Vivox Initialization Failed. Please check the Vivox Service Window <a data=\"OpenVivoxSettings\"><b>Project Settings > Services > Vivox</b></a>\n\n{e}", 2);
#else
                Utils.Log($"{k_DebugPrepend}Vivox Initialization Failed.\n\n{e}", 2);
#endif
            }
        }

#if UNITY_EDITOR
        void HyperlinkClicked(EditorWindow window, HyperLinkClickedEventArgs args)
        {
            if (args.hyperLinkData.ContainsValue("OpenVivoxSettings"))
                SettingsService.OpenProjectSettings("Project/Services/Vivox");
        }
#endif

        string GetBestVoiceChannelId()
        {
            if (XRINetworkGameManager.Instance != null &&
                XRINetworkGameManager.Instance.sessionManager != null &&
                XRINetworkGameManager.Instance.sessionManager.currentSession != null &&
                !string.IsNullOrWhiteSpace(XRINetworkGameManager.Instance.sessionManager.currentSession.Id))
            {
                return XRINetworkGameManager.Instance.sessionManager.currentSession.Id;
            }

            if (!string.IsNullOrWhiteSpace(XRINetworkGameManager.ConnectedRoomCode))
                return XRINetworkGameManager.ConnectedRoomCode;

            if (!string.IsNullOrWhiteSpace(XRINetworkGameManager.ConnectedRoomName.Value))
                return XRINetworkGameManager.ConnectedRoomName.Value;

            return string.Empty;
        }

        void RefreshCurrentLobbyId()
        {
            m_CurrentLobbyId = GetBestVoiceChannelId();
            Debug.Log("[Vivox] RefreshCurrentLobbyId -> '" + m_CurrentLobbyId + "'");
        }

        async void Login()
        {
            if (VivoxService.Instance == null)
            {
                Debug.LogError("[Vivox] Instance is null");
                return;
            }

            if (VivoxService.Instance.IsLoggedIn)
            {
                Debug.Log("[Vivox] Already logged in");
                return;
            }

            if (m_IsLoggingIn)
            {
                Debug.Log("[Vivox] Login already in progress");
                return;
            }

            var displayName = XRINetworkGameManager.AuthenicationId;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                Debug.LogError("[Vivox] AuthenticationId is empty");
                return;
            }

            RefreshCurrentLobbyId();

            LoginOptions loginOptions = new()
            {
                DisplayName = displayName,
                ParticipantUpdateFrequency = m_UpdateFrequency
            };

            try
            {
                m_IsLoggingIn = true;
                Debug.Log("[Vivox] LoginAsync start: " + displayName);
                m_ConnectionStatus.Value = "Logging In To Voice Service";
                await VivoxService.Instance.LoginAsync(loginOptions);
                Debug.Log("[Vivox] LoginAsync success");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Vivox] LoginAsync failed: " + e);
            }
            finally
            {
                m_IsLoggingIn = false;
            }
        }

        void LocalUserLoggedIn()
        {
            Debug.Log("[Vivox] LocalUserLoggedIn Connected=" + XRINetworkGameManager.Connected.Value);

            if (!VivoxService.Instance.IsLoggedIn)
                return;

            if (XRINetworkGameManager.Connected.Value)
            {
                m_ConnectionStatus.Value = "Joining Voice Channel";
                TryJoinVoiceChannelNow();
            }
            else
            {
                m_ConnectionStatus.Value = "Voice Service Ready";
            }
        }

        public void TryJoinVoiceChannelNow()
        {
            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
                return;

            if (VivoxService.Instance == null)
                return;

            if (m_IsJoining || m_IsLeaving)
                return;

            RefreshCurrentLobbyId();

            if (VivoxService.Instance.IsLoggedIn)
            {
                if (m_RetryJoinCoroutine != null)
                {
                    StopCoroutine(m_RetryJoinCoroutine);
                    m_RetryJoinCoroutine = null;
                }

                if (string.IsNullOrWhiteSpace(m_CurrentLobbyId))
                {
                    m_RetryJoinCoroutine = StartCoroutine(RetryJoinWhenChannelReady());
                    return;
                }

                ConnectToVoiceChannel();
            }
            else
            {
                Login();
            }
        }

        IEnumerator RetryJoinWhenChannelReady()
        {
            const float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (m_IsJoining || m_IsLeaving)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                    continue;
                }

                RefreshCurrentLobbyId();

                if (!string.IsNullOrWhiteSpace(m_CurrentLobbyId))
                {
                    Debug.Log("[Vivox] Channel id became available -> " + m_CurrentLobbyId);
                    ConnectToVoiceChannel();
                    m_RetryJoinCoroutine = null;
                    yield break;
                }

                elapsed += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            Debug.LogWarning("[Vivox] Timed out waiting for a valid voice channel id.");
            m_RetryJoinCoroutine = null;
        }

        async void ConnectToVoiceChannel()
        {
            if (m_IsJoining || m_IsLeaving)
                return;

            m_IsJoining = true;

            try
            {
                RefreshCurrentLobbyId();
                string targetLobbyId = m_CurrentLobbyId;

                bool isConnectedClient = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

                Debug.Log("[Vivox] ConnectToVoiceChannel IsConnectedClient=" + isConnectedClient +
                          " m_ConnectedToRoom=" + m_ConnectedToRoom +
                          " targetLobbyId='" + targetLobbyId + "'");

                if (!isConnectedClient || string.IsNullOrWhiteSpace(targetLobbyId))
                    return;

                await VivoxService.Instance.LeaveAllChannelsAsync();

                if (!VivoxService.Instance.IsLoggedIn)
                {
                    Debug.Log("[Vivox] Not logged in anymore, abort join");
                    return;
                }

                m_ConnectedToRoom = false;

                Channel3DProperties properties = new(
                    AudibleDistance,
                    ConversationalDistance,
                    AudioFadeIntensity,
                    AudioFadeModel
                );

                await VivoxService.Instance.JoinPositionalChannelAsync(
                    targetLobbyId,
                    m_ChatCapability,
                    properties
                );

                m_CurrentLobbyId = targetLobbyId;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Vivox] ConnectToVoiceChannel FAILED: " + e);
            }
            finally
            {
                m_IsJoining = false;
            }
        }

        void BindToParticipantEvents()
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }

        void UnbindParticipantEvents()
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }

        async void LeaveRoomChannelOnly()
        {
            if (m_IsLeaving)
                return;

            if (VivoxService.Instance == null)
                return;

            m_IsLeaving = true;

            if (m_RetryJoinCoroutine != null)
            {
                StopCoroutine(m_RetryJoinCoroutine);
                m_RetryJoinCoroutine = null;
            }

            try
            {
                Debug.Log("[Vivox] FULL disconnect: leaving channels + logout");

                if (VivoxService.Instance.IsLoggedIn)
                {
                    if (VivoxService.Instance.ActiveChannels.Count > 0)
                    {
                        await VivoxService.Instance.LeaveAllChannelsAsync();
                    }

                    await VivoxService.Instance.LogoutAsync();
                }

                m_ConnectedToRoom = false;
                m_CurrentLobbyId = string.Empty;
                m_LocalParticpant = null;

                m_ConnectionStatus.Value = "Voice Service Ready";
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Vivox] Leave failed: " + e);
            }
            finally
            {
                m_IsLeaving = false;
            }
        }

        [ContextMenu("Reconnect")]
        public void Reconnect()
        {
            if (XRINetworkGameManager.CurrentSessionType == SessionType.LocalOnly)
                return;

            ReconnectAsync();
        }

        async void ReconnectAsync()
        {
            if (VivoxService.Instance == null)
                return;

            if (m_RetryJoinCoroutine != null)
            {
                StopCoroutine(m_RetryJoinCoroutine);
                m_RetryJoinCoroutine = null;
            }

            await VivoxService.Instance.LeaveAllChannelsAsync();
            m_ConnectedToRoom = false;
            m_CurrentLobbyId = string.Empty;

            if (VivoxService.Instance.IsLoggedIn)
            {
                if (XRINetworkGameManager.Connected.Value)
                    TryJoinVoiceChannelNow();
                else
                    m_ConnectionStatus.Value = "Voice Service Ready";
            }
            else
            {
                m_ConnectionStatus.Value = "Reconnecting to Voice Chat";
                Login();
            }
        }

        public void Set3DAudio(Transform localPlayerHeadTransform)
        {
            if (VivoxService.Instance.IsLoggedIn &&
                VivoxService.Instance.ActiveChannels.Count > 0 &&
                !string.IsNullOrWhiteSpace(m_CurrentLobbyId) &&
                VivoxService.Instance.TransmittingChannels.Count > 0 &&
                VivoxService.Instance.TransmittingChannels[0] == m_CurrentLobbyId)
            {
                VivoxService.Instance.Set3DPosition(
                    localPlayerHeadTransform.position,
                    localPlayerHeadTransform.position,
                    localPlayerHeadTransform.forward,
                    localPlayerHeadTransform.up,
                    m_CurrentLobbyId
                );
            }
        }

        public void ToggleSelfMute(bool setManual = false, bool mutedOverrideValue = false)
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                m_SelfMuted.Value = setManual ? mutedOverrideValue : !m_SelfMuted.Value;
            else
                m_SelfMuted.Value = false;

            if (XRINetworkGameManager.CurrentSessionType == SessionType.DistributedAuthority && VivoxService.Instance.IsLoggedIn)
            {
                if (m_SelfMuted.Value)
                    VivoxService.Instance.MuteInputDevice();
                else
                    VivoxService.Instance.UnmuteInputDevice();
            }
            else
            {
                OfflinePlayerAvatar.muted = m_SelfMuted.Value;
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                PlayerHudNotification.Instance.ShowText(k_MicrophonePersmissionDialogue, 3.0f);
        }

        public void SetInputVolume(float volume)
        {
            volume = Mathf.Clamp(volume, m_MinMaxVoiceInputVolume.x, m_MinMaxVoiceInputVolume.y);
            VivoxService.Instance.SetInputDeviceVolume((int)volume);

            if (volume <= (m_MinMaxVoiceInputVolume.x + .05f))
                ToggleSelfMute(true, true);
            else
                ToggleSelfMute(true, false);
        }

        public void SetOutputVolume(float volume)
        {
            volume = Mathf.Clamp(volume, m_MinMaxVoiceOutputVolume.x, m_MinMaxVoiceOutputVolume.y);
            VivoxService.Instance.SetOutputDeviceVolume((int)volume);
        }

        void OnParticipantAdded(VivoxParticipant participant)
        {
            Debug.Log("[Vivox] OnParticipantAdded IsSelf=" + participant.IsSelf + " PlayerId=" + participant.PlayerId);

            if (participant.IsSelf)
            {
                m_ConnectedToRoom = true;
                m_LocalParticpant = participant;
                m_SelfMuted.Value = false;

                if (XRINetworkPlayer.LocalPlayer != null)
                    XRINetworkPlayer.LocalPlayer.SetVoiceId(m_LocalParticpant.PlayerId);

                m_ConnectionStatus.Value = "Joined Voice Channel";
                PlayerHudNotification.Instance.ShowText("Joined Voice Chat", 3.0f);
            }
            else
            {
                foreach (XRINetworkPlayer player in FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None))
                {
                    if (player.playerVoiceId == participant.PlayerId)
                        player.SetupVoicePlayer();
                }
            }
        }

        void OnParticipantRemoved(VivoxParticipant participant)
        {
            RemoveVivoxPlayer(participant.PlayerId);

            if (participant.IsSelf)
            {
                m_ConnectionStatus.Value = VivoxService.Instance.IsLoggedIn ? "Voice Service Ready" : "Left Voice Channel";
                m_ConnectedToRoom = false;
                m_LocalParticpant = null;
            }
        }

        public VivoxParticipant GetVivoxParticipantById(string participantPlayerId)
        {
            if (string.IsNullOrWhiteSpace(m_CurrentLobbyId) || !VivoxService.Instance.ActiveChannels.ContainsKey(m_CurrentLobbyId))
                return null;

            foreach (var participant in VivoxService.Instance.ActiveChannels[m_CurrentLobbyId])
            {
                if (participantPlayerId == participant.PlayerId)
                    return participant;
            }

            return null;
        }

        public static void AddNewVivoxPlayer(string participantID, XRINetworkPlayer networkPlayer)
        {
            if (!m_PlayersDictionary.ContainsKey(participantID))
                m_PlayersDictionary.Add(participantID, networkPlayer);
        }

        public static void RemoveVivoxPlayer(string participantID)
        {
            if (XRINetworkPlayer.LocalPlayer != null && participantID == XRINetworkPlayer.LocalPlayer.playerVoiceId)
                return;

            if (m_PlayersDictionary.ContainsKey(participantID))
                m_PlayersDictionary.Remove(participantID);
        }

        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            LeaveRoomChannelOnly();
        }

        [ContextMenu("Debug Particpants")]
        void DebugParticipants()
        {
            if (string.IsNullOrWhiteSpace(m_CurrentLobbyId) || !VivoxService.Instance.ActiveChannels.ContainsKey(m_CurrentLobbyId))
                return;

            StringBuilder output = new StringBuilder();
            output.Append($"[Room Type: Positional\n[Room Code: {m_CurrentLobbyId}]");

            foreach (var participant in VivoxService.Instance.ActiveChannels[m_CurrentLobbyId])
            {
                output.Append($"\n[ParticipantID: {participant.PlayerId}]\n[AudioEnergy: {participant.AudioEnergy}]");
            }

            Utils.Log($"{k_DebugPrepend}{output}");
        }
    }
}