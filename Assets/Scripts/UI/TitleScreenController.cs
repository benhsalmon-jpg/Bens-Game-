using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Title / Menu screen controller. Wire Play OnClick to <see cref="OnPlayPressed"/>.
/// Loads scene "Current" in Single mode and refreshes lighting after the load.
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Exact scene name as it appears in File > Build Settings.")]
    public string playSceneName = "Current";

    [Tooltip("Always use Single so Menu lighting/fog/skybox are fully unloaded.")]
    public bool unloadMenuCompletely = true;

    [Header("Optional UI")]
    public Button playButton;
    public Button quitButton;

    private bool isLoading;

    private void Awake()
    {
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
    /// Call from the Play button OnClick, or assign playButton for auto-wire.
    /// </summary>
    public void OnPlayPressed()
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(playSceneName))
        {
            Debug.LogError("TitleScreenController: playSceneName is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(playSceneName))
        {
            Debug.LogError(
                $"TitleScreenController: Scene '{playSceneName}' cannot be loaded. " +
                "Add it via File > Build Settings (name must match exactly, e.g. Current).",
                this);
            return;
        }

        StartCoroutine(LoadPlaySceneRoutine());
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadPlaySceneRoutine()
    {
        isLoading = true;

        LoadSceneMode mode = unloadMenuCompletely ? LoadSceneMode.Single : LoadSceneMode.Additive;
        AsyncOperation op = SceneManager.LoadSceneAsync(playSceneName, mode);
        if (op == null)
        {
            Debug.LogError($"TitleScreenController: LoadSceneAsync('{playSceneName}') failed.", this);
            isLoading = false;
            yield break;
        }

        while (!op.isDone)
            yield return null;

        // One frame later: RenderSettings from Current are active; refresh ambient/reflections.
        yield return null;
        SceneLoadLightingFix.RefreshLighting(SceneManager.GetActiveScene());
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
