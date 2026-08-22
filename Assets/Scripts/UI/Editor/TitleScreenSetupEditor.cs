#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds a full-screen, Scale-With-Screen-Size title canvas and wires Play -> scene "Current".
/// Menu: GameObject > UI > Create Fitted Title Screen
/// </summary>
public static class TitleScreenSetupEditor
{
    private const string PlaySceneName = "Current";

    [MenuItem("GameObject/UI/Create Fitted Title Screen", false, 10)]
    public static void CreateFittedTitleScreen()
    {
        // Event system required for button clicks
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        GameObject canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasFitToScreen));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Fitted Title Screen");

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        Stretch(canvasRect);

        // Full-screen background
        GameObject bg = CreateUiObject("Background", canvasGo.transform);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);
        Stretch(bg.GetComponent<RectTransform>());

        // Center content group
        GameObject content = CreateUiObject("Content", canvasGo.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(800f, 420f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 28f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(24, 24, 24, 24);

        // Title
        GameObject titleGo = CreateUiObject("Title", content.transform);
        Text title = titleGo.AddComponent<Text>();
        title.text = "Ben's Game";
        title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 72;
        title.color = Color.white;
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (title.font == null)
            title.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        LayoutElement titleLayout = titleGo.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 120f;

        // Play button
        Button playButton = CreateButton(content.transform, "PlayButton", "Play", new Color(0.20f, 0.55f, 0.35f, 1f));

        // Quit button
        Button quitButton = CreateButton(content.transform, "QuitButton", "Quit", new Color(0.35f, 0.20f, 0.20f, 1f));

        TitleScreenController controller = canvasGo.AddComponent<TitleScreenController>();
        controller.playSceneName = PlaySceneName;
        controller.playButton = playButton;
        controller.quitButton = quitButton;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(playButton.onClick, controller.OnPlayPressed);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(quitButton.onClick, controller.OnQuitPressed);

        canvasGo.GetComponent<CanvasFitToScreen>().Apply();

        Selection.activeGameObject = canvasGo;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Title Screen Created",
            "Created a full-screen title canvas.\n\n" +
            "Play loads scene \"" + PlaySceneName + "\".\n\n" +
            "Next:\n" +
            "1. Save this scene (e.g. Assets/Scenes/TitleScreen.unity)\n" +
            "2. File > Build Settings → add TitleScreen and Current\n" +
            "3. Put TitleScreen first in the list",
            "OK");
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;

        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = 64f;
        layout.preferredWidth = 320f;

        GameObject textGo = CreateUiObject("Label", go.transform);
        Text text = textGo.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 36;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Stretch(textGo.GetComponent<RectTransform>());

        return button;
    }

    private static void Stretch(RectTransform rect)
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
#endif
