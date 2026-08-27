using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional UI wiring for graphics quality. Drop on a settings panel and assign a Dropdown
/// with options: Low, Medium, High, Max.
/// </summary>
public class GraphicsSettingsUI : MonoBehaviour
{
    [Tooltip("Legacy UI Dropdown with 4 options: Low, Medium, High, Max")]
    public Dropdown legacyDropdown;

    public GraphicsSettingsController controller;

    private void Awake()
    {
        if (controller == null)
            controller = GraphicsSettingsController.Instance != null
                ? GraphicsSettingsController.Instance
                : FindAnyObjectByType<GraphicsSettingsController>();

        if (controller == null)
        {
            GameObject go = new GameObject("GraphicsSettingsController");
            controller = go.AddComponent<GraphicsSettingsController>();
        }

        if (legacyDropdown != null)
        {
            legacyDropdown.ClearOptions();
            legacyDropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High", "Max" });
            legacyDropdown.value = (int)controller.CurrentPreset;
            legacyDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            legacyDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }
    }

    private void OnEnable()
    {
        if (controller != null)
            controller.PresetChanged += OnPresetChanged;
        SyncDropdown();
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.PresetChanged -= OnPresetChanged;
    }

    private void OnDropdownChanged(int index)
    {
        if (controller != null)
            controller.SetPresetIndex(index);
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

    // Button hooks if you prefer four buttons instead of a dropdown.
    public void OnClickLow() => controller?.SetLow();
    public void OnClickMedium() => controller?.SetMedium();
    public void OnClickHigh() => controller?.SetHigh();
    public void OnClickMax() => controller?.SetMax();
}
