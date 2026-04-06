using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.XR.CoreUtils.Bindings.Variables;
using UnityEngine;
using UnityEditor;
using Unity.Services.Multiplayer;
using Unity.Netcode.Transports.UTP;
using System.Net.Sockets;
using System.Net;

namespace XRMultiplayer
{
    [RequireComponent(typeof(SessionManager)), RequireComponent(typeof(AuthenticationManager))]
    public class XRINetworkGameManager : MonoBehaviour
    {
        public enum ConnectionState
        {
            None,
            Authenticating,
            Authenticated,
            Connecting,
            Connected
        }

        public const int maxPlayers = 20;

        public static XRINetworkGameManager Instance => s_Instance;
        private static XRINetworkGameManager s_Instance;

        public static ulong LocalId;
        public static string AuthenicationId;
        public static string ConnectedRoomCode;
        public static string ConnectedRoomRegion;

        public static BindableVariable<string> ConnectedRoomName = new("");
        public static BindableVariable<string> LocalPlayerName = new("Player");
        public static BindableVariable<Color> LocalPlayerColor = new(Color.white);

        public static IReadOnlyBindableVariable<bool> Connected => m_Connected;
        private static BindableVariable<bool> m_Connected = new(false);

        public static IReadOnlyBindableVariable<ConnectionState> CurrentConnectionState => m_ConnectionState;
        private static BindableEnum<ConnectionState> m_ConnectionState = new(ConnectionState.None);

        public static SessionType CurrentSessionType
        {
            get
            {
                SessionType defaultSessionType;
                try
                {
                    defaultSessionType = Instance.sessionManager.sessionType;

                    if (defaultSessionType == SessionType.DistributedAuthority &&
                        Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        defaultSessionType = SessionType.LocalOnly;
                    }
                }
                catch (Exception ex)
                {
                    Utils.Log($"{k_DebugPrepend}Error getting CurrentSessionType: {ex.Message}", 1);
                    defaultSessionType = SessionType.LocalOnly;
                }

                return defaultSessionType;
            }
        }

        public bool autoConnectOnLobbyJoin => m_AutoConnectOnLobbyJoin;
        [SerializeField] private bool m_AutoConnectOnLobbyJoin = true;

        public bool positionalVoiceChat = false;

        public Action<ulong, bool> OnPlayerStateChanged;
        public Action<string> OnConnectionUpdated;
        public Action<string> OnConnectionFailedAction;
        public Action<ulong> OnSessionOwnerPromoted;

        public SessionManager sessionManager => m_SessionManager;
        private SessionManager m_SessionManager;

        public AuthenticationManager authenticationManager => m_AuthenticationManager;
        private AuthenticationManager m_AuthenticationManager;

        private readonly List<ulong> m_CurrentPlayerIDs = new();

        private bool m_IsShuttingDown = false;
        private bool m_IsDisconnecting = false;

        private const string k_DebugPrepend = "<color=#FAC00C>[Network Game Manager]</color> ";

        protected virtual async void Awake()
        {
            if (s_Instance != null)
            {
                Utils.Log($"{k_DebugPrepend}Duplicate XRINetworkGameManager found, destroying.", 2);
                Destroy(gameObject);
                return;
            }

            s_Instance = this;

            if (TryGetComponent(out m_SessionManager) && TryGetComponent(out m_AuthenticationManager))
            {
                m_SessionManager.OnSessionFailed += ConnectionFailed;
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Missing Managers, disabling component.", 2);
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            bool skipCloudCheck = false;
            if (!CloudProjectSettings.projectBound && !skipCloudCheck)
            {
                Utils.Log(
                    $"{k_DebugPrepend}Project has not been linked to Unity Cloud." +
                    "\nThe VR Multiplayer Template utilizes Unity Gaming Services and must be linked to Unity Cloud." +
                    "\nGo to <b>Settings -> Project Settings -> Services</b> and link your project.",
                    2);
            }
#endif

            m_Connected.Value = false;
            m_ConnectionState.Value = ConnectionState.Authenticating;

            if (CurrentSessionType == SessionType.DistributedAuthority)
            {
                bool signedIn = await m_AuthenticationManager.Authenticate();
                if (!signedIn)
                {
                    Utils.Log($"{k_DebugPrepend}Failed to Authenticate.", 1);
                    ConnectionFailed("Failed to Authenticate.");
                    PlayerHudNotification.Instance.ShowText("Failed to Authenticate.");
                    return;
                }
            }

            m_ConnectionState.Value = ConnectionState.Authenticated;
        }

        protected virtual void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped += LocalClientStopped;
                NetworkManager.Singleton.OnSessionOwnerPromoted += SessionOwnerPromoted;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void SessionOwnerPromoted(ulong sessionOwnerId)
        {
            OnSessionOwnerPromoted?.Invoke(sessionOwnerId);

            if (TryGetPlayerByID(sessionOwnerId, out XRINetworkPlayer player))
                PlayerHudNotification.Instance.ShowText($"<b>Status:</b> {player.playerName} now the Host.");
        }

        public void OnDestroy()
        {
            ShutDown();
        }

        private void OnApplicationQuit()
        {
            ShutDown();
        }

        private async void ShutDown()
        {
            if (m_IsShuttingDown)
                return;

            m_IsShuttingDown = true;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped -= LocalClientStopped;
                NetworkManager.Singleton.OnSessionOwnerPromoted -= SessionOwnerPromoted;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }

            if (m_SessionManager != null)
                await m_SessionManager.LeaveSession();
        }

        public bool IsAuthenticated()
        {
            return m_SessionManager.sessionType != SessionType.DistributedAuthority ||
                   AuthenticationManager.IsAuthenticated();
        }

        public virtual void OnLocalClientStarted(ulong localPlayerId)
        {
            LocalId = localPlayerId;
            m_Connected.Value = true;
            m_ConnectionState.Value = ConnectionState.Connected;
            PlayerHudNotification.Instance.ShowText("<b>Status:</b> Connected");
            Utils.Log($"{k_DebugPrepend}Local Player Started with ID: {localPlayerId}", 0);

            /*var voiceChatManager = FindFirstObjectByType<VoiceChatManager>();
            if (voiceChatManager != null)
            {
                voiceChatManager.TryJoinVoiceChannelNow();
            }*/
        }

        protected virtual void LocalClientStopped(bool _)
        {
            m_Connected.Value = false;
            m_CurrentPlayerIDs.Clear();
            PlayerHudNotification.Instance.ShowText("<b>Status:</b> Disconnected");

            if (IsAuthenticated())
                m_ConnectionState.Value = ConnectionState.Authenticated;
            else
                m_ConnectionState.Value = ConnectionState.None;
        }

        public virtual bool TryGetPlayerByID(ulong id, out XRINetworkPlayer player)
        {
            XRINetworkPlayer[] allPlayers = FindObjectsByType<XRINetworkPlayer>(FindObjectsSortMode.None);

            foreach (XRINetworkPlayer p in allPlayers)
            {
                if (p.NetworkObject.OwnerClientId == id)
                {
                    player = p;
                    return true;
                }
            }

            player = null;
            return false;
        }

        [ContextMenu("Show All NetworkClients")]
        private void ShowAllNetworkClients()
        {
            if (NetworkManager.Singleton == null)
                return;

            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                if (client.Value?.PlayerObject != null)
                    Debug.Log($"Client: {client.Key}, {client.Value.PlayerObject.name}");
            }
        }

        public virtual void PlayerJoined(ulong playerID)
        {
            if (!m_CurrentPlayerIDs.Contains(playerID))
            {
                m_CurrentPlayerIDs.Add(playerID);
                OnPlayerStateChanged?.Invoke(playerID, true);
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Trying to add player ID [{playerID}] that already exists", 1);
            }
        }

        public virtual void PlayerLeft(ulong playerID)
        {
            if (m_CurrentPlayerIDs.Contains(playerID))
            {
                m_CurrentPlayerIDs.Remove(playerID);
                OnPlayerStateChanged?.Invoke(playerID, false);
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Trying to remove player ID [{playerID}] that doesn't exist", 1);
            }
        }

        public virtual void ConnectionFailed(string reason)
        {
            OnConnectionFailedAction?.Invoke(reason);
            m_ConnectionState.Value = IsAuthenticated() ? ConnectionState.Authenticated : ConnectionState.None;
        }

        public virtual void ConnectionUpdated(string update)
        {
            OnConnectionUpdated?.Invoke(update);
        }

        public virtual async void QuickJoinLobby()
        {
            Utils.Log($"{k_DebugPrepend}Joining Lobby by Quick Join.");
            if (await AbleToConnect())
                ConnectToSession(await m_SessionManager.QuickJoinLobby());
        }

        public virtual async void JoinLobbyByCode(string code)
        {
            Utils.Log($"{k_DebugPrepend}Joining Lobby by room code: {code}.");
            if (await AbleToConnect())
                ConnectToSession(await m_SessionManager.JoinLobby(roomCode: code));
        }

        public virtual async void JoinLobbySpecific(ISessionInfo session)
        {
            Utils.Log($"{k_DebugPrepend}Joining specific Lobby: {session.Name}.");
            if (await AbleToConnect())
                ConnectToSession(await m_SessionManager.JoinLobby(sessionInfo: session));
        }

        public virtual void CreateNewLobby(string roomName = null, bool isPrivate = false, int playerCount = maxPlayers)
        {
            CreateNewLobby(roomName, isPrivate, playerCount, null);
        }

        public virtual async void CreateNewLobby(string roomName, bool isPrivate, int playerCount, Dictionary<string, SessionProperty> extraSessionProperties)
        {
            Utils.Log($"{k_DebugPrepend}Creating New Lobby: {roomName}.");
            if (await AbleToConnect())
                ConnectToSession(await m_SessionManager.CreateSession(roomName, isPrivate, playerCount, extraSessionProperties));
        }

        protected virtual async Task<bool> AbleToConnect()
        {
            if (m_ConnectionState.Value == ConnectionState.Connecting)
            {
                string failureMessage = "Connection attempt still in progress.";
                Utils.Log($"{k_DebugPrepend}{failureMessage}", 1);
                ConnectionFailed(failureMessage);
                return false;
            }

            if (m_IsDisconnecting)
            {
                string failureMessage = "Disconnect still in progress.";
                Utils.Log($"{k_DebugPrepend}{failureMessage}", 1);
                ConnectionFailed(failureMessage);
                return false;
            }

            if (Connected.Value || m_ConnectionState.Value == ConnectionState.Connected)
            {
                Utils.Log($"{k_DebugPrepend}Already connected to a lobby. Disconnecting first.", 0);
                await DisconnectAsync();
                await Task.Delay(100);
            }

            m_ConnectionState.Value = ConnectionState.Connecting;
            return true;
        }

        protected virtual void ConnectToSession(ISession session)
        {
            if (session == null)
            {
                FailedToConnect();
                return;
            }

            ConnectedRoomCode = session.Code;
            ConnectedRoomName.Value = session.Name;
        }

        protected virtual void FailedToConnect(string reason = null)
        {
            string failureMessage = reason ?? "Failed to connect to lobby.";
            Utils.Log($"{k_DebugPrepend}{failureMessage}", 1);
        }

        public virtual async void CancelMatchmaking()
        {
            if (IsAuthenticated())
                m_ConnectionState.Value = ConnectionState.Authenticated;

            await m_SessionManager.LeaveSession();
            m_Connected.Value = false;
        }

        public virtual async void Disconnect()
        {
            if (CurrentSessionType == SessionType.DistributedAuthority)
                await DisconnectAsync();
            else
                LeaveLocalConnection();
        }

        public virtual async Task DisconnectAsync()
        {
            if (m_IsDisconnecting)
                return;

            m_IsDisconnecting = true;

            try
            {
                await m_SessionManager.LeaveSession();
                await WaitForNetworkShutdownAsync();

                m_Connected.Value = false;

                if (IsAuthenticated())
                    m_ConnectionState.Value = ConnectionState.Authenticated;
                else
                    m_ConnectionState.Value = ConnectionState.None;

                Utils.Log($"{k_DebugPrepend}Disconnected from Game.");
            }
            finally
            {
                m_IsDisconnecting = false;
            }
        }

        public virtual bool HostLocalConnection()
        {
            string localIP = GetLocalIPAddress();
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;

            transport.ConnectionData.Address = localIP;
            ConnectedRoomName.Value = "Local Room";
            ConnectedRoomCode = localIP;
            return NetworkManager.Singleton.StartHost();
        }

        public virtual bool JoinLocalConnection()
        {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;

            ConnectedRoomName.Value = "Local Room";
            ConnectedRoomCode = transport.ConnectionData.Address;
            return NetworkManager.Singleton.StartClient();
        }

        public virtual void LeaveLocalConnection()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            m_Connected.Value = false;
        }

        public virtual string GetLocalIPAddress()
        {
            string localIP = "127.0.0.1";

            try
            {
                string host = "8.8.8.8";
                int port = 65530;

                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect(host, port);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null)
                        localIP = endPoint.Address.ToString();
                }
            }
            catch (Exception e)
            {
                Utils.Log($"{k_DebugPrepend}Failed to get local IP: {e.Message}", 1);
            }

            return localIP;
        }

        public virtual async void JoinLobbyBySessionId(string sessionId)
        {
            Utils.Log($"{k_DebugPrepend}Joining Lobby by session ID: {sessionId}.");
            if (await AbleToConnect())
                ConnectToSession(await m_SessionManager.JoinLobbyById(sessionId));
        }

        private async Task WaitForNetworkShutdownAsync(float timeoutSeconds = 5f)
        {
            if (NetworkManager.Singleton == null)
                return;

            float startTime = Time.realtimeSinceStartup;

            while (NetworkManager.Singleton.IsListening)
            {
                if (Time.realtimeSinceStartup - startTime >= timeoutSeconds)
                {
                    Utils.Log($"{k_DebugPrepend}Timed out waiting for NetworkManager shutdown.", 1);
                    break;
                }

                await Task.Yield();
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == clientId)
            {
                OnLocalClientStarted(clientId);
            }
        }
    }
}