using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop on a Canvas (or empty child) to force Screen Space Overlay + Scale With Screen Size
/// and stretch common full-screen panels. Fixes title UIs that disappear / clip in Game view.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CanvasFitToScreen : MonoBehaviour
{
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;
    public bool stretchDirectChildrenNamedPanel = true;

    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Apply();
    }
#endif

    [ContextMenu("Apply Canvas Fit")]
    public void Apply()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
        scaler.referencePixelsPerUnit = 100f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.transform as RectTransform;
        Stretch(canvasRect);

        if (!stretchDirectChildrenNamedPanel)
            return;

        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            Transform child = canvas.transform.GetChild(i);
            string n = child.name.ToLowerInvariant();
            if (n.Contains("panel") || n.Contains("root") || n.Contains("background") || n.Contains("safe"))
                Stretch(child as RectTransform);
        }
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
