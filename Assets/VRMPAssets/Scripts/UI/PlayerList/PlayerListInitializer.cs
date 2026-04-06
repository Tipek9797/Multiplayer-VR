using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XRMultiplayer
{
    public class PlayerListInitializer : MonoBehaviour
    {
        [Header("Optional manual references")]
        [SerializeField] private PlayerListUI[] m_PlayerListUIs;

        [Header("Auto-find settings")]
        [SerializeField] private bool autoFindIfMissing = true;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private float retryDuration = 5f;
        [SerializeField] private float retryInterval = 0.25f;

        private bool m_Initialized;

        private void Start()
        {
            StartCoroutine(InitializeWhenReady());
        }

        private IEnumerator InitializeWhenReady()
        {
            float elapsed = 0f;

            while (!TryInitializePlayerLists())
            {
                if (!autoFindIfMissing)
                    yield break;

                elapsed += retryInterval;

                if (elapsed >= retryDuration)
                {
                    yield break;
                }

                yield return new WaitForSeconds(retryInterval);
            }
        }

        private bool TryInitializePlayerLists()
        {
            if (m_Initialized)
                return true;

            List<PlayerListUI> resolved = new List<PlayerListUI>();

            if (m_PlayerListUIs != null)
            {
                foreach (var ui in m_PlayerListUIs)
                {
                    if (ui != null && !resolved.Contains(ui))
                        resolved.Add(ui);
                }
            }

            if (autoFindIfMissing)
            {
                PlayerListUI[] found = includeInactive
                    ? Resources.FindObjectsOfTypeAll<PlayerListUI>()
                    : FindObjectsByType<PlayerListUI>(FindObjectsSortMode.None);

                foreach (var ui in found)
                {
                    if (ui == null)
                        continue;

                    if (string.IsNullOrEmpty(ui.gameObject.scene.name))
                        continue;

                    if (!resolved.Contains(ui))
                        resolved.Add(ui);
                }
            }

            if (resolved.Count == 0)
                return false;

            m_PlayerListUIs = resolved.ToArray();


            foreach (var ui in m_PlayerListUIs)
            {
                if (ui == null)
                    continue;

                ui.InitializeCallbacks();
            }

            m_Initialized = true;
            return true;
        }

        public void RebindAndInitializeNow()
        {
            m_Initialized = false;
            TryInitializePlayerLists();
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
                return "(null)";

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}