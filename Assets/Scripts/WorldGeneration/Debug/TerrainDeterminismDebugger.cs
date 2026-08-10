using UnityEngine;

/// <summary>
/// Runtime determinism validation helper.
/// Attach next to TerrainGenerator, or enable the built-in probe on TerrainGenerator.
///
/// Usage:
/// 1. Set world seed to a fixed value (e.g. 12345)
/// 2. Enter Play Mode and move near the probe chunk
/// 3. Note Height / Grass / Object counts
/// 4. Exit Play Mode, enter again — values must match exactly
/// </summary>
[RequireComponent(typeof(TerrainGenerator))]
public class TerrainDeterminismDebugger : MonoBehaviour
{
    [SerializeField] private Vector2Int probeChunk = Vector2Int.zero;
    [SerializeField] private bool logOnChunkReady = true;
    [SerializeField] private bool drawProbeGizmo = true;
    [SerializeField] private KeyCode reloadProbeKey = KeyCode.F6;

    private TerrainGenerator generator;
    private string cachedReport = "Waiting for chunk...";
    private string firstFingerprint;

    private void Awake()
    {
        generator = GetComponent<TerrainGenerator>();
    }

    private void Update()
    {
        if (generator == null)
            return;

        TerrainChunkData data = generator.GetChunkData(probeChunk);
        if (data == null || data.State < TerrainChunkData.ChunkState.Generated)
            return;

        float height = data.SampleHeightBilinear(data.ChunkSize * 0.5f, data.ChunkSize * 0.5f);
        BiomeSample sample = data.GetBiomeSample(data.Resolution / 2, data.Resolution / 2);
        string biomeName = sample.primaryBiome != null ? sample.primaryBiome.biomeName : "None";

        cachedReport =
            $"Seed: {generator.GetResolvedSeed()}\n" +
            $"Chunk: {probeChunk}\n" +
            $"Biome: {biomeName}\n" +
            $"Height sample: {height:F3}\n" +
            $"Grass count: {data.GrassInstanceCount}\n" +
            $"Object count: {data.ObjectInstanceCount}\n" +
            $"Fingerprint: {data.DeterminismFingerprint}";

        if (string.IsNullOrEmpty(firstFingerprint))
        {
            firstFingerprint = data.DeterminismFingerprint;
            if (logOnChunkReady)
                Debug.Log("[Determinism] First sample:\n" + cachedReport, this);
        }
        else if (logOnChunkReady && data.DeterminismFingerprint != firstFingerprint)
        {
            // HeightSum/grass can change with LOD/distance fade — compare seed-stable core via reload.
            Debug.LogWarning(
                "[Determinism] Fingerprint changed during session (may be LOD/distance fade).\n" +
                "First: " + firstFingerprint + "\nNow: " + data.DeterminismFingerprint,
                this);
            firstFingerprint = data.DeterminismFingerprint;
        }

        if (Input.GetKeyDown(reloadProbeKey))
        {
            Debug.Log("[Determinism] Manual probe:\n" + cachedReport, this);
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12, 160, 440, 150), "Terrain Determinism Debugger");
        GUI.Label(new Rect(24, 184, 416, 120), cachedReport);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawProbeGizmo || generator == null)
            return;

        // Approximate chunk size from probe if generator not exposing it — use 50 default visual.
        float size = 50f;
        Vector3 center = new Vector3((probeChunk.x + 0.5f) * size, 8f, (probeChunk.y + 0.5f) * size);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(center, new Vector3(size, 16f, size));
    }
}
