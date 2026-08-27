using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Single efficient world sun + ambient setup. Avoids multiple directional lights,
/// keeps shadow casting on the sun only, and scales with GraphicsSettingsController presets.
/// </summary>
[DefaultExecutionOrder(-90)]
public class OptimizedWorldLighting : MonoBehaviour
{
    [Header("Sun")]
    public Light sun;
    public Vector3 sunEulerAngles = new Vector3(45f, -30f, 0f);
    public Color sunColor = new Color(1f, 0.96f, 0.88f, 1f);
    public float sunIntensity = 1.15f;

    [Header("Ambient (cheap + stable after scene loads)")]
    public AmbientMode ambientMode = AmbientMode.Trilight;
    public Color skyColor = new Color(0.55f, 0.70f, 0.85f);
    public Color equatorColor = new Color(0.40f, 0.42f, 0.38f);
    public Color groundColor = new Color(0.22f, 0.20f, 0.18f);
    public float ambientIntensity = 1f;

    [Header("Fog (optional, cheap depth cue)")]
    public bool enableFog = true;
    public FogMode fogMode = FogMode.ExponentialSquared;
    public Color fogColor = new Color(0.65f, 0.72f, 0.78f);
    public float fogDensity = 0.008f;

    [Header("Efficiency")]
    [Tooltip("Disable realtime GI probes — open procedural worlds rarely need them.")]
    public bool disableRealtimeGI = true;
    [Tooltip("Destroy extra directional lights in the scene (keeps this sun only).")]
    public bool enforceSingleDirectionalLight = true;
    [Tooltip("Refresh ambient after Menu → Current loads.")]
    public bool refreshOnSceneLoaded = true;

    private void Awake()
    {
        EnsureSun();
        ApplyBaseLighting();
        ApplyShadowProfile(ShadowQualityProfile.For(
            GraphicsSettingsController.Instance != null
                ? GraphicsSettingsController.Instance.CurrentPreset
                : GraphicsQualityPreset.High));
    }

    private void OnEnable()
    {
        if (refreshOnSceneLoaded)
            SceneManager.sceneLoaded += OnSceneLoaded;

        // Make sure graphics controller knows about this sun.
        GraphicsSettingsController controller = GraphicsSettingsController.Instance != null
            ? GraphicsSettingsController.Instance
            : FindAnyObjectByType<GraphicsSettingsController>();
        if (controller != null && controller.worldLighting == null)
            controller.worldLighting = this;
    }

    private void OnDisable()
    {
        if (refreshOnSceneLoaded)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSun();
        ApplyBaseLighting();
        DynamicGI.UpdateEnvironment();

        if (enforceSingleDirectionalLight)
            CullExtraDirectionalLights();
    }

    public void EnsureSun()
    {
        if (sun == null)
            sun = GetComponent<Light>();

        if (sun == null)
        {
            // Prefer an existing directional light if one exists.
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    sun = lights[i];
                    break;
                }
            }
        }

        if (sun == null)
        {
            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(sunEulerAngles);
        sun.color = sunColor;
        sun.intensity = sunIntensity;
        sun.renderMode = LightRenderMode.ForcePixel;
        sun.bounceIntensity = 0f; // procedural worlds: skip bounce cost

        if (enforceSingleDirectionalLight)
            CullExtraDirectionalLights();
    }

    public void ApplyBaseLighting()
    {
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = skyColor;
        RenderSettings.ambientEquatorColor = equatorColor;
        RenderSettings.ambientGroundColor = groundColor;
        RenderSettings.ambientIntensity = ambientIntensity;

        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        if (disableRealtimeGI)
        {
            // Keep baked/ambient probes cheap; don't force continuous realtime GI updates.
#if UNITY_2022_1_OR_NEWER
            // No-op if project already has GI off; safe to call.
#endif
            DynamicGI.updateThreshold = 200f;
        }

        DynamicGI.UpdateEnvironment();
    }

    public void ApplyShadowProfile(ShadowQualityProfile profile)
    {
        EnsureSun();
        if (sun == null)
            return;

        if (profile.shadows == ShadowQuality.Disable)
        {
            sun.shadows = LightShadows.None;
        }
        else if (profile.shadows == ShadowQuality.HardOnly || !profile.softShadows)
        {
            sun.shadows = LightShadows.Hard;
        }
        else
        {
            sun.shadows = LightShadows.Soft;
        }

        sun.shadowStrength = profile.shadowStrength;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        sun.shadowNearPlane = Mathf.Clamp(profile.shadowNearPlaneOffset, 0.1f, 10f);
    }

    private void CullExtraDirectionalLights()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light == sun || light.type != LightType.Directional)
                continue;

            // Disable extras instead of destroying — safer if Menu had DontDestroyOnLoad lights.
            light.enabled = false;
            Debug.LogWarning($"OptimizedWorldLighting disabled extra directional light '{light.name}' for efficiency.", light);
        }
    }
}
