using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XRMultiplayer
{
    public class OutgoingRequestRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button cancelButton;

        public void Setup(string displayText, UnityAction onCancel)
        {
            if (nameText != null)
                nameText.text = displayText;

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                if (onCancel != null) cancelButton.onClick.AddListener(onCancel);
            }
        }
    }
}