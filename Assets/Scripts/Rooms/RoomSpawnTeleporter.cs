using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace XRMultiplayer
{
    public class RoomSpawnTeleporter : MonoBehaviour
    {
        [SerializeField] private string mainCameraPath = "Camera Offset/Main Camera";

        private CharacterController characterController;
        private XROrigin xrOrigin;

        private string lastSceneName;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            xrOrigin = GetComponent<XROrigin>();
        }

        private void Start()
        {
            StartCoroutine(CheckScenesLoop());
        }

        private IEnumerator CheckScenesLoop()
        {
            while (true)
            {
                TeleportIfReady();
                yield return null;
            }
        }

        private void TeleportIfReady()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                    continue;

                if (scene.name == "MainScene")
                    continue;

                if (lastSceneName == scene.name)
                    continue;

                var spawnPoints = FindObjectsByType<RoomSpawnPoint>(FindObjectsSortMode.None)
                    .Where(p => p.gameObject.scene == scene)
                    .OrderBy(p => p.spawnIndex)
                    .ToArray();

                if (spawnPoints.Length == 0)
                    continue;

                Teleport(scene.name, spawnPoints);
                return;
            }
        }

        private void Teleport(string sceneName, RoomSpawnPoint[] spawnPoints)
        {
            Transform cameraTransform = transform.Find(mainCameraPath);
            if (cameraTransform == null)
            {
                return;
            }

            int spawnIndex = 0;

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                spawnIndex = (int)(NetworkManager.Singleton.LocalClientId % (ulong)spawnPoints.Length);
            }

            RoomSpawnPoint targetSpawn = spawnPoints[spawnIndex];

            if (characterController != null)
                characterController.enabled = false;

            xrOrigin.MoveCameraToWorldLocation(targetSpawn.transform.position);

            Vector3 forward = targetSpawn.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            if (characterController != null)
                characterController.enabled = true;

            lastSceneName = sceneName;
        }
    }
}