using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRMultiplayer
{
    public class RoomSceneManager : MonoBehaviour
    {
        public static RoomSceneManager Instance { get; private set; }

        public static event Action<string> ActiveSceneChanged;

        [SerializeField] private string mainLobbySceneName = "MainLobbyScene";
        [SerializeField] private string forestSceneName = RoomTypeDropdownUI.ForestSceneName;
        [SerializeField] private string alchemistSceneName = RoomTypeDropdownUI.AlchemistSceneName;

        [SerializeField] private float sessionWaitTimeoutSeconds = 20f;
        [SerializeField] private float networkReadyTimeoutSeconds = 20f;
        [SerializeField] private float sceneEventTimeoutSeconds = 30f;

        private Coroutine currentRoutine;
        private bool isSubscribedToSceneEvents;
        private string currentRoomScene;
        private string lastActiveScene;

        private bool waitingForSceneEvent;
        private bool waitingForLoad;
        private string waitingSceneName;
        private bool sceneEventFinished;

        public string CurrentRoomScene => currentRoomScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            XRINetworkGameManager.Connected.Subscribe(OnConnectedChanged);
            SubscribeToSceneEvents();
        }

        private void Start()
        {
            if (XRINetworkGameManager.Connected.Value)
                StartSceneRoutine(LoadRoomAfterConnect());
            else
                StartSceneRoutine(LoadLobbyAtStart());
        }

        private void Update()
        {
            SubscribeToSceneEvents();
        }

        private void OnDisable()
        {
            XRINetworkGameManager.Connected.Unsubscribe(OnConnectedChanged);
            UnsubscribeFromSceneEvents();
            StopSceneRoutine();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnConnectedChanged(bool connected)
        {
            if (connected)
                StartSceneRoutine(LoadRoomAfterConnect());
            else
                StartSceneRoutine(ReturnToLobby());
        }

        private void StartSceneRoutine(IEnumerator routine)
        {
            StopSceneRoutine();
            currentRoutine = StartCoroutine(routine);
        }

        private void StopSceneRoutine()
        {
            if (currentRoutine != null)
            {
                StopCoroutine(currentRoutine);
                currentRoutine = null;
            }
        }

        private IEnumerator LoadLobbyAtStart()
        {
            if (XRINetworkGameManager.Connected.Value)
                yield break;

            yield return LoadSingleLocalScene(mainLobbySceneName);
            yield return UnloadAllScenesByName(forestSceneName);
            yield return UnloadAllScenesByName(alchemistSceneName);

            NotifyActiveSceneChanged(mainLobbySceneName);
        }

        private IEnumerator LoadRoomAfterConnect()
        {
            var gameManager = XRINetworkGameManager.Instance;
            if (gameManager == null || gameManager.sessionManager == null)
                yield break;

            float sessionWait = 0f;
            while (gameManager.sessionManager.currentSession == null && sessionWait < sessionWaitTimeoutSeconds)
            {
                sessionWait += Time.deltaTime;
                yield return null;
            }

            var currentSession = gameManager.sessionManager.currentSession;
            if (currentSession == null)
            {
                yield break;
            }

            string roomTypeId;
            if (!SessionManager.TryReadSessionProperty(currentSession, SessionManager.k_RoomTypeKeyIdentifier, out roomTypeId) ||
                string.IsNullOrWhiteSpace(roomTypeId))
            {
                roomTypeId = CreateRoomUI.SelectedRoomTypeId;
            }

            if (string.IsNullOrWhiteSpace(roomTypeId))
                roomTypeId = RoomTypeDropdownUI.ForestRoomTypeId;

            string targetScene = GetRoomSceneName(roomTypeId);
            currentRoomScene = targetScene;

            float networkWait = 0f;
            while (!IsNetworkSceneManagerReady() && networkWait < networkReadyTimeoutSeconds)
            {
                networkWait += Time.deltaTime;
                yield return null;
            }

            if (!IsNetworkSceneManagerReady())
            {
                yield break;
            }

            if (currentSession.IsHost)
                yield return LoadRoomAsHost(targetScene);
            else
                yield return LoadRoomAsClient(targetScene);

            NotifyActiveSceneChanged(targetScene);
        }

        private IEnumerator LoadRoomAsHost(string targetScene)
        {
            string otherRoom = GetOtherRoomScene(targetScene);

            yield return UnloadAllScenesByName(mainLobbySceneName);

            if (!string.IsNullOrWhiteSpace(otherRoom) && CountLoadedScenesByName(otherRoom) > 0)
                yield return UnloadSceneAndWait(otherRoom);

            if (CountLoadedScenesByName(targetScene) == 0)
                yield return LoadSceneAndWait(targetScene);
        }

        private IEnumerator LoadRoomAsClient(string targetScene)
        {
            yield return UnloadAllScenesByName(mainLobbySceneName);

            if (CountLoadedScenesByName(targetScene) > 0)
                yield break;

            float elapsed = 0f;
            while (CountLoadedScenesByName(targetScene) == 0 && elapsed < sceneEventTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (CountLoadedScenesByName(targetScene) == 0)
            {
            }
        }

        private IEnumerator ReturnToLobby()
        {
            currentRoomScene = null;

            yield return UnloadAllScenesByName(forestSceneName);
            yield return UnloadAllScenesByName(alchemistSceneName);
            yield return LoadSingleLocalScene(mainLobbySceneName);

            NotifyActiveSceneChanged(mainLobbySceneName);
        }

        public string GetRoomSceneName()
        {
            return GetRoomSceneName(CreateRoomUI.SelectedRoomTypeId);
        }

        public string GetRoomSceneName(string roomTypeId)
        {
            switch (roomTypeId)
            {
                case RoomTypeDropdownUI.AlchemistRoomTypeId:
                    return alchemistSceneName;
                case RoomTypeDropdownUI.ForestRoomTypeId:
                default:
                    return forestSceneName;
            }
        }

        private string GetOtherRoomScene(string targetScene)
        {
            if (targetScene == forestSceneName)
                return alchemistSceneName;

            if (targetScene == alchemistSceneName)
                return forestSceneName;

            return null;
        }

        private bool IsNetworkSceneManagerReady()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   NetworkManager.Singleton.SceneManager != null;
        }

        private IEnumerator LoadSingleLocalScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                yield break;

            int countBefore = CountLoadedScenesByName(sceneName);

            if (countBefore == 0)
            {
                AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (loadOp == null)
                {
                    yield break;
                }

                while (!loadOp.isDone)
                    yield return null;
            }
            else if (countBefore > 1)
            {
                yield return UnloadExtraScenes(sceneName, keepFirstLoaded: true);
            }

            int countAfter = CountLoadedScenesByName(sceneName);
        }

        private IEnumerator UnloadAllScenesByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                yield break;

            while (true)
            {
                Scene? loadedScene = GetFirstLoadedSceneByName(sceneName);
                if (!loadedScene.HasValue)
                    yield break;

                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(loadedScene.Value);
                if (unloadOp == null)
                {
                    yield break;
                }

                while (!unloadOp.isDone)
                    yield return null;
            }
        }

        private IEnumerator UnloadExtraScenes(string sceneName, bool keepFirstLoaded)
        {
            List<Scene> loadedScenes = GetLoadedScenesByName(sceneName);
            if (loadedScenes.Count <= 1)
                yield break;

            int startIndex = keepFirstLoaded ? 1 : 0;
            for (int i = startIndex; i < loadedScenes.Count; i++)
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(loadedScenes[i]);
                if (unloadOp == null)
                {
                    continue;
                }

                while (!unloadOp.isDone)
                    yield return null;
            }
        }

        private IEnumerator LoadSceneAndWait(string sceneName)
        {
            var sceneManager = NetworkManager.Singleton.SceneManager;
            var status = sceneManager.LoadScene(sceneName, LoadSceneMode.Additive);

            if (status != SceneEventProgressStatus.Started)
            {
                yield break;
            }

            yield return WaitForSceneEvent(sceneName, true);
        }

        private IEnumerator UnloadSceneAndWait(string sceneName)
        {
            Scene? scene = GetFirstLoadedSceneByName(sceneName);
            if (!scene.HasValue)
                yield break;

            var sceneManager = NetworkManager.Singleton.SceneManager;
            var status = sceneManager.UnloadScene(scene.Value);

            if (status != SceneEventProgressStatus.Started)
            {
                yield break;
            }

            yield return WaitForSceneEvent(sceneName, false);
        }

        private IEnumerator WaitForSceneEvent(string sceneName, bool isLoad)
        {
            waitingForSceneEvent = true;
            waitingForLoad = isLoad;
            waitingSceneName = sceneName;
            sceneEventFinished = false;

            float elapsed = 0f;
            while (!sceneEventFinished && elapsed < sceneEventTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            waitingForSceneEvent = false;
            waitingSceneName = null;
        }

        private void SubscribeToSceneEvents()
        {
            if (isSubscribedToSceneEvents)
                return;

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null)
                return;

            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnNetworkSceneEvent;
            isSubscribedToSceneEvents = true;
        }

        private void UnsubscribeFromSceneEvents()
        {
            if (!isSubscribedToSceneEvents)
                return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;

            isSubscribedToSceneEvents = false;
        }

        private void OnNetworkSceneEvent(SceneEvent sceneEvent)
        {
            if (!waitingForSceneEvent || string.IsNullOrWhiteSpace(waitingSceneName))
                return;

            if (sceneEvent.SceneName != waitingSceneName)
                return;

            if (waitingForLoad && sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
                sceneEventFinished = true;
            else if (!waitingForLoad && sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted)
                sceneEventFinished = true;
        }

        private int CountLoadedScenesByName(string sceneName)
        {
            int count = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                    count++;
            }

            return count;
        }

        private Scene? GetFirstLoadedSceneByName(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                    return scene;
            }

            return null;
        }

        private List<Scene> GetLoadedScenesByName(string sceneName)
        {
            var scenes = new List<Scene>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                    scenes.Add(scene);
            }

            return scenes;
        }

        private void NotifyActiveSceneChanged(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (lastActiveScene == sceneName)
                return;

            lastActiveScene = sceneName;
            ActiveSceneChanged?.Invoke(sceneName);
        }
    }
}