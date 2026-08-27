using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fixes "strange lighting" after Menu → gameplay scene loads.
/// Unity often keeps a stale ambient/skybox/reflection probe until the environment is refreshed.
/// Put this on any always-present object, or it auto-registers via RuntimeInitializeOnLoadMethod.
/// </summary>
public static class SceneLoadLightingFix
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Also refresh the first loaded scene (Menu) so editor quirks are consistent.
        RefreshLighting(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshLighting(scene);
    }

    /// <summary>
    /// Call after LoadScene / LoadSceneAsync completes if you want an explicit refresh.
    /// </summary>
    public static void RefreshLighting(Scene scene)
    {
        if (!scene.IsValid())
            return;

        // Rebuild ambient probe from the active skybox / environment lighting of THIS scene.
        DynamicGI.UpdateEnvironment();

        // Force reflection probes to catch up after a hard scene swap.
        if (scene.name == "Current" || scene.name == "Menu")
        {
            RefreshReflectionProbes();
        }

        // If something marked DontDestroyOnLoad left an extra sun from Menu, log it.
        WarnIfMultipleDirectionalLights(scene);
    }

    private static void RefreshReflectionProbes()
    {
        ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null && probes[i].mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
                probes[i].RenderProbe();
        }
    }

    private static void WarnIfMultipleDirectionalLights(Scene scene)
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int directional = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].enabled)
                directional++;
        }

        if (directional > 1)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' has {directional} enabled Directional Lights after load. " +
                "A Menu light may be surviving via DontDestroyOnLoad, which makes lighting look washed out / wrong.",
                lights[0]);
        }
    }
}
