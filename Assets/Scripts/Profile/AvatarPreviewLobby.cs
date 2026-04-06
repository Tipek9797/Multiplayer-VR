using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AvatarPreviewLobby : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private Image previewImage;
    [SerializeField] private Sprite femaleSprite;
    [SerializeField] private Sprite maleSprite;
    [SerializeField] private string femaleId = "female";
    [SerializeField] private string maleId = "male";

    private void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (previewImage == null)
            previewImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        UpdatePreviewFromDropdown();
    }

    private void OnDisable()
    {
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }

    private void Start()
    {
        UpdatePreviewFromDropdown();
    }

    private void OnDropdownChanged(int _)
    {
        UpdatePreviewFromDropdown();
    }

    public void UpdatePreviewFromDropdown()
    {
        if (dropdown == null)
            return;

        string selectedId = GetSelectedCharacterId();
        SetPreviewImage(selectedId);
    }

    private string GetSelectedCharacterId()
    {
        if (dropdown == null)
            return femaleId;

        return dropdown.value == 1 ? maleId : femaleId;
    }

    private void SetPreviewImage(string characterId)
    {
        if (previewImage == null)
            return;

        if (characterId == maleId)
            previewImage.sprite = maleSprite;
        else
            previewImage.sprite = femaleSprite;

        previewImage.enabled = previewImage.sprite != null;
    }
}