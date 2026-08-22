using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Title screen UI controller. Wire the Play button OnClick to <see cref="OnPlayPressed"/>.
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Exact scene name as it appears in File > Build Settings.")]
    public string playSceneName = "Current";

    [Header("Optional UI")]
    public Button playButton;
    public Button quitButton;

    private void Awake()
    {
        // Ensure the title UI is readable on any resolution.
        EnsureCanvasScalesCorrectly();

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayPressed);
            playButton.onClick.AddListener(OnPlayPressed);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitPressed);
            quitButton.onClick.AddListener(OnQuitPressed);
        }
    }

    /// <summary>
    /// Call this from the Play button's OnClick in the Inspector,
    /// or it will be wired automatically if playButton is assigned.
    /// </summary>
    public void OnPlayPressed()
    {
        if (string.IsNullOrWhiteSpace(playSceneName))
        {
            Debug.LogError("TitleScreenController: playSceneName is empty.", this);
            return;
        }

        // Scene must be listed in File > Build Settings.
        if (!Application.CanStreamedLevelBeLoaded(playSceneName))
        {
            Debug.LogError(
                $"TitleScreenController: Scene '{playSceneName}' cannot be loaded. " +
                "Add it via File > Build Settings (and check the name matches exactly).",
                this);
            return;
        }

        SceneManager.LoadScene(playSceneName);
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureCanvasScalesCorrectly()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // Stretch the root panel under the canvas so content fills the screen.
        RectTransform root = canvas.transform as RectTransform;
        if (root != null)
            StretchFull(root);

        foreach (Transform child in canvas.transform)
        {
            if (child.name.Contains("Panel") || child.name.Contains("Root") || child.name.Contains("Background"))
                StretchFull(child as RectTransform);
        }
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
