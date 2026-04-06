using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XRMultiplayer
{
    public class OfflineMenu : MonoBehaviour
    {
        Color[] m_PlayerColors;

        [Header("Player Info")]
        string m_DefaultPlayerName = "Player";
        [SerializeField] TMP_Text m_PlayerNameText;
        [SerializeField] TMP_Text m_PlayerInitialText;
        [SerializeField] Image[] m_PlayerColorIcons;
        [SerializeField] Image m_VolumeIndicator;
        [SerializeField] Image m_MicIcon;
        [SerializeField] Sprite m_MutedSprite;
        [SerializeField] Sprite m_UnmutedSprite;

        [Header("Panel Objects")]
        [SerializeField] GameObject m_CustomizationPanel;
        [SerializeField] GameObject m_ConnectionPanel;

        VoiceChatManager m_VoiceChatManager;

        private void Awake()
        {
            XRINetworkGameManager.Connected.Subscribe(OnConnected);
            XRINetworkGameManager.LocalPlayerName.Subscribe(SetPlayerName);
            XRINetworkGameManager.LocalPlayerColor.Subscribe(SetPlayerColor);

            OfflinePlayerAvatar.voiceAmp.Subscribe(UpdateMicIcon);

            m_VoiceChatManager = FindFirstObjectByType<VoiceChatManager>();
            if (m_VoiceChatManager != null)
                m_VoiceChatManager.selfMuted.Subscribe(MutedChanged);

            SetupDefaultPlayer();

            if (PlayerProfileManager.Instance != null)
                PlayerProfileManager.Instance.OnProfileLoaded += HandleProfileLoaded;
        }

        private void Start()
        {
            UpdateMenuFromProfile();

            if (XRINetworkGameManager.Instance != null)
                XRINetworkGameManager.Instance.OnConnectionFailedAction += ConnectionFailed;
        }

        private void OnEnable()
        {
            UpdateMenuFromProfile();
        }

        private void OnDestroy()
        {
            XRINetworkGameManager.Connected.Unsubscribe(OnConnected);
            XRINetworkGameManager.LocalPlayerName.Unsubscribe(SetPlayerName);
            XRINetworkGameManager.LocalPlayerColor.Unsubscribe(SetPlayerColor);
            OfflinePlayerAvatar.voiceAmp.Unsubscribe(UpdateMicIcon);

            if (m_VoiceChatManager != null)
                m_VoiceChatManager.selfMuted.Unsubscribe(MutedChanged);

            if (XRINetworkGameManager.Instance != null)
                XRINetworkGameManager.Instance.OnConnectionFailedAction -= ConnectionFailed;

            if (PlayerProfileManager.Instance != null)
                PlayerProfileManager.Instance.OnProfileLoaded -= HandleProfileLoaded;
        }

        void SetupDefaultPlayer()
        {
            string bestName = m_DefaultPlayerName;

            if (PlayerProfileManager.Instance != null)
            {
                bestName = PlayerProfileManager.Instance.GetPublicName();
                if (string.IsNullOrWhiteSpace(bestName))
                    bestName = m_DefaultPlayerName;
            }

            XRINetworkGameManager.LocalPlayerName.Value = bestName;

            if (m_PlayerColors != null && m_PlayerColors.Length > 0)
                XRINetworkGameManager.LocalPlayerColor.Value = m_PlayerColors[Random.Range(0, m_PlayerColors.Length)];

            SetPlayerName(bestName);
        }

        void UpdateMenuFromProfile()
        {
            if (PlayerProfileManager.Instance == null || !PlayerProfileManager.Instance.IsProfileLoaded)
            {
                if (m_CustomizationPanel != null)
                    m_CustomizationPanel.SetActive(true);

                if (m_ConnectionPanel != null)
                    m_ConnectionPanel.SetActive(false);

                SetupDefaultPlayer();
                return;
            }

            string bestName = PlayerProfileManager.Instance.GetPublicName();
            if (string.IsNullOrWhiteSpace(bestName))
                bestName = m_DefaultPlayerName;

            XRINetworkGameManager.LocalPlayerName.Value = bestName;
            SetPlayerName(bestName);

            if (PlayerProfileManager.Instance.HasExistingProfile)
                ShowConnection();
            else
                ShowCustomization();
        }

        void SetPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = m_DefaultPlayerName;
            }

            if (m_PlayerNameText != null)
                m_PlayerNameText.text = name;

            if (m_PlayerInitialText != null)
                m_PlayerInitialText.text = name.Substring(0, 1).ToUpperInvariant();

            if (m_PlayerNameText != null)
            {
                m_PlayerNameText.rectTransform.sizeDelta = new Vector2(
                    m_PlayerNameText.preferredWidth * 0.25f,
                    m_PlayerNameText.rectTransform.sizeDelta.y
                );
            }
        }

        void SetPlayerColor(Color color)
        {
            if (m_PlayerColorIcons == null)
                return;

            foreach (var c in m_PlayerColorIcons)
            {
                if (c != null)
                    c.color = color;
            }
        }

        void UpdateMicIcon(float amp)
        {
            if (m_VolumeIndicator != null)
                m_VolumeIndicator.fillAmount = amp;
        }

        void ShowCustomization()
        {
            if (m_CustomizationPanel != null)
                m_CustomizationPanel.SetActive(true);

            if (m_ConnectionPanel != null)
                m_ConnectionPanel.SetActive(false);
        }

        void ShowConnection()
        {
            if (m_CustomizationPanel != null)
                m_CustomizationPanel.SetActive(false);

            if (m_ConnectionPanel != null)
                m_ConnectionPanel.SetActive(true);
        }

        public void CompleteCustomization()
        {
            ShowConnection();
        }

        void OnConnected(bool connected)
        {
            if (connected)
            {
                if (m_CustomizationPanel != null)
                    m_CustomizationPanel.SetActive(false);
            }
            else
            {
                gameObject.SetActive(true);
                UpdateMenuFromProfile();
            }
        }

        void MutedChanged(bool muted)
        {
            if (m_MicIcon != null)
                m_MicIcon.sprite = muted ? m_MutedSprite : m_UnmutedSprite;
        }

        void ConnectionFailed(string reason)
        {
            ShowConnection();
        }

        void HandleProfileLoaded()
        {
            UpdateMenuFromProfile();
        }
    }
}