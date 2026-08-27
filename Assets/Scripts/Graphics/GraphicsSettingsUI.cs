using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional UI wiring for graphics quality. Drop on a settings panel and assign a Dropdown
/// with options: Low, Medium, High, Max. Auto-wires to GraphicsSettingsController.
/// </summary>
public class GraphicsSettingsUI : MonoBehaviour
{
    [Tooltip("Legacy UI Dropdown with 4 options: Low, Medium, High, Max")]
    public Dropdown legacyDropdown;

    public GraphicsSettingsController controller;

    private void Awake()
    {
        // Prefer the fully wired stack from bootstrap when present.
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

        if (legacyDropdown != null)
        {
            legacyDropdown.ClearOptions();
            legacyDropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High", "Max" });
            legacyDropdown.SetValueWithoutNotify((int)controller.CurrentPreset);
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

    public void OnClickLow() => controller?.SetLow();
    public void OnClickMedium() => controller?.SetMedium();
    public void OnClickHigh() => controller?.SetHigh();
    public void OnClickMax() => controller?.SetMax();
}
