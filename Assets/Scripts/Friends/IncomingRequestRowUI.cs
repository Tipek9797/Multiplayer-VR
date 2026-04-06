using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XRMultiplayer
{
    public class IncomingRequestRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button denyButton;

        public void Setup(string displayText, UnityAction onAccept, UnityAction onDeny)
        {
            if (nameText != null)
                nameText.text = displayText;

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                if (onAccept != null) acceptButton.onClick.AddListener(onAccept);
            }

            if (denyButton != null)
            {
                denyButton.onClick.RemoveAllListeners();
                if (onDeny != null) denyButton.onClick.AddListener(onDeny);
            }
        }
    }
}