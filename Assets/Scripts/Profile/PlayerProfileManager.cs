using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class PlayerProfileManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField firstTimeNameInputField;
    [SerializeField] private TMP_InputField profileNameInputField;
    [SerializeField] private GameObject firstTimeSetupPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_Text[] displayNameTexts;
    [SerializeField] private TMP_Text[] publicNameTexts;
    [SerializeField] private TMP_Text profilePublicNameText;
    [SerializeField] private TMP_Text firstTimeNicknameStatusText;
    [SerializeField] private TMP_Text profileNicknameStatusText;
    [SerializeField] private string defaultDisplayName = "Player";
    [SerializeField] private string defaultCharacterId = "female";
    [SerializeField] private int cloudLoadRetryCount = 3;
    [SerializeField] private float cloudLoadRetryDelaySeconds = 1.25f;

    public static PlayerProfileManager Instance { get; private set; }

    private const string DisplayNameKey = "displayName";
    private const string CharacterIdKey = "characterId";

    private const string LocalCacheDisplayNameKey = "ppm.cached.displayName";
    private const string LocalCacheCharacterIdKey = "ppm.cached.characterId";
    private const string LocalCachePlayerIdKey = "ppm.cached.playerId";

    public string CurrentDisplayName { get; private set; }
    public string CurrentCharacterId { get; private set; }
    public string CurrentPublicPlayerName { get; private set; }
    public bool HasExistingProfile { get; private set; }
    public bool IsProfileLoaded { get; private set; }
    public bool ProfileLoadFailed { get; private set; }
    public event Action OnProfileLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HideStartupPanels();
    }

    private async void Start()
    {
        HideStartupPanels();

        await WaitForAuthentication();
        await LoadPublicName();
        await LoadProfileFromCloud();

        IsProfileLoaded = true;

        UpdateProfileUI();
        ApplyStartupUIState();
        OnProfileLoaded?.Invoke();
    }

    private void HideStartupPanels()
    {
        if (firstTimeSetupPanel != null)
            firstTimeSetupPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    private async Task WaitForAuthentication(float timeoutSeconds = 20f)
    {
        float startTime = Time.realtimeSinceStartup;

        while (true)
        {
            try
            {
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                    break;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            if (Time.realtimeSinceStartup - startTime >= timeoutSeconds)
            {
                break;
            }

            await Task.Yield();
        }
    }

    private async Task LoadPublicName()
    {
        try
        {
            CurrentPublicPlayerName = AuthenticationService.Instance.PlayerName;

            if (string.IsNullOrWhiteSpace(CurrentPublicPlayerName))
                CurrentPublicPlayerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            CurrentPublicPlayerName = "";
        }
    }

    private async Task<bool> UpdatePublicName(string requestedName, bool isFirstTimeSetup)
    {
        try
        {
            string finalPublicName = await AuthenticationService.Instance.UpdatePlayerNameAsync(requestedName);
            CurrentPublicPlayerName = finalPublicName;

            if (isFirstTimeSetup)
                SetFirstTimeNicknameStatus($"Nickname saved: {finalPublicName}");
            else
                SetProfileNicknameStatus($"Nickname saved: {finalPublicName}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);

            if (isFirstTimeSetup)
                SetFirstTimeNicknameStatus("Couldn't save nickname.");
            else
                SetProfileNicknameStatus("Couldn't save nickname.");

            return false;
        }
    }

    public async void SaveFirstTimeProfile()
    {
        string chosenName = GetInputName(firstTimeNameInputField);

        if (!NicknameValidation.IsValidNickname(chosenName, out string error))
        {
            SetFirstTimeNicknameStatus(error);
            Debug.LogWarning(error);
            return;
        }

        CurrentCharacterId = CharacterDropdownUI.GetSelectedCharacterId();

        if (string.IsNullOrWhiteSpace(CurrentCharacterId))
            CurrentCharacterId = defaultCharacterId;

        SetFirstTimeNicknameStatus("Saving nickname...");

        bool authNameUpdated = await UpdatePublicName(chosenName, true);
        if (!authNameUpdated)
            return;

        CurrentDisplayName = chosenName;

        SetFirstTimeNicknameStatus("Saving profile...");
        bool saveSucceeded = await SaveProfileToCloud();

        if (!saveSucceeded)
            return;

        SaveProfileLocally();

        HasExistingProfile = true;
        IsProfileLoaded = true;
        ProfileLoadFailed = false;

        UpdateProfileUI();
        ApplyStartupUIState();
        OnProfileLoaded?.Invoke();
        RefreshPlayerNameSync();
        RefreshLocalNetworkAvatar();

        SetFirstTimeNicknameStatus("Profile saved.");
        SetProfileNicknameStatus("");
    }

    public async void SaveProfileChanges()
    {
        string chosenName = GetInputName(profileNameInputField);
        bool hasTypedNewName = !string.IsNullOrWhiteSpace(chosenName);

        CurrentCharacterId = CharacterDropdownUI.GetSelectedCharacterId();

        if (string.IsNullOrWhiteSpace(CurrentCharacterId))
            CurrentCharacterId = defaultCharacterId;

        if (!hasTypedNewName)
        {
            if (string.IsNullOrWhiteSpace(CurrentDisplayName))
                CurrentDisplayName = defaultDisplayName;

            SetProfileNicknameStatus("Saving character...");
            bool characterSaveSucceeded = await SaveProfileToCloud();

            if (!characterSaveSucceeded)
                return;

            SaveProfileLocally();

            HasExistingProfile = true;
            IsProfileLoaded = true;
            ProfileLoadFailed = false;

            UpdateProfileUI();
            ApplyStartupUIState();
            OnProfileLoaded?.Invoke();
            RefreshPlayerNameSync();
            RefreshLocalNetworkAvatar();

            SetProfileNicknameStatus("Character saved.");
            SetFirstTimeNicknameStatus("");
            return;
        }

        if (!NicknameValidation.IsValidNickname(chosenName, out string error))
        {
            SetProfileNicknameStatus(error);
            return;
        }

        SetProfileNicknameStatus("Saving nickname...");

        bool authNameUpdated = await UpdatePublicName(chosenName, false);
        if (!authNameUpdated)
            return;

        CurrentDisplayName = chosenName;

        SetProfileNicknameStatus("Saving profile...");
        bool saveSucceeded = await SaveProfileToCloud();

        if (!saveSucceeded)
            return;

        SaveProfileLocally();

        HasExistingProfile = true;
        IsProfileLoaded = true;
        ProfileLoadFailed = false;

        UpdateProfileUI();
        ApplyStartupUIState();
        OnProfileLoaded?.Invoke();
        RefreshPlayerNameSync();
        RefreshLocalNetworkAvatar();

        SetProfileNicknameStatus("Profile saved.");
        SetFirstTimeNicknameStatus("");
    }

    private string GetInputName(TMP_InputField sourceField)
    {
        return sourceField != null ? sourceField.text.Trim() : "";
    }

    private async Task<bool> SaveProfileToCloud()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentDisplayName))
                CurrentDisplayName = defaultDisplayName;

            if (string.IsNullOrWhiteSpace(CurrentCharacterId))
                CurrentCharacterId = defaultCharacterId;

            var data = new Dictionary<string, object>
            {
                { DisplayNameKey, CurrentDisplayName },
                { CharacterIdKey, CurrentCharacterId }
            };

            await CloudSaveService.Instance.Data.Player.SaveAsync(data);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            SetFirstTimeNicknameStatus("Save failed.");
            SetProfileNicknameStatus("Save failed.");
            return false;
        }
    }

    private async Task LoadProfileFromCloud()
    {
        ProfileLoadFailed = false;

        for (int attempt = 1; attempt <= Mathf.Max(1, cloudLoadRetryCount); attempt++)
        {
            try
            {
                var keys = new HashSet<string>
                {
                    DisplayNameKey,
                    CharacterIdKey
                };

                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                bool hasDisplayName = data.TryGetValue(DisplayNameKey, out var displayNameItem);
                bool hasCharacterId = data.TryGetValue(CharacterIdKey, out var characterIdItem);

                string loadedDisplayName = hasDisplayName ? displayNameItem.Value.GetAs<string>() : "";
                string loadedCharacterId = hasCharacterId ? characterIdItem.Value.GetAs<string>() : "";

                if (string.IsNullOrWhiteSpace(loadedDisplayName))
                    loadedDisplayName = defaultDisplayName;

                if (string.IsNullOrWhiteSpace(loadedCharacterId))
                    loadedCharacterId = defaultCharacterId;

                CurrentDisplayName = loadedDisplayName;
                CurrentCharacterId = loadedCharacterId;
                HasExistingProfile = hasDisplayName || hasCharacterId;
                ProfileLoadFailed = false;

                if (HasExistingProfile)
                    SaveProfileLocally();

                return;
            }
            catch (Exception e)
            {
                Debug.LogError(e);

                if (attempt < Mathf.Max(1, cloudLoadRetryCount))
                    await Task.Delay(TimeSpan.FromSeconds(cloudLoadRetryDelaySeconds));
            }
        }

        ProfileLoadFailed = true;

        if (LoadLocalCachedProfile(out string cachedDisplayName, out string cachedCharacterId))
        {
            CurrentDisplayName = string.IsNullOrWhiteSpace(cachedDisplayName) ? defaultDisplayName : cachedDisplayName;
            CurrentCharacterId = string.IsNullOrWhiteSpace(cachedCharacterId) ? defaultCharacterId : cachedCharacterId;
            HasExistingProfile = true;
            return;
        }

        HasExistingProfile = false;
        CurrentDisplayName = defaultDisplayName;
        CurrentCharacterId = defaultCharacterId;
    }

    private void UpdateProfileUI()
    {
        if (firstTimeNameInputField != null)
            firstTimeNameInputField.SetTextWithoutNotify(HasExistingProfile ? CurrentDisplayName : "");

        if (profileNameInputField != null)
            profileNameInputField.SetTextWithoutNotify("");

        if (displayNameTexts != null)
        {
            foreach (var textTarget in displayNameTexts)
            {
                if (textTarget != null)
                    textTarget.text = CurrentDisplayName;
            }
        }

        string bestPublicName = GetPublicName();

        if (publicNameTexts != null)
        {
            foreach (var textTarget in publicNameTexts)
            {
                if (textTarget != null)
                    textTarget.text = bestPublicName;
            }
        }

        if (profilePublicNameText != null)
            profilePublicNameText.text = bestPublicName;

        if (!ProfileLoadFailed)
        {
            SetFirstTimeNicknameStatus("");
            SetProfileNicknameStatus("");
        }

    }

    private void ApplyStartupUIState()
    {
        if (!IsProfileLoaded)
        {
            HideStartupPanels();
            return;
        }

        if (ProfileLoadFailed)
        {
            if (HasExistingProfile)
            {
                if (firstTimeSetupPanel != null)
                    firstTimeSetupPanel.SetActive(false);

                if (lobbyPanel != null)
                    lobbyPanel.SetActive(true);

                SetFirstTimeNicknameStatus("");
                SetProfileNicknameStatus("Cloud load failed. Restored local data.");
            }
            else
            {
                if (firstTimeSetupPanel != null)
                    firstTimeSetupPanel.SetActive(true);

                if (lobbyPanel != null)
                    lobbyPanel.SetActive(false);

                SetFirstTimeNicknameStatus("Could not load cloud profile. Try again.");
                SetProfileNicknameStatus("Could not load cloud profile. Try again.");
            }
            return;
        }

        if (firstTimeSetupPanel != null)
            firstTimeSetupPanel.SetActive(!HasExistingProfile);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(HasExistingProfile);
    }

    public void ValidateFirstTimeNickname()
    {
        string enteredName = GetInputName(firstTimeNameInputField);

        if (string.IsNullOrWhiteSpace(enteredName))
        {
            SetFirstTimeNicknameStatus("Enter a nickname.");
            return;
        }

        if (NicknameValidation.IsValidNickname(enteredName, out string error))
            SetFirstTimeNicknameStatus("Nickname looks good.");
        else
            SetFirstTimeNicknameStatus(error);
    }

    public void ValidateProfileNickname()
    {
        string enteredName = GetInputName(profileNameInputField);

        if (string.IsNullOrWhiteSpace(enteredName))
        {
            SetProfileNicknameStatus("");
            return;
        }

        if (NicknameValidation.IsValidNickname(enteredName, out string error))
            SetProfileNicknameStatus("Nickname looks good.");
        else
            SetProfileNicknameStatus(error);
    }

    public string GetPublicName()
    {
        if (!string.IsNullOrWhiteSpace(CurrentPublicPlayerName))
            return CurrentPublicPlayerName;

        if (!string.IsNullOrWhiteSpace(CurrentDisplayName))
            return CurrentDisplayName;

        return defaultDisplayName;
    }

    public string GetShortPublicName()
    {
        string fullName = GetPublicName();

        if (string.IsNullOrWhiteSpace(fullName))
            return defaultDisplayName;

        int hashIndex = fullName.IndexOf('#');

        if (hashIndex > 0)
            return fullName.Substring(0, hashIndex);

        return fullName;
    }

    public string GetNetworkName()
    {
        return GetShortPublicName();
    }

    public void BindSceneUI(
        TMP_InputField firstTimeInput,
        TMP_InputField profileInput,
        GameObject firstTimePanel,
        GameObject lobbyPanelObject,
        TMP_Text firstTimeStatus,
        TMP_Text profileStatus,
        TMP_Text profilePublicNameText,
        TMP_Text[] localDisplayTexts,
        TMP_Text[] publicDisplayTexts)
    {
        if (firstTimeNameInputField == null)
            firstTimeNameInputField = firstTimeInput;

        if (profileNameInputField == null)
            profileNameInputField = profileInput;

        if (firstTimeSetupPanel == null)
            firstTimeSetupPanel = firstTimePanel;

        if (lobbyPanel == null)
            lobbyPanel = lobbyPanelObject;

        if (firstTimeNicknameStatusText == null)
            firstTimeNicknameStatusText = firstTimeStatus;

        if (profileNicknameStatusText == null)
            profileNicknameStatusText = profileStatus;

        if (this.profilePublicNameText == null)
            this.profilePublicNameText = profilePublicNameText;

        if ((displayNameTexts == null || displayNameTexts.Length == 0) && localDisplayTexts != null)
            displayNameTexts = localDisplayTexts;

        if ((publicNameTexts == null || publicNameTexts.Length == 0) && publicDisplayTexts != null)
            publicNameTexts = publicDisplayTexts;

        if (IsProfileLoaded)
        {
            UpdateProfileUI();
            ApplyStartupUIState();
        }
        else
        {
            HideStartupPanels();
        }
    }

    public void RefreshUI()
    {
        UpdateProfileUI();
        ApplyStartupUIState();
    }

    private void RefreshPlayerNameSync()
    {
        var allNameSyncs = FindObjectsByType<PlayerDisplayNameSync>(FindObjectsSortMode.None);

        foreach (var nameSync in allNameSyncs)
        {
            if (nameSync != null && nameSync.IsOwner)
            {
                nameSync.RefreshMyName();
                return;
            }
        }
    }

    private void SetFirstTimeNicknameStatus(string message)
    {
        if (firstTimeNicknameStatusText != null)
            firstTimeNicknameStatusText.text = message;
    }

    private void SetProfileNicknameStatus(string message)
    {
        if (profileNicknameStatusText != null)
            profileNicknameStatusText.text = message;
    }

    private void SaveProfileLocally()
    {
        string playerId = GetPlayerId();

        if (string.IsNullOrWhiteSpace(playerId))
            return;

        PlayerPrefs.SetString(LocalCachePlayerIdKey, playerId);
        PlayerPrefs.SetString(LocalCacheDisplayNameKey, CurrentDisplayName ?? "");
        PlayerPrefs.SetString(LocalCacheCharacterIdKey, CurrentCharacterId ?? "");
        PlayerPrefs.Save();
    }

    private bool LoadLocalCachedProfile(out string cachedDisplayName, out string cachedCharacterId)
    {
        cachedDisplayName = "";
        cachedCharacterId = "";

        string currentPlayerId = GetPlayerId();
        string cachedPlayerId = PlayerPrefs.GetString(LocalCachePlayerIdKey, "");

        if (string.IsNullOrWhiteSpace(currentPlayerId) || string.IsNullOrWhiteSpace(cachedPlayerId))
            return false;

        if (!string.Equals(currentPlayerId, cachedPlayerId, StringComparison.Ordinal))
        {
            return false;
        }

        cachedDisplayName = PlayerPrefs.GetString(LocalCacheDisplayNameKey, "");
        cachedCharacterId = PlayerPrefs.GetString(LocalCacheCharacterIdKey, "");

        bool hasSomething = !string.IsNullOrWhiteSpace(cachedDisplayName) || !string.IsNullOrWhiteSpace(cachedCharacterId);

        return hasSomething;
    }

    private string GetPlayerId()
    {
        try
        {
            return AuthenticationService.Instance != null ? AuthenticationService.Instance.PlayerId : "";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return "";
        }
    }

    private void RefreshLocalNetworkAvatar()
    {
        var allAvatarSyncs = FindObjectsByType<NetworkAvatarVisualSelector>(FindObjectsSortMode.None);

        foreach (var avatarSync in allAvatarSyncs)
        {
            if (avatarSync != null && avatarSync.IsOwner)
            {
                avatarSync.RefreshFromProfile();
                return;
            }
        }
    }
}