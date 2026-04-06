using TMPro;
using UnityEngine;

[ExecuteAlways]
public class ChatBubbleHeightSync : MonoBehaviour
{
    [SerializeField] private RectTransform messageBubbleRect;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float topPadding = 10f;
    [SerializeField] private float bottomPadding = 10f;
    [SerializeField] private float minBubbleHeight = 40f;

    private void Awake()
    {
        RefreshBubbleHeight(false);
    }

    private void OnEnable()
    {
        RefreshBubbleHeight(false);
    }

    private void LateUpdate()
    {
        RefreshBubbleHeight(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshBubbleHeight(true);
    }
#endif

    public void RefreshBubbleHeight(bool skipCanvasUpdate = false)
    {
        if (messageBubbleRect == null || messageText == null)
            return;

        if (!skipCanvasUpdate)
            Canvas.ForceUpdateCanvases();

        messageText.ForceMeshUpdate();

        float textHeight = messageText.preferredHeight;
        float bubbleHeight = Mathf.Max(minBubbleHeight, textHeight + topPadding + bottomPadding);

        messageBubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
    }
}