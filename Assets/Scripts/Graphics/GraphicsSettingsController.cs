using UnityEngine;

/// <summary>
/// Graphics quality presets focused on shadow cost (usually the biggest realtime lighting expense).
/// Low / Medium / High / Max.
/// </summary>
public enum GraphicsQualityPreset
{
    Low = 0,
    Medium = 1,
    High = 2,
    Max = 3
}

/// <summary>
/// Applies and persists graphics quality. Wire UI dropdowns to <see cref="SetPreset"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GraphicsSettingsController : MonoBehaviour
{
    public const string PlayerPrefsKey = "GraphicsQualityPreset";

    public static GraphicsSettingsController Instance { get; private set; }

    [Header("Startup")]
    public GraphicsQualityPreset defaultPreset = GraphicsQualityPreset.High;
    public bool loadSavedPresetOnAwake = true;
    public bool applyOnAwake = true;

    [Header("Optional hooks")]
    [Tooltip("If assigned, sun shadow type / strength are synced with the preset.")]
    public OptimizedWorldLighting worldLighting;
    [Tooltip("If assigned, applies distance-based shadow LOD (cascade bias + distant caster cull).")]
    public ShadowLodController shadowLod;

    public GraphicsQualityPreset CurrentPreset { get; private set; } = GraphicsQualityPreset.High;

    public event System.Action<GraphicsQualityPreset> PresetChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadSavedPresetOnAwake && PlayerPrefs.HasKey(PlayerPrefsKey))
            CurrentPreset = (GraphicsQualityPreset)PlayerPrefs.GetInt(PlayerPrefsKey, (int)defaultPreset);
        else
            CurrentPreset = defaultPreset;

        if (applyOnAwake)
            ApplyPreset(CurrentPreset, save: false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetPreset(GraphicsQualityPreset preset)
    {
        ApplyPreset(preset, save: true);
    }

    /// <summary>UI helper: 0=Low, 1=Medium, 2=High, 3=Max</summary>
    public void SetPresetIndex(int index)
    {
        index = Mathf.Clamp(index, 0, 3);
        SetPreset((GraphicsQualityPreset)index);
    }

    public void SetLow() => SetPreset(GraphicsQualityPreset.Low);
    public void SetMedium() => SetPreset(GraphicsQualityPreset.Medium);
    public void SetHigh() => SetPreset(GraphicsQualityPreset.High);
    public void SetMax() => SetPreset(GraphicsQualityPreset.Max);

    public void ApplyPreset(GraphicsQualityPreset preset, bool save)
    {
        CurrentPreset = preset;
        ShadowQualityProfile profile = ShadowQualityProfile.For(preset);
        profile.Apply();

        if (worldLighting == null)
            worldLighting = FindAnyObjectByType<OptimizedWorldLighting>();

        if (worldLighting != null)
            worldLighting.ApplyShadowProfile(profile);

        if (shadowLod == null)
            shadowLod = ShadowLodController.Instance != null
                ? ShadowLodController.Instance
                : FindAnyObjectByType<ShadowLodController>();

        if (shadowLod != null)
            shadowLod.ApplyFromPreset(preset);

        if (save)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)preset);
            PlayerPrefs.Save();
        }

        PresetChanged?.Invoke(preset);
        Debug.Log($"Graphics quality set to {preset} (shadows: {profile.shadows}, distance: {profile.shadowDistance}, cascades: {profile.shadowCascades})");
    }
}

/// <summary>
/// Concrete shadow numbers tuned for open-world terrain streaming.
/// </summary>
public struct ShadowQualityProfile
{
    public ShadowQuality shadows;
    public ShadowResolution resolution;
    public ShadowProjection projection;
    public float shadowDistance;
    public int shadowCascades;
    public float cascade2Split;
    public Vector3 cascade4Split;
    public float shadowNearPlaneOffset;
    public bool softShadows;
    public float shadowStrength;

    // Broader quality knobs that still help efficiency without a full quality-settings rewrite.
    public int pixelLightCount;
    public bool softVegetation;
    public AnisotropicFiltering anisotropicFiltering;
    public int antiAliasing; // 0,2,4,8

    public static ShadowQualityProfile For(GraphicsQualityPreset preset)
    {
        switch (preset)
        {
            case GraphicsQualityPreset.Low:
                return new ShadowQualityProfile
                {
                    shadows = ShadowQuality.Disable,
                    resolution = ShadowResolution.Low,
                    projection = ShadowProjection.StableFit,
                    shadowDistance = 35f,
                    shadowCascades = 1,
                    cascade2Split = 0.333f,
                    cascade4Split = new Vector3(0.067f, 0.2f, 0.467f),
                    shadowNearPlaneOffset = 2f,
                    softShadows = false,
                    shadowStrength = 0.7f,
                    pixelLightCount = 1,
                    softVegetation = false,
                    anisotropicFiltering = AnisotropicFiltering.Disable,
                    antiAliasing = 0
                };

            case GraphicsQualityPreset.Medium:
                return new ShadowQualityProfile
                {
                    shadows = ShadowQuality.HardOnly,
                    resolution = ShadowResolution.Medium,
                    projection = ShadowProjection.StableFit,
                    shadowDistance = 60f,
                    shadowCascades = 2,
                    // Near-biased: cascade 1 stays denser; cascade 2 covers the rest cheaper.
                    cascade2Split = 0.22f,
                    cascade4Split = new Vector3(0.05f, 0.15f, 0.35f),
                    shadowNearPlaneOffset = 1.5f,
                    softShadows = false,
                    shadowStrength = 0.8f,
                    pixelLightCount = 2,
                    softVegetation = false,
                    anisotropicFiltering = AnisotropicFiltering.Enable,
                    antiAliasing = 0
                };

            case GraphicsQualityPreset.High:
                return new ShadowQualityProfile
                {
                    shadows = ShadowQuality.All,
                    resolution = ShadowResolution.High,
                    projection = ShadowProjection.StableFit,
                    shadowDistance = 100f,
                    shadowCascades = 4,
                    cascade2Split = 0.25f,
                    // Near-biased 4-cascade splits (distant cascades = lower texel density).
                    cascade4Split = new Vector3(0.05f, 0.14f, 0.35f),
                    shadowNearPlaneOffset = 1f,
                    softShadows = true,
                    shadowStrength = 0.9f,
                    pixelLightCount = 4,
                    softVegetation = true,
                    anisotropicFiltering = AnisotropicFiltering.Enable,
                    antiAliasing = 2
                };

            default: // Max
                return new ShadowQualityProfile
                {
                    shadows = ShadowQuality.All,
                    resolution = ShadowResolution.VeryHigh,
                    projection = ShadowProjection.StableFit,
                    shadowDistance = 150f,
                    shadowCascades = 4,
                    cascade2Split = 0.25f,
                    cascade4Split = new Vector3(0.04f, 0.12f, 0.32f),
                    shadowNearPlaneOffset = 0.5f,
                    softShadows = true,
                    shadowStrength = 1f,
                    pixelLightCount = 4,
                    softVegetation = true,
                    anisotropicFiltering = AnisotropicFiltering.ForceEnable,
                    antiAliasing = 4
                };
        }
    }

    public void Apply()
    {
        QualitySettings.shadows = shadows;
        QualitySettings.shadowResolution = resolution;
        QualitySettings.shadowProjection = projection;
        QualitySettings.shadowDistance = shadowDistance;
        QualitySettings.shadowCascades = shadowCascades;
        QualitySettings.shadowCascade2Split = cascade2Split;
        QualitySettings.shadowCascade4Split = cascade4Split;
        QualitySettings.shadowNearPlaneOffset = shadowNearPlaneOffset;
        QualitySettings.pixelLightCount = pixelLightCount;
        QualitySettings.softVegetation = softVegetation;
        QualitySettings.anisotropicFiltering = anisotropicFiltering;
        QualitySettings.antiAliasing = antiAliasing;
    }
}
