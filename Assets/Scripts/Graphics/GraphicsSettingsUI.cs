using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional UI wiring for graphics quality + performance overlay toggle.
/// </summary>
public class GraphicsSettingsUI : MonoBehaviour
{
    [Tooltip("Legacy UI Dropdown with 4 options: Low, Medium, High, Max")]
    public Dropdown legacyDropdown;

    [Tooltip("Toggle that enables the FPS / perf overlay.")]
    public Toggle perfMonitorToggle;

    [Tooltip("Optional: show RAM / CPU / GPU lines when the overlay is on.")]
    public Toggle perfExtendedToggle;

    public GraphicsSettingsController controller;
    public PerformanceMonitor performanceMonitor;

    private void Awake()
    {
        WorldGraphicsBootstrap.EnsureWired(gameObject.scene);

        if (controller == null)
            controller = GraphicsSettingsController.Instance != null
                ? GraphicsSettingsController.Instance
                : FindAnyObjectByType<GraphicsSettingsController>();

        if (controller == null)
        {
            GameObject go = new GameObject("GraphicsSettingsController");
            controller = go.AddComponent<GraphicsSettingsController>();
        }

        performanceMonitor = PerformanceMonitor.EnsureExists();

        if (legacyDropdown == null)
        {
            Dropdown[] dropdowns = GetComponentsInChildren<Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                if (dropdowns[i] != null &&
                    dropdowns[i].name.IndexOf("Graphics", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    legacyDropdown = dropdowns[i];
                    break;
                }
            }

            if (legacyDropdown == null && dropdowns.Length == 1)
                legacyDropdown = dropdowns[0];
        }

        if (perfMonitorToggle == null)
            perfMonitorToggle = FindToggleByName("Perf", "FPS", "Monitor");

        if (perfExtendedToggle == null)
            perfExtendedToggle = FindToggleByName("Extended", "RAM", "CPU", "GPU", "Stats");

        if (legacyDropdown != null)
        {
            legacyDropdown.ClearOptions();
            legacyDropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High", "Max" });
            legacyDropdown.SetValueWithoutNotify((int)controller.CurrentPreset);
            legacyDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            legacyDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        WirePerfToggles();
    }

    private void OnEnable()
    {
        if (controller != null)
            controller.PresetChanged += OnPresetChanged;
        if (performanceMonitor != null)
            performanceMonitor.EnabledChanged += OnPerfEnabledChanged;
        SyncDropdown();
        SyncPerfToggles();
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.PresetChanged -= OnPresetChanged;
        if (performanceMonitor != null)
            performanceMonitor.EnabledChanged -= OnPerfEnabledChanged;
    }

    private void WirePerfToggles()
    {
        if (performanceMonitor == null)
            return;

        if (perfMonitorToggle != null)
        {
            perfMonitorToggle.onValueChanged.RemoveListener(OnPerfToggleChanged);
            perfMonitorToggle.SetIsOnWithoutNotify(performanceMonitor.IsEnabled);
            perfMonitorToggle.onValueChanged.AddListener(OnPerfToggleChanged);
        }

        if (perfExtendedToggle != null)
        {
            perfExtendedToggle.onValueChanged.RemoveListener(OnExtendedToggleChanged);
            perfExtendedToggle.SetIsOnWithoutNotify(performanceMonitor.ShowExtendedStats);
            perfExtendedToggle.onValueChanged.AddListener(OnExtendedToggleChanged);
            perfExtendedToggle.interactable = performanceMonitor.IsEnabled;
        }
    }

    private void SyncPerfToggles()
    {
        if (performanceMonitor == null)
            return;

        if (perfMonitorToggle != null && perfMonitorToggle.isOn != performanceMonitor.IsEnabled)
            perfMonitorToggle.SetIsOnWithoutNotify(performanceMonitor.IsEnabled);

        if (perfExtendedToggle != null)
        {
            if (perfExtendedToggle.isOn != performanceMonitor.ShowExtendedStats)
                perfExtendedToggle.SetIsOnWithoutNotify(performanceMonitor.ShowExtendedStats);
            perfExtendedToggle.interactable = performanceMonitor.IsEnabled;
        }
    }

    private Toggle FindToggleByName(params string[] nameParts)
    {
        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle t = toggles[i];
            if (t == null)
                continue;

            string n = t.name;
            for (int p = 0; p < nameParts.Length; p++)
            {
                if (n.IndexOf(nameParts[p], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
        }

        return null;
    }

    private void OnDropdownChanged(int index)
    {
        if (controller != null)
            controller.SetPresetIndex(index);
    }

    private void OnPerfToggleChanged(bool value)
    {
        performanceMonitor?.SetEnabled(value);
        if (perfExtendedToggle != null)
            perfExtendedToggle.interactable = value;
    }

    private void OnExtendedToggleChanged(bool value)
    {
        performanceMonitor?.SetShowExtendedStats(value);
    }

    private void OnPerfEnabledChanged(bool enabled)
    {
        SyncPerfToggles();
    }

    private void OnPresetChanged(GraphicsQualityPreset preset)
    {
        SyncDropdown();
    }

    private void SyncDropdown()
    {
        if (legacyDropdown == null || controller == null)
            return;

        int index = (int)controller.CurrentPreset;
        if (legacyDropdown.value != index)
            legacyDropdown.SetValueWithoutNotify(index);
    }

    public void OnClickLow() => controller?.SetLow();
    public void OnClickMedium() => controller?.SetMedium();
    public void OnClickHigh() => controller?.SetHigh();
    public void OnClickMax() => controller?.SetMax();

    public void OnClickTogglePerfMonitor() => performanceMonitor?.Toggle();
}
