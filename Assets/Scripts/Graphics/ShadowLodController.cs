using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Distance-based shadow LOD:
/// 1) Biases cascade splits so near shadows stay sharp and far cascades cover more area (lower texel density).
/// 2) Turns off shadow casting on distant renderers (biggest CPU/GPU win for open worlds).
///
/// Works with <see cref="GraphicsSettingsController"/> Low/Medium/High/Max presets.
/// </summary>
[DefaultExecutionOrder(-80)]
public class ShadowLodController : MonoBehaviour
{
    public static ShadowLodController Instance { get; private set; }

    [Header("References")]
    public Transform distanceOrigin;
    public GraphicsSettingsController graphicsSettings;

    [Header("Caster LOD (per-renderer)")]
    [Tooltip("Renderers closer than this cast shadows normally.")]
    public float fullCastDistance = 45f;
    [Tooltip("Beyond this distance, shadow casting is disabled.")]
    public float disableCastDistance = 90f;
    [Tooltip("Extra margin before re-enabling casting to avoid flicker.")]
    public float hysteresis = 8f;

    [Header("Update budgeting")]
    [Tooltip("How often the renderer cache is rebuilt.")]
    public float registryRefreshSeconds = 2f;
    [Tooltip("Max renderers whose shadow mode is evaluated per frame.")]
    public int maxUpdatesPerFrame = 48;
    public bool includeInactiveRenderers;

    [Header("Exclusions")]
    [Tooltip("These layers never have casting stripped (e.g. Player).")]
    public LayerMask alwaysCastLayers;
    [Tooltip("If a renderer is on these layers, never cast (detail/grass).")]
    public LayerMask neverCastLayers;

    [Header("Cascade LOD")]
    [Tooltip("0 = profile default splits, 1 = heavily favor near-camera sharpness.")]
    [Range(0f, 1f)] public float nearCascadeBias = 0.65f;

    private readonly List<Renderer> registry = new List<Renderer>(512);
    private readonly Dictionary<int, ShadowCastingMode> originalModes = new Dictionary<int, ShadowCastingMode>(512);
    private readonly Dictionary<int, bool> castingDisabled = new Dictionary<int, bool>(512);
    private readonly Dictionary<int, ShadowLodOverride.Mode> overrides = new Dictionary<int, ShadowLodOverride.Mode>(64);

    private float nextRegistryRefresh;
    private int updateCursor;
    private ShadowLodBand activeBand = ShadowLodBand.High;
    private bool shadowsGloballyEnabled = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (graphicsSettings == null)
            graphicsSettings = GraphicsSettingsController.Instance != null
                ? GraphicsSettingsController.Instance
                : FindAnyObjectByType<GraphicsSettingsController>();

        if (distanceOrigin == null && Camera.main != null)
            distanceOrigin = Camera.main.transform;
    }

    private void OnEnable()
    {
        if (graphicsSettings == null)
            graphicsSettings = GraphicsSettingsController.Instance != null
                ? GraphicsSettingsController.Instance
                : FindAnyObjectByType<GraphicsSettingsController>();

        if (graphicsSettings != null)
        {
            graphicsSettings.PresetChanged += OnPresetChanged;
            if (graphicsSettings.shadowLod == null)
                graphicsSettings.shadowLod = this;
        }

        if (graphicsSettings != null)
            ApplyFromPreset(graphicsSettings.CurrentPreset);
        else
            ApplyFromPreset(GraphicsQualityPreset.High);

        WorldGraphicsBootstrap.BindDistanceOrigin(this);
        RefreshRegistry(force: true);
    }

    private void OnDisable()
    {
        if (graphicsSettings != null)
            graphicsSettings.PresetChanged -= OnPresetChanged;

        RestoreAllOriginalModes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        RestoreAllOriginalModes();
    }

    private void Update()
    {
        if (!shadowsGloballyEnabled)
            return;

        if (distanceOrigin == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                distanceOrigin = cam.transform;
            else
                return;
        }

        if (Time.unscaledTime >= nextRegistryRefresh)
            RefreshRegistry(force: false);

        UpdateCasterLodBudgeted();
    }

    private void OnPresetChanged(GraphicsQualityPreset preset)
    {
        ApplyFromPreset(preset);
        RefreshRegistry(force: true);
    }

    public void ApplyFromPreset(GraphicsQualityPreset preset)
    {
        ShadowQualityProfile profile = ShadowQualityProfile.For(preset);
        activeBand = ShadowLodBandUtil.FromPreset(preset);
        shadowsGloballyEnabled = profile.shadows != ShadowQuality.Disable;

        ShadowLodDistances distances = ShadowLodDistances.For(activeBand);
        fullCastDistance = distances.fullCastDistance;
        disableCastDistance = distances.disableCastDistance;
        nearCascadeBias = distances.nearCascadeBias;

        // Cascade map LOD: keep profile cascade count, but bias splits toward the camera.
        ApplyBiasedCascades(profile);

        if (!shadowsGloballyEnabled)
            RestoreAllOriginalModes();
    }

    /// <summary>
    /// Push more shadow-map resolution into near cascades; far cascades cover larger volume = softer/blockier distant shadows.
    /// </summary>
    public void ApplyBiasedCascades(ShadowQualityProfile profile)
    {
        QualitySettings.shadowCascades = profile.shadowCascades;
        QualitySettings.shadowDistance = profile.shadowDistance;

        float bias = Mathf.Clamp01(nearCascadeBias);

        if (profile.shadowCascades <= 1)
            return;

        if (profile.shadowCascades == 2)
        {
            // Smaller split => first cascade ends sooner => higher near density, coarser far cascade.
            float split = Mathf.Lerp(profile.cascade2Split, profile.cascade2Split * 0.55f, bias);
            QualitySettings.shadowCascade2Split = Mathf.Clamp(split, 0.05f, 0.9f);
            return;
        }

        // 4 cascades: scale splits toward camera as bias increases.
        Vector3 baseSplits = profile.cascade4Split;
        Vector3 aggressive = new Vector3(
            Mathf.Max(0.02f, baseSplits.x * 0.55f),
            Mathf.Max(0.05f, baseSplits.y * 0.65f),
            Mathf.Max(0.12f, baseSplits.z * 0.75f));

        Vector3 splits = Vector3.Lerp(baseSplits, aggressive, bias);
        // Keep strictly increasing.
        splits.y = Mathf.Max(splits.y, splits.x + 0.02f);
        splits.z = Mathf.Max(splits.z, splits.y + 0.02f);
        splits.z = Mathf.Min(splits.z, 0.95f);
        QualitySettings.shadowCascade4Split = splits;
    }

    private void RefreshRegistry(bool force)
    {
        nextRegistryRefresh = Time.unscaledTime + Mathf.Max(0.5f, registryRefreshSeconds);

        Renderer[] found = includeInactiveRenderers
            ? FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        registry.Clear();
        overrides.Clear();
        for (int i = 0; i < found.Length; i++)
        {
            Renderer rend = found[i];
            if (rend == null)
                continue;

            int id = rend.GetInstanceID();
            if (!originalModes.ContainsKey(id))
                originalModes[id] = rend.shadowCastingMode;

            ShadowLodOverride overrideComp = rend.GetComponentInParent<ShadowLodOverride>();
            if (overrideComp != null)
                overrides[id] = overrideComp.mode;

            registry.Add(rend);
        }

        updateCursor = 0;
        if (force)
            UpdateCasterLodBudgeted(fullPass: true);
    }

    private void UpdateCasterLodBudgeted(bool fullPass = false)
    {
        if (registry.Count == 0 || distanceOrigin == null)
            return;

        Vector3 origin = distanceOrigin.position;
        float fullSq = fullCastDistance * fullCastDistance;
        float disableSq = disableCastDistance * disableCastDistance;
        float reenableSq = (disableCastDistance - hysteresis) * (disableCastDistance - hysteresis);
        if (reenableSq < fullSq)
            reenableSq = fullSq;

        int budget = fullPass ? registry.Count : Mathf.Max(1, maxUpdatesPerFrame);
        int processed = 0;

        while (processed < budget && registry.Count > 0)
        {
            if (updateCursor >= registry.Count)
                updateCursor = 0;

            Renderer rend = registry[updateCursor];
            updateCursor++;
            processed++;

            if (rend == null)
                continue;

            int layerBit = 1 << rend.gameObject.layer;
            int id = rend.GetInstanceID();

            if (overrides.TryGetValue(id, out ShadowLodOverride.Mode overrideMode))
            {
                if (overrideMode == ShadowLodOverride.Mode.NeverCast)
                {
                    if (rend.shadowCastingMode != ShadowCastingMode.Off)
                        rend.shadowCastingMode = ShadowCastingMode.Off;
                    castingDisabled[id] = true;
                }
                else
                {
                    RestoreOriginalMode(rend);
                    castingDisabled[id] = false;
                }
                continue;
            }

            if ((neverCastLayers.value & layerBit) != 0)
            {
                if (rend.shadowCastingMode != ShadowCastingMode.Off)
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                castingDisabled[id] = true;
                continue;
            }

            if ((alwaysCastLayers.value & layerBit) != 0)
            {
                RestoreOriginalMode(rend);
                castingDisabled[id] = false;
                continue;
            }

            if (!originalModes.TryGetValue(id, out ShadowCastingMode original))
            {
                original = rend.shadowCastingMode;
                originalModes[id] = original;
            }

            // Don't fight assets that never cast shadows.
            if (original == ShadowCastingMode.Off)
                continue;

            float distSq = (rend.bounds.center - origin).sqrMagnitude;
            bool isDisabled = castingDisabled.TryGetValue(id, out bool disabled) && disabled;

            if (!isDisabled && distSq >= disableSq)
            {
                rend.shadowCastingMode = ShadowCastingMode.Off;
                castingDisabled[id] = true;
            }
            else if (isDisabled && distSq <= reenableSq)
            {
                rend.shadowCastingMode = original;
                castingDisabled[id] = false;
            }
            else if (!isDisabled && distSq <= fullSq && rend.shadowCastingMode != original)
            {
                rend.shadowCastingMode = original;
            }
        }
    }

    private void RestoreOriginalMode(Renderer rend)
    {
        if (rend == null)
            return;

        int id = rend.GetInstanceID();
        if (originalModes.TryGetValue(id, out ShadowCastingMode mode))
            rend.shadowCastingMode = mode;
    }

    private void RestoreAllOriginalModes()
    {
        for (int i = 0; i < registry.Count; i++)
        {
            Renderer rend = registry[i];
            if (rend == null)
                continue;

            int id = rend.GetInstanceID();
            if (originalModes.TryGetValue(id, out ShadowCastingMode mode))
                rend.shadowCastingMode = mode;
        }

        castingDisabled.Clear();
    }

    /// <summary>Call after world chunks spawn lots of new props.</summary>
    public void InvalidateRegistry()
    {
        nextRegistryRefresh = 0f;
    }
}

public enum ShadowLodBand
{
    Off,
    Low,
    Medium,
    High,
    Max
}

public static class ShadowLodBandUtil
{
    public static ShadowLodBand FromPreset(GraphicsQualityPreset preset)
    {
        switch (preset)
        {
            case GraphicsQualityPreset.Low: return ShadowLodBand.Off;
            case GraphicsQualityPreset.Medium: return ShadowLodBand.Medium;
            case GraphicsQualityPreset.High: return ShadowLodBand.High;
            default: return ShadowLodBand.Max;
        }
    }
}

public struct ShadowLodDistances
{
    public float fullCastDistance;
    public float disableCastDistance;
    public float nearCascadeBias;

    public static ShadowLodDistances For(ShadowLodBand band)
    {
        switch (band)
        {
            case ShadowLodBand.Off:
                return new ShadowLodDistances { fullCastDistance = 0f, disableCastDistance = 0f, nearCascadeBias = 1f };
            case ShadowLodBand.Medium:
                return new ShadowLodDistances { fullCastDistance = 30f, disableCastDistance = 55f, nearCascadeBias = 0.75f };
            case ShadowLodBand.High:
                return new ShadowLodDistances { fullCastDistance = 45f, disableCastDistance = 90f, nearCascadeBias = 0.65f };
            default: // Max
                return new ShadowLodDistances { fullCastDistance = 70f, disableCastDistance = 130f, nearCascadeBias = 0.45f };
        }
    }
}

/// <summary>
/// Optional marker: force a renderer to always / never cast under ShadowLodController.
/// </summary>
public class ShadowLodOverride : MonoBehaviour
{
    public enum Mode
    {
        AlwaysCast,
        NeverCast
    }

    public Mode mode = Mode.AlwaysCast;
}
