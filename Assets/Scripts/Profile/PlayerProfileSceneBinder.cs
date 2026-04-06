using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfileSceneBinder : MonoBehaviour
{
    [SerializeField] private TMP_InputField firstTimeNameInputField;
    [SerializeField] private GameObject firstTimeSetupPanel;
    [SerializeField] private TMP_Text firstTimeNicknameStatusText;
    [SerializeField] private Button firstTimeConfirmButton;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_InputField profileNameInputField;
    [SerializeField] private TMP_Text profileNicknameStatusText;
    [SerializeField] private TMP_Text profilePublicPlayerNameText;
    [SerializeField] private Button profileSaveButton;
    [SerializeField] private TMP_Text[] displayNameTexts;
    [SerializeField] private TMP_Text[] publicNameTexts;

    private PlayerProfileManager profileManager;

    private void Start()
    {
        BindUI();
    }

    public void BindUI()
    {
        profileManager = PlayerProfileManager.Instance;

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();

        if (profileManager == null)
        {
            return;
        }

        profileManager.BindSceneUI(
            firstTimeNameInputField,
            profileNameInputField,
            firstTimeSetupPanel,
            lobbyPanel,
            firstTimeNicknameStatusText,
            profileNicknameStatusText,
            profilePublicPlayerNameText,
            displayNameTexts,
            publicNameTexts
        );

        SetupButtons();
        SetupValidation();

        if (profileManager.IsProfileLoaded)
        {
            profileManager.RefreshUI();
        }
        else
        {
            profileManager.OnProfileLoaded -= OnProfileLoaded;
            profileManager.OnProfileLoaded += OnProfileLoaded;
        }
    }

    private void SetupButtons()
    {
        if (firstTimeConfirmButton != null)
        {
            firstTimeConfirmButton.onClick.RemoveListener(OnFirstTimeConfirmClicked);
            firstTimeConfirmButton.onClick.AddListener(OnFirstTimeConfirmClicked);
        }

        if (profileSaveButton != null)
        {
            profileSaveButton.onClick.RemoveListener(OnProfileSaveClicked);
            profileSaveButton.onClick.AddListener(OnProfileSaveClicked);
        }
    }

    private void SetupValidation()
    {
        if (firstTimeNameInputField != null)
        {
            firstTimeNameInputField.onValueChanged.RemoveListener(OnFirstTimeNameChanged);
            firstTimeNameInputField.onValueChanged.AddListener(OnFirstTimeNameChanged);
        }

        if (profileNameInputField != null)
        {
            profileNameInputField.onValueChanged.RemoveListener(OnProfileNameChanged);
            profileNameInputField.onValueChanged.AddListener(OnProfileNameChanged);
        }
    }

    private void OnFirstTimeConfirmClicked()
    {
        if (profileManager == null) return;
        profileManager.SaveFirstTimeProfile();
    }

    private void OnProfileSaveClicked()
    {
        if (profileManager == null) return;
        profileManager.SaveProfileChanges();
    }

    private void OnFirstTimeNameChanged(string _)
    {
        if (profileManager == null) return;
        profileManager.ValidateFirstTimeNickname();
    }

    private void OnProfileNameChanged(string _)
    {
        if (profileManager == null) return;
        profileManager.ValidateProfileNickname();
    }

    private void OnProfileLoaded()
    {
        if (profileManager == null) return;
        profileManager.RefreshUI();
    }

    private void OnDestroy()
    {
        if (profileManager != null)
            profileManager.OnProfileLoaded -= OnProfileLoaded;

        if (firstTimeConfirmButton != null)
            firstTimeConfirmButton.onClick.RemoveListener(OnFirstTimeConfirmClicked);

        if (profileSaveButton != null)
            profileSaveButton.onClick.RemoveListener(OnProfileSaveClicked);

        if (firstTimeNameInputField != null)
            firstTimeNameInputField.onValueChanged.RemoveListener(OnFirstTimeNameChanged);

        if (profileNameInputField != null)
            profileNameInputField.onValueChanged.RemoveListener(OnProfileNameChanged);
    }
}