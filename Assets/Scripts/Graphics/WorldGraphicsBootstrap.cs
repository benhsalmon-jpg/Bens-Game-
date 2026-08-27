using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-creates and wires GraphicsSettingsController, OptimizedWorldLighting, and ShadowLodController.
/// No manual Inspector hookup required — runs after the first scene loads and again on each scene change.
/// </summary>
public static class WorldGraphicsBootstrap
{
    private const string RootName = "WorldGraphics";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureWired(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureWired(scene);
    }

    /// <summary>Idempotent: finds or creates the graphics stack and links all references.</summary>
    public static WorldGraphicsRig EnsureWired(Scene scene)
    {
        GraphicsSettingsController controller = GraphicsSettingsController.Instance != null
            ? GraphicsSettingsController.Instance
            : Object.FindAnyObjectByType<GraphicsSettingsController>();

        OptimizedWorldLighting lighting = Object.FindAnyObjectByType<OptimizedWorldLighting>();
        ShadowLodController shadowLod = ShadowLodController.Instance != null
            ? ShadowLodController.Instance
            : Object.FindAnyObjectByType<ShadowLodController>();

        GameObject root = null;
        if (controller != null)
            root = controller.gameObject;
        else if (lighting != null)
            root = lighting.gameObject;
        else if (shadowLod != null)
            root = shadowLod.gameObject;

        if (root == null)
        {
            root = GameObject.Find(RootName);
            if (root == null)
                root = new GameObject(RootName);
        }

        if (controller == null)
            controller = root.GetComponent<GraphicsSettingsController>() ?? root.AddComponent<GraphicsSettingsController>();

        if (lighting == null)
            lighting = root.GetComponent<OptimizedWorldLighting>() ?? root.AddComponent<OptimizedWorldLighting>();

        if (shadowLod == null)
            shadowLod = root.GetComponent<ShadowLodController>() ?? root.AddComponent<ShadowLodController>();

        // Cross-wire references
        controller.worldLighting = lighting;
        controller.shadowLod = shadowLod;
        shadowLod.graphicsSettings = controller;

        lighting.EnsureSun();
        lighting.ApplyBaseLighting();

        BindDistanceOrigin(shadowLod);
        EnsurePlayerAlwaysCasts();

        // Apply current preset so sun + LOD match saved quality
        controller.ApplyPreset(controller.CurrentPreset, save: false);
        shadowLod.InvalidateRegistry();

        // Keep across Menu → Current if this is the DDOL controller root
        if (controller.gameObject.scene.name == "DontDestroyOnLoad" || Application.isPlaying)
        {
            // GraphicsSettingsController already calls DontDestroyOnLoad on itself.
            // Parent lighting/lod under it if they were separate scene objects.
            if (lighting.transform.parent == null && lighting.gameObject != controller.gameObject)
                lighting.transform.SetParent(controller.transform, true);
            if (shadowLod.transform.parent == null && shadowLod.gameObject != controller.gameObject)
                shadowLod.transform.SetParent(controller.transform, true);
        }

        return new WorldGraphicsRig(controller, lighting, shadowLod);
    }

    public static void BindDistanceOrigin(ShadowLodController shadowLod)
    {
        if (shadowLod == null)
            return;

        if (shadowLod.distanceOrigin != null)
            return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            shadowLod.distanceOrigin = cam.transform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            shadowLod.distanceOrigin = player.transform;
            return;
        }

        CharacterController cc = Object.FindAnyObjectByType<CharacterController>();
        if (cc != null)
            shadowLod.distanceOrigin = cc.transform;
    }

    public static void EnsurePlayerAlwaysCasts()
    {
        Transform player = null;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            player = tagged.transform;

        if (player == null)
        {
            CharacterController cc = Object.FindAnyObjectByType<CharacterController>();
            if (cc != null)
                player = cc.transform;
        }

        if (player == null)
            return;

        ShadowLodOverride ov = player.GetComponent<ShadowLodOverride>();
        if (ov == null)
            ov = player.gameObject.AddComponent<ShadowLodOverride>();
        ov.mode = ShadowLodOverride.Mode.AlwaysCast;

        // Player layer should keep casting even without the override component on children.
        ShadowLodController lod = ShadowLodController.Instance != null
            ? ShadowLodController.Instance
            : Object.FindAnyObjectByType<ShadowLodController>();
        if (lod != null)
            lod.alwaysCastLayers |= (1 << player.gameObject.layer);
    }

    public readonly struct WorldGraphicsRig
    {
        public readonly GraphicsSettingsController controller;
        public readonly OptimizedWorldLighting lighting;
        public readonly ShadowLodController shadowLod;

        public WorldGraphicsRig(
            GraphicsSettingsController controller,
            OptimizedWorldLighting lighting,
            ShadowLodController shadowLod)
        {
            this.controller = controller;
            this.lighting = lighting;
            this.shadowLod = shadowLod;
        }
    }
}
