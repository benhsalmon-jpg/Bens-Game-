#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click wiring for world lighting, shadow LOD, and optional settings dropdown.
/// Menu: GameObject > Graphics > Setup World Lighting & Shadow LOD
/// </summary>
public static class WorldGraphicsSetupEditor
{
    [MenuItem("GameObject/Graphics/Setup World Lighting & Shadow LOD", false, 10)]
    public static void SetupWorldGraphics()
    {
        var rig = WorldGraphicsBootstrap.EnsureWired(EditorSceneManager.GetActiveScene());

        // Ensure DDOL-friendly single root in the open scene for saving.
        GameObject root = rig.controller.gameObject;
        root.name = "WorldGraphics";

        if (rig.lighting.transform.parent != root.transform && rig.lighting.gameObject != root)
            rig.lighting.transform.SetParent(root.transform, true);
        if (rig.shadowLod.transform.parent != root.transform && rig.shadowLod.gameObject != root)
            rig.shadowLod.transform.SetParent(root.transform, true);

        // Prefer Main Camera as distance origin in editor
        if (Camera.main != null)
            rig.shadowLod.distanceOrigin = Camera.main.transform;

        WorldGraphicsBootstrap.EnsurePlayerAlwaysCasts();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "World Graphics Wired",
            "Created/wired:\n" +
            "• GraphicsSettingsController\n" +
            "• OptimizedWorldLighting (sun)\n" +
            "• ShadowLodController (cascade bias + distant caster cull)\n\n" +
            "Optional: GameObject > Graphics > Add Quality Dropdown To Canvas\n" +
            "to create a Low/Medium/High/Max UI control.",
            "OK");
    }

    [MenuItem("GameObject/Graphics/Add Quality Dropdown To Canvas", false, 11)]
    public static void AddQualityDropdown()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Settings Canvas");
        }

        // Panel
        GameObject panel = new GameObject("GraphicsQualityPanel", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create Graphics Quality Panel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(260f, 90f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Label
        GameObject labelGo = CreateText(panel.transform, "Label", "Graphics Quality", 18, TextAnchor.UpperLeft);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -8f);
        labelRect.sizeDelta = new Vector2(-16f, 28f);

        // Dropdown
        GameObject dropdownGo = DefaultControls.CreateDropdown(new DefaultControls.Resources());
        dropdownGo.name = "GraphicsQualityDropdown";
        Undo.RegisterCreatedObjectUndo(dropdownGo, "Create Graphics Quality Dropdown");
        dropdownGo.transform.SetParent(panel.transform, false);
        RectTransform ddRect = dropdownGo.GetComponent<RectTransform>();
        ddRect.anchorMin = new Vector2(0f, 0f);
        ddRect.anchorMax = new Vector2(1f, 0f);
        ddRect.pivot = new Vector2(0.5f, 0f);
        ddRect.anchoredPosition = new Vector2(0f, 12f);
        ddRect.sizeDelta = new Vector2(-16f, 34f);

        Dropdown dropdown = dropdownGo.GetComponent<Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High", "Max" });

        GraphicsSettingsUI ui = panel.GetComponent<GraphicsSettingsUI>();
        if (ui == null)
            ui = panel.AddComponent<GraphicsSettingsUI>();
        ui.legacyDropdown = dropdown;

        // Ensure backend exists and link
        var rig = WorldGraphicsBootstrap.EnsureWired(EditorSceneManager.GetActiveScene());
        ui.controller = rig.controller;
        dropdown.value = (int)rig.controller.CurrentPreset;

        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Quality Dropdown Added",
            "Added GraphicsQualityPanel with Low/Medium/High/Max dropdown,\n" +
            "wired to GraphicsSettingsController.",
            "OK");
    }

    private static GameObject CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null)
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return go;
    }
}
#endif
