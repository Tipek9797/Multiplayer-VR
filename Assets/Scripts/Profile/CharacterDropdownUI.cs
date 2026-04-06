using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDropdownUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private Image previewImage;
    [SerializeField] private Sprite femaleSprite;
    [SerializeField] private Sprite maleSprite;

    private const string FemaleId = "female";
    private const string MaleId = "male";

    public static string SelectedCharacterId { get; private set; }

    private void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();
    }

    private void Start()
    {
        LoadSavedCharacter();
        UpdateSelectedCharacter();
        UpdatePreviewImage();
    }

    public void OnDropdownChanged(int _)
    {
        UpdateSelectedCharacter();
        UpdatePreviewImage();
    }

    private void UpdateSelectedCharacter()
    {
        if (dropdown == null)
        {
            return;
        }

        int realIndex = dropdown.value;
        SelectedCharacterId = realIndex == 1 ? MaleId : FemaleId;
    }

    public void LoadSavedCharacter()
    {
        if (dropdown == null || PlayerProfileManager.Instance == null)
            return;

        string currentId = PlayerProfileManager.Instance.CurrentCharacterId;

        if (string.IsNullOrWhiteSpace(currentId))
            currentId = FemaleId;

        dropdown.SetValueWithoutNotify(currentId == MaleId ? 1 : 0);

        SelectedCharacterId = currentId;

        UpdatePreviewImage();
    }

    private void UpdatePreviewImage()
    {
        if (previewImage == null)
            return;

        string currentId = GetSelectedCharacterId();

        if (currentId == MaleId)
            previewImage.sprite = maleSprite;
        else
            previewImage.sprite = femaleSprite;
    }

    public static string GetSelectedCharacterId()
    {
        if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
            return SelectedCharacterId;

        if (PlayerProfileManager.Instance != null &&
            !string.IsNullOrWhiteSpace(PlayerProfileManager.Instance.CurrentCharacterId))
        {
            return PlayerProfileManager.Instance.CurrentCharacterId;
        }

        return FemaleId;
    }
}