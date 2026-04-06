using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ChatRowHeightSync : MonoBehaviour
{
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private LayoutElement rowLayoutElement;
    [SerializeField] private float minRowHeight = 40f;

    private void Awake()
    {
        Refresh(false);
    }

    private void OnEnable()
    {
        Refresh(false);
    }

    private void LateUpdate()
    {
        Refresh(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Refresh(true);
    }
#endif

    public void Refresh(bool skipCanvasForceUpdate = false)
    {
        if (bubbleRect == null || rowLayoutElement == null)
            return;

        if (!skipCanvasForceUpdate)
            Canvas.ForceUpdateCanvases();

        float h = Mathf.Max(minRowHeight, bubbleRect.rect.height);
        rowLayoutElement.preferredHeight = h;
        rowLayoutElement.minHeight = h;
    }
}