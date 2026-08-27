using System;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// Lightweight performance overlay (FPS + optional RAM / CPU / GPU).
/// Collectors run only while enabled and heavy stats sample at ~1 Hz.
/// </summary>
[DefaultExecutionOrder(1000)]
public class PerformanceMonitor : MonoBehaviour
{
    public const string PrefsEnabledKey = "PerfMonitor.Enabled";
    public const string PrefsShowExtrasKey = "PerfMonitor.ShowExtras";

    public static PerformanceMonitor Instance { get; private set; }

    [Header("Display")]
    [SerializeField] private bool enabledOnStart;
    [SerializeField] private bool showExtendedStats = true;
    [SerializeField] private Vector2 screenOffset = new Vector2(12f, 12f);
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Color textColor = new Color(0.85f, 1f, 0.55f, 0.95f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Sampling (keep low to protect frame time)")]
    [Tooltip("How often FPS text is rebuilt.")]
    [SerializeField] private float displayRefreshSeconds = 0.25f;
    [Tooltip("How often RAM/CPU/GPU are sampled. Higher = cheaper.")]
    [SerializeField] private float extrasSampleSeconds = 1f;

    public bool IsEnabled => overlayEnabled;
    public bool ShowExtendedStats => showExtendedStats;

    public event Action<bool> EnabledChanged;

    private bool overlayEnabled;
    private float fpsSmooth = 60f;
    private float fpsDisplay = 60f;
    private float msDisplay = 16.7f;
    private float nextDisplayRefresh;
    private float nextExtrasSample;

    private long allocatedMb;
    private long reservedMb;
    private long monoMb;
    private float processCpuPercent = -1f;
    private float gpuFrameMs = -1f;
    private string gpuLabel = "n/a";

    private TimeSpan lastProcessCpuTime;
    private float lastProcessCpuWallTime;
    private Process currentProcess;

    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder gpuRecorder;
    private bool recordersReady;

    private FrameTiming[] frameTimings = new FrameTiming[1];
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;
    private Texture2D boxTexture;
    private readonly StringBuilder textBuilder = new StringBuilder(128);
    private string cachedText = "FPS --";
    private Rect cachedRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (PlayerPrefs.HasKey(PrefsEnabledKey))
            overlayEnabled = PlayerPrefs.GetInt(PrefsEnabledKey, 0) == 1;
        else
            overlayEnabled = enabledOnStart;

        if (PlayerPrefs.HasKey(PrefsShowExtrasKey))
            showExtendedStats = PlayerPrefs.GetInt(PrefsShowExtrasKey, 1) == 1;

        // Don't destroy with scene loads so the toggle keeps working from Menu → Current.
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (overlayEnabled)
            BeginCollectors();
    }

    private void OnDisable()
    {
        EndCollectors();
    }

    private void OnDestroy()
    {
        EndCollectors();
        if (boxTexture != null)
            Destroy(boxTexture);
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!overlayEnabled)
            return;

        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            float instant = 1f / dt;
            fpsSmooth = Mathf.Lerp(fpsSmooth, instant, 1f - Mathf.Exp(-dt * 4f));
        }

        if (Time.unscaledTime >= nextDisplayRefresh)
        {
            nextDisplayRefresh = Time.unscaledTime + Mathf.Max(0.1f, displayRefreshSeconds);
            fpsDisplay = fpsSmooth;
            msDisplay = fpsDisplay > 0.01f ? 1000f / fpsDisplay : 0f;
            RebuildText();
        }

        if (showExtendedStats && Time.unscaledTime >= nextExtrasSample)
        {
            nextExtrasSample = Time.unscaledTime + Mathf.Max(0.5f, extrasSampleSeconds);
            SampleExtras();
            RebuildText();
        }
    }

    private void OnGUI()
    {
        if (!overlayEnabled)
            return;

        EnsureStyles();
        GUI.Box(cachedRect, GUIContent.none, boxStyle);
        GUI.Label(cachedRect, cachedText, labelStyle);
    }

    public void SetEnabled(bool enabled)
    {
        if (overlayEnabled == enabled)
            return;

        overlayEnabled = enabled;
        PlayerPrefs.SetInt(PrefsEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (enabled)
        {
            BeginCollectors();
            nextDisplayRefresh = 0f;
            nextExtrasSample = 0f;
        }
        else
        {
            EndCollectors();
        }

        EnabledChanged?.Invoke(enabled);
    }

    public void Toggle() => SetEnabled(!overlayEnabled);

    public void SetShowExtendedStats(bool show)
    {
        showExtendedStats = show;
        PlayerPrefs.SetInt(PrefsShowExtrasKey, show ? 1 : 0);
        PlayerPrefs.Save();

        if (overlayEnabled)
        {
            if (show)
            {
                nextExtrasSample = 0f;
            }
            RebuildText();
        }
    }

    // UI hooks
    public void OnToggleChanged(bool value) => SetEnabled(value);
    public void OnExtendedToggleChanged(bool value) => SetShowExtendedStats(value);

    private void BeginCollectors()
    {
        try
        {
            currentProcess = Process.GetCurrentProcess();
            lastProcessCpuTime = currentProcess.TotalProcessorTime;
            lastProcessCpuWallTime = Time.realtimeSinceStartup;
        }
        catch
        {
            currentProcess = null;
            processCpuPercent = -1f;
        }

        // FrameTimingManager is cheap when we only read at 1 Hz.
        FrameTimingManager.CaptureFrameTimings();

        InitProfilers();
    }

    private void EndCollectors()
    {
        if (mainThreadRecorder.Valid)
            mainThreadRecorder.Dispose();
        if (gpuRecorder.Valid)
            gpuRecorder.Dispose();
        recordersReady = false;
        currentProcess = null;
    }

    private void InitProfilers()
    {
        // ProfilerRecorder only while overlay is on — zero cost when disabled.
        if (recordersReady)
            return;

        try
        {
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 16);
            // "GPU Time in Frame" / Render category varies by Unity version; try a few names.
            gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 16);
            if (!gpuRecorder.Valid)
                gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Main Thread Frame Time", 16);
            recordersReady = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PerformanceMonitor: ProfilerRecorder unavailable ({e.Message}). FPS/RAM still work.");
            recordersReady = false;
        }
    }

    private void SampleExtras()
    {
        // Memory — very cheap.
        allocatedMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        reservedMb = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
        monoMb = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);

        // Process CPU % — sampled infrequently.
        SampleProcessCpu();

        // GPU / frame timings — infrequent.
        SampleGpu();
    }

    private void SampleProcessCpu()
    {
        if (currentProcess == null)
        {
            processCpuPercent = -1f;
            return;
        }

        try
        {
            currentProcess.Refresh();
            TimeSpan cpu = currentProcess.TotalProcessorTime;
            float wall = Time.realtimeSinceStartup;
            double cpuDelta = (cpu - lastProcessCpuTime).TotalSeconds;
            float wallDelta = wall - lastProcessCpuWallTime;

            if (wallDelta > 0.05f)
            {
                processCpuPercent = (float)(cpuDelta / wallDelta / Environment.ProcessorCount * 100.0);
                processCpuPercent = Mathf.Clamp(processCpuPercent, 0f, 100f * Environment.ProcessorCount);
            }

            lastProcessCpuTime = cpu;
            lastProcessCpuWallTime = wall;
        }
        catch
        {
            processCpuPercent = -1f;
        }
    }

    private void SampleGpu()
    {
        gpuFrameMs = -1f;
        gpuLabel = "n/a";

        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, frameTimings);
        if (count > 0 && frameTimings[0].gpuFrameTime > 0.0)
        {
            gpuFrameMs = (float)frameTimings[0].gpuFrameTime;
            gpuLabel = $"{gpuFrameMs:0.0} ms";
            return;
        }

        if (gpuRecorder.Valid && gpuRecorder.Count > 0)
        {
            // Recorder values are nanoseconds.
            long ns = gpuRecorder.LastValue;
            if (ns > 0)
            {
                gpuFrameMs = ns / 1_000_000f;
                gpuLabel = $"{gpuFrameMs:0.0} ms";
            }
        }
    }

    private void RebuildText()
    {
        textBuilder.Clear();
        textBuilder.Append("FPS ").Append(fpsDisplay.ToString("0")).Append("  (").Append(msDisplay.ToString("0.0")).Append(" ms)");

        if (showExtendedStats)
        {
            textBuilder.Append('\n')
                .Append("RAM ").Append(allocatedMb).Append(" / ").Append(reservedMb).Append(" MB")
                .Append("  Mono ").Append(monoMb).Append(" MB");

            textBuilder.Append('\n').Append("CPU ");
            if (processCpuPercent >= 0f)
                textBuilder.Append(processCpuPercent.ToString("0.0")).Append('%');
            else
                textBuilder.Append("n/a");

            textBuilder.Append("   GPU ").Append(gpuLabel);
        }

        cachedText = textBuilder.ToString();

        float width = showExtendedStats ? 320f : 180f;
        float height = showExtendedStats ? 64f : 28f;
        cachedRect = new Rect(screenOffset.x, screenOffset.y, width, height);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null)
            return;

        boxTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        boxTexture.SetPixel(0, 0, backgroundColor);
        boxTexture.Apply(false, true);

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = boxTexture }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(8, 8, 6, 6),
            normal = { textColor = textColor }
        };
    }

    /// <summary>Ensures a DontDestroyOnLoad monitor exists.</summary>
    public static PerformanceMonitor EnsureExists()
    {
        if (Instance != null)
            return Instance;

        PerformanceMonitor existing = FindAnyObjectByType<PerformanceMonitor>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("PerformanceMonitor");
        return go.AddComponent<PerformanceMonitor>();
    }
}
