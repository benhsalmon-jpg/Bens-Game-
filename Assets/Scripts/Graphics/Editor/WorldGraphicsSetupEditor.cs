#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click wiring for world lighting, shadow LOD, settings dropdown, and FPS overlay toggles.
/// </summary>
public static class WorldGraphicsSetupEditor
{
    [MenuItem("GameObject/Graphics/Setup World Lighting & Shadow LOD", false, 10)]
    public static void SetupWorldGraphics()
    {
        var rig = WorldGraphicsBootstrap.EnsureWired(EditorSceneManager.GetActiveScene());

        GameObject root = rig.controller.gameObject;
        root.name = "WorldGraphics";

        if (rig.lighting.transform.parent != root.transform && rig.lighting.gameObject != root)
            rig.lighting.transform.SetParent(root.transform, true);
        if (rig.shadowLod.transform.parent != root.transform && rig.shadowLod.gameObject != root)
            rig.shadowLod.transform.SetParent(root.transform, true);

        if (Camera.main != null)
            rig.shadowLod.distanceOrigin = Camera.main.transform;

        WorldGraphicsBootstrap.EnsurePlayerAlwaysCasts();
        PerformanceMonitor.EnsureExists();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "World Graphics Wired",
            "Created/wired:\n" +
            "• GraphicsSettingsController\n" +
            "• OptimizedWorldLighting (sun)\n" +
            "• ShadowLodController\n" +
            "• PerformanceMonitor (FPS overlay, off until enabled in settings)\n\n" +
            "Optional: GameObject > Graphics > Add Quality Dropdown To Canvas",
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

        GameObject panel = new GameObject("GraphicsQualityPanel", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create Graphics Quality Panel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(280f, 170f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        GameObject labelGo = CreateText(panel.transform, "Label", "Graphics Quality", 18, TextAnchor.UpperLeft);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -8f);
        labelRect.sizeDelta = new Vector2(-16f, 24f);

        GameObject dropdownGo = DefaultControls.CreateDropdown(new DefaultControls.Resources());
        dropdownGo.name = "GraphicsQualityDropdown";
        Undo.RegisterCreatedObjectUndo(dropdownGo, "Create Graphics Quality Dropdown");
        dropdownGo.transform.SetParent(panel.transform, false);
        RectTransform ddRect = dropdownGo.GetComponent<RectTransform>();
        ddRect.anchorMin = new Vector2(0f, 1f);
        ddRect.anchorMax = new Vector2(1f, 1f);
        ddRect.pivot = new Vector2(0.5f, 1f);
        ddRect.anchoredPosition = new Vector2(0f, -36f);
        ddRect.sizeDelta = new Vector2(-16f, 34f);

        Dropdown dropdown = dropdownGo.GetComponent<Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High", "Max" });

        Toggle perfToggle = CreateToggle(panel.transform, "PerfMonitorToggle", "Show FPS Overlay", new Vector2(0f, -80f));
        Toggle extrasToggle = CreateToggle(panel.transform, "PerfExtendedToggle", "Show RAM / CPU / GPU", new Vector2(0f, -112f));

        GraphicsSettingsUI ui = panel.GetComponent<GraphicsSettingsUI>();
        if (ui == null)
            ui = panel.AddComponent<GraphicsSettingsUI>();
        ui.legacyDropdown = dropdown;
        ui.perfMonitorToggle = perfToggle;
        ui.perfExtendedToggle = extrasToggle;

        var rig = WorldGraphicsBootstrap.EnsureWired(EditorSceneManager.GetActiveScene());
        ui.controller = rig.controller;
        ui.performanceMonitor = PerformanceMonitor.EnsureExists();
        dropdown.value = (int)rig.controller.CurrentPreset;
        perfToggle.isOn = ui.performanceMonitor.IsEnabled;
        extrasToggle.isOn = ui.performanceMonitor.ShowExtendedStats;

        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Quality Dropdown Added",
            "Added Low/Medium/High/Max dropdown plus FPS overlay toggles,\n" +
            "wired to GraphicsSettingsController / PerformanceMonitor.",
            "OK");
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject go = DefaultControls.CreateToggle(new DefaultControls.Resources());
        go.name = name;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(-16f, 24f);

        Toggle toggle = go.GetComponent<Toggle>();
        Text labelText = go.GetComponentInChildren<Text>();
        if (labelText != null)
        {
            labelText.text = label;
            labelText.color = Color.white;
            labelText.fontSize = 14;
        }

        return toggle;
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
