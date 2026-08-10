using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Valheim-style procedural terrain orchestrator.
///
/// Pipeline:
/// Seed → Biome regions → Terrain sampling (blended) → Mesh → Vegetation (GPU) → Objects → Streaming
///
/// Vertex color channel layout (for TerrainBiomeBlend shader):
///   R = primary biome weight
///   G = secondary biome weight
///   B = primary biome index (0-1 normalized by /32)
///   A = secondary biome index (0-1 normalized by /32)
/// UV2.x = tertiary weight, UV2.y = tertiary biome index / 32
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    private const float BiomeIndexNormalize = 32f;

    #region Inspector

    [Header("World")]
    [SerializeField] private int chunkSize = 50;
    [SerializeField] private float worldRadius = 2000f;
    [Tooltip("Chunks whose center is outside this radius are not generated.")]
    [SerializeField] private bool enforceWorldBoundary = true;

    [Header("Seed")]
    [Tooltip("Primary world seed. Same seed = same world layout.")]
    [SerializeField] private int worldSeed = 12345;
    [Tooltip("Optional string seed. If set, overrides worldSeed via a stable hash.")]
    [SerializeField] private string seedString = "";
    [SerializeField] private bool useSeedString;

    [Header("Terrain")]
    [SerializeField] private float heightScale = 20f;
    [SerializeField] private AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float uvScale = 2f;
    [Tooltip("Render mesh resolution along one chunk edge (vertices = resolution+1).")]
    [SerializeField] private int renderResolution = 50;
    [Tooltip("Collider mesh resolution (can be lower than render).")]
    [SerializeField] private int colliderResolution = 25;

    [Header("Noise")]
    [SerializeField] private TerrainNoiseSettings noiseSettings = new TerrainNoiseSettings();

    [Header("Biomes")]
    [SerializeField] private Biome[] biomes;
    [Tooltip("Approximate spacing between biome capitals in world units.")]
    [SerializeField] private float capitalSpacing = 180f;
    [Range(0f, 0.5f)]
    [SerializeField] private float capitalJitter = 0.35f;
    [SerializeField] private float borderWarpScale = 90f;
    [SerializeField] private float borderWarpStrength = 28f;
    [SerializeField] private float spawnBiomeRadius = 220f;

    [Header("Biome Blending")]
    [Tooltip("World-unit width of soft biome transitions.")]
    [SerializeField] private float biomeBlendDistance = 48f;
    [SerializeField] private Material terrainBlendMaterial;
    [SerializeField] private bool colorByBiome = true;

    [Header("Elevation Materials (Fallback)")]
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Material dirtMaterial;
    [SerializeField] private Material rockMaterial;
    [SerializeField] private Material snowMaterial;
    [SerializeField] private float grassHeight;
    [SerializeField] private float dirtHeight = 5f;
    [SerializeField] private float rockHeight = 10f;
    [SerializeField] private float snowHeight = 17f;

    [Header("Vegetation")]
    [SerializeField] private float grassCellSize = 2f;
    [SerializeField] private bool enableGrass = true;
    [SerializeField] private bool createDefaultGrassIfMissing = true;

    [Header("Objects")]
    [SerializeField] private int maxObjectsPerChunk = 100;
    [SerializeField] private float objectSpacing = 2f;
    [Range(0f, 1f)]
    [SerializeField] private float objectAttemptChance = 0.12f;

    [Header("Chunk Streaming")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float terrainLoadDistance = 200f;
    [SerializeField] private float terrainUnloadDistance = 300f;
    [SerializeField] private float vegetationLoadDistance = 120f;
    [SerializeField] private float vegetationUnloadDistance = 160f;
    [SerializeField] private float objectLoadDistance = 180f;
    [SerializeField] private float objectUnloadDistance = 240f;

    [Header("LOD")]
    [Tooltip("Within this distance: full render resolution.")]
    [SerializeField] private float lod0Distance = 120f;
    [Tooltip("Within this distance: half resolution. Beyond: quarter.")]
    [SerializeField] private float lod1Distance = 220f;

    [Header("Performance")]
    [SerializeField] private int maxChunkGenerationsPerFrame = 1;
    [SerializeField] private int meshVertexBudgetPerFrame = 2500;
    [SerializeField] private bool useCoroutineGeneration = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;
    [SerializeField] private bool showChunkBoundaries;
    [SerializeField] private bool showBiomeBoundaries;
    [SerializeField] private bool showBiomeBlendWeights;
    [SerializeField] private bool showVegetationDensity;
    [SerializeField] private bool showTerrainNormals;
    [SerializeField] private bool enableDeterminismProbe;
    [SerializeField] private Vector2Int determinismProbeChunk = Vector2Int.zero;

    #endregion

    private readonly WorldSeed seed = new WorldSeed();
    private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new Dictionary<Vector2Int, TerrainChunk>();
    private readonly Dictionary<Vector2Int, TerrainChunkData> chunkDataMap = new Dictionary<Vector2Int, TerrainChunkData>();
    private readonly HashSet<Vector2Int> pendingGenerate = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> sortedLoadList = new List<Vector2Int>(128);
    private readonly List<VegetationInstance> grassScratch = new List<VegetationInstance>(1024);
    private readonly List<ObjectSpawnRecord> objectScratch = new List<ObjectSpawnRecord>(64);

    // Reusable mesh buffers (avoid per-chunk alloc of temp lists where possible).
    private Vector3[] vertexBuffer;
    private Vector2[] uvBuffer;
    private Vector2[] uv2Buffer;
    private Color[] colorBuffer;
    private Vector3[] normalBuffer;
    private int[] triangleBuffer;

    private BiomeSystem biomeSystem;
    private VegetationSystem vegetationSystem;
    private VegetationRenderer vegetationRenderer;
    private GameObject chunksContainer;
    private Material sharedTerrainMaterial;
    private Mesh defaultGrassMesh;
    private Material defaultGrassMaterial;

    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private Vector3 lastVegetationPlayerPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    private int groundLayerId;
    private int generationTokenCounter = 1;
    private int totalChunksGenerated;
    private bool generationLoopRunning;
    private string lastDeterminismReport = "";
    private const float VegetationRefreshMoveThreshold = 4f;

    #region Unity Lifecycle

    private void Start()
    {
        seed.Configure(worldSeed, seedString, useSeedString);
        seed.Resolve();

        groundLayerId = LayerMask.NameToLayer("Ground");
        if (groundLayerId == -1)
        {
            Debug.LogWarning("[TerrainGenerator] 'Ground' layer not found. Using Default layer.");
            groundLayerId = 0;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            playerTransform = player != null ? player.transform : null;
            if (playerTransform == null)
                Debug.LogError("[TerrainGenerator] Player not found. Assign playerTransform or tag a GameObject as 'Player'.");
        }

        chunksContainer = new GameObject("Terrain_Chunks");
        chunksContainer.transform.SetParent(transform, false);

        EnsureDefaultMaterials();
        EnsureDefaultBiomes();
        EnsureDefaultGrassPrototypes();

        biomeSystem = new BiomeSystem(
            seed,
            biomes,
            worldRadius,
            capitalSpacing,
            capitalJitter,
            borderWarpScale,
            borderWarpStrength,
            spawnBiomeRadius,
            biomeBlendDistance);

        vegetationSystem = new VegetationSystem(seed, grassCellSize);

        vegetationRenderer = GetComponent<VegetationRenderer>();
        if (vegetationRenderer == null)
            vegetationRenderer = gameObject.AddComponent<VegetationRenderer>();
        vegetationRenderer.Initialize(biomes);

        sharedTerrainMaterial = terrainBlendMaterial != null
            ? terrainBlendMaterial
            : CreateFallbackTerrainMaterial();

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TerrainGenerator] Seed={seed.ResolvedSeed} Capitals={biomeSystem.CapitalCount} Biomes={biomes?.Length ?? 0} WorldRadius={worldRadius}",
                gameObject);
        }

        if (playerTransform != null)
            RequestChunkUpdate(force: true);

        if (useCoroutineGeneration && !generationLoopRunning)
            StartCoroutine(ChunkGenerationLoop());
    }

    private void Update()
    {
        if (playerTransform == null)
            return;

        Vector2Int playerChunk = GetChunkCoordinates(playerTransform.position);
        if (playerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = playerChunk;
            RequestChunkUpdate(force: false);
        }

        UpdateVegetationDistanceFade();
        UpdateObjectStreaming();
        DrawVegetation();

        if (enableDeterminismProbe)
            RefreshDeterminismProbe();
    }

    private void LateUpdate()
    {
        if (!useCoroutineGeneration && playerTransform != null)
            ProcessPendingGenerationsSync();
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        if (showChunkBoundaries)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
            foreach (var kvp in activeChunks)
            {
                Vector3 center = new Vector3(
                    (kvp.Key.x + 0.5f) * chunkSize,
                    0f,
                    (kvp.Key.y + 0.5f) * chunkSize);
                Gizmos.DrawWireCube(center + Vector3.up * 2f, new Vector3(chunkSize, 4f, chunkSize));
            }
        }

        if (showBiomeBoundaries && biomeSystem != null)
        {
            var capitals = biomeSystem.GetCapitalPositionsForDebug();
            for (int i = 0; i < capitals.Count; i++)
            {
                Gizmos.color = biomeSystem.GetCapitalBiomeColor(i);
                Vector3 p = new Vector3(capitals[i].x, 5f, capitals[i].y);
                Gizmos.DrawSphere(p, 6f);
            }
        }

        if (showTerrainNormals)
        {
            Gizmos.color = Color.cyan;
            foreach (var kvp in chunkDataMap)
            {
                TerrainChunkData data = kvp.Value;
                if (data?.Normals == null || data.State < TerrainChunkData.ChunkState.Generated)
                    continue;

                int step = Mathf.Max(1, data.Resolution / 8);
                for (int z = 0; z <= data.Resolution; z += step)
                {
                    for (int x = 0; x <= data.Resolution; x += step)
                    {
                        int idx = z * (data.Resolution + 1) + x;
                        float wx = kvp.Key.x * chunkSize + (x / (float)data.Resolution) * chunkSize;
                        float wz = kvp.Key.y * chunkSize + (z / (float)data.Resolution) * chunkSize;
                        Vector3 pos = new Vector3(wx, data.Heights[idx], wz);
                        Gizmos.DrawLine(pos, pos + data.Normals[idx] * 2f);
                    }
                }
            }
        }

        if (showBiomeBlendWeights)
        {
            foreach (var kvp in chunkDataMap)
            {
                TerrainChunkData data = kvp.Value;
                if (data?.BiomeSamples == null)
                    continue;

                int step = Mathf.Max(1, data.Resolution / 6);
                for (int z = 0; z <= data.Resolution; z += step)
                {
                    for (int x = 0; x <= data.Resolution; x += step)
                    {
                        BiomeSample s = data.GetBiomeSample(x, z);
                        float wx = kvp.Key.x * chunkSize + (x / (float)data.Resolution) * chunkSize;
                        float wz = kvp.Key.y * chunkSize + (z / (float)data.Resolution) * chunkSize;
                        float h = data.GetHeight(x, z);
                        Gizmos.color = new Color(s.primaryWeight, s.secondaryWeight, 0f, 1f);
                        Gizmos.DrawCube(new Vector3(wx, h + 0.5f, wz), Vector3.one * 0.6f);
                    }
                }
            }
        }
    }

    #endregion

    #region Public API

    public int GetResolvedSeed() => seed.ResolvedSeed;

    public BiomeSample GetBiomeSample(Vector3 worldPosition)
    {
        EnsureSystems();
        return biomeSystem.SampleBiome(worldPosition.x, worldPosition.z);
    }

    public float GetTerrainHeight(Vector3 worldPosition)
    {
        EnsureSystems();
        Vector2Int coord = GetChunkCoordinates(worldPosition);
        if (chunkDataMap.TryGetValue(coord, out TerrainChunkData data) && data.Heights != null)
        {
            float localX = worldPosition.x - coord.x * chunkSize;
            float localZ = worldPosition.z - coord.y * chunkSize;
            return data.SampleHeightBilinear(localX, localZ);
        }

        return SampleTerrainHeight(worldPosition.x, worldPosition.z);
    }

    public TerrainChunkData GetChunkData(Vector2Int chunkCoordinate)
    {
        chunkDataMap.TryGetValue(chunkCoordinate, out TerrainChunkData data);
        return data;
    }

    public bool IsChunkLoaded(Vector2Int coordinate) => activeChunks.ContainsKey(coordinate);

    public int GetActiveChunkCount() => activeChunks.Count;
    public int GetTotalChunksGenerated() => totalChunksGenerated;
    public string GetLastDeterminismReport() => lastDeterminismReport;

    /// <summary>Legacy helper preserved from original API.</summary>
    public Biome GetBiomeAtWorldPosition(Vector3 worldPosition)
    {
        BiomeSample sample = GetBiomeSample(worldPosition);
        return sample.primaryBiome;
    }

    #endregion

    #region Streaming

    private void RequestChunkUpdate(bool force)
    {
        Vector3 playerPos = playerTransform.position;
        Vector2Int playerChunk = GetChunkCoordinates(playerPos);

        float loadDistSq = terrainLoadDistance * terrainLoadDistance;
        float unloadDistSq = terrainUnloadDistance * terrainUnloadDistance;

        int chunkLoadRadius = Mathf.CeilToInt(terrainLoadDistance / chunkSize);
        sortedLoadList.Clear();

        for (int x = -chunkLoadRadius; x <= chunkLoadRadius; x++)
        {
            for (int z = -chunkLoadRadius; z <= chunkLoadRadius; z++)
            {
                Vector2Int chunkCoord = playerChunk + new Vector2Int(x, z);
                if (!IsChunkInsideWorld(chunkCoord))
                    continue;

                float distSq = ChunkDistanceSq(playerChunk, chunkCoord);
                if (distSq <= loadDistSq)
                    sortedLoadList.Add(chunkCoord);
            }
        }

        // Prioritize nearest chunks first.
        Vector2Int pc = playerChunk;
        sortedLoadList.Sort((a, b) =>
        {
            float da = ChunkDistanceSq(pc, a);
            float db = ChunkDistanceSq(pc, b);
            return da.CompareTo(db);
        });

        for (int i = 0; i < sortedLoadList.Count; i++)
        {
            Vector2Int coord = sortedLoadList[i];
            if (!activeChunks.ContainsKey(coord) && !pendingGenerate.Contains(coord))
                pendingGenerate.Add(coord);
        }

        // Unload far chunks (hysteresis via unload > load).
        List<Vector2Int> toUnload = null;
        foreach (var kvp in activeChunks)
        {
            float distSq = ChunkDistanceSq(playerChunk, kvp.Key);
            if (distSq > unloadDistSq)
            {
                if (toUnload == null)
                    toUnload = new List<Vector2Int>(8);
                toUnload.Add(kvp.Key);
            }
        }

        if (toUnload != null)
        {
            for (int i = 0; i < toUnload.Count; i++)
                UnloadChunk(toUnload[i]);
        }

        if (showDebugLogs && (force || toUnload != null))
            Debug.Log($"[TerrainGenerator] Player chunk {playerChunk}. Active={activeChunks.Count} Pending={pendingGenerate.Count}", gameObject);
    }

    private float ChunkDistanceSq(Vector2Int a, Vector2Int b)
    {
        float dx = (a.x - b.x) * chunkSize;
        float dz = (a.y - b.y) * chunkSize;
        return dx * dx + dz * dz;
    }

    private bool IsChunkInsideWorld(Vector2Int chunkCoord)
    {
        if (!enforceWorldBoundary)
            return true;

        float cx = (chunkCoord.x + 0.5f) * chunkSize;
        float cz = (chunkCoord.y + 0.5f) * chunkSize;
        return (cx * cx + cz * cz) <= worldRadius * worldRadius;
    }

    private void UnloadChunk(Vector2Int coord)
    {
        pendingGenerate.Remove(coord);

        if (chunkDataMap.TryGetValue(coord, out TerrainChunkData data))
        {
            data.Invalidate(++generationTokenCounter);
            chunkDataMap.Remove(coord);
        }

        if (activeChunks.TryGetValue(coord, out TerrainChunk chunk))
        {
            if (showDebugLogs)
                Debug.Log($"[TerrainGenerator] Unload chunk {coord}", gameObject);

            Destroy(chunk.gameObject);
            activeChunks.Remove(coord);
        }
    }

    #endregion

    #region Generation Pipeline

    private IEnumerator ChunkGenerationLoop()
    {
        generationLoopRunning = true;
        while (enabled)
        {
            int generated = 0;
            while (generated < maxChunkGenerationsPerFrame && TryDequeueNearest(out Vector2Int coord))
            {
                yield return GenerateChunkStaged(coord);
                generated++;
            }

            yield return null;
        }

        generationLoopRunning = false;
    }

    private void ProcessPendingGenerationsSync()
    {
        int generated = 0;
        while (generated < maxChunkGenerationsPerFrame && TryDequeueNearest(out Vector2Int coord))
        {
            // Synchronous fallback path (still staged logically, but completes this frame).
            IEnumerator e = GenerateChunkStaged(coord);
            while (e.MoveNext()) { }
            generated++;
        }
    }

    private bool TryDequeueNearest(out Vector2Int coord)
    {
        coord = default;
        if (pendingGenerate.Count == 0 || playerTransform == null)
            return false;

        Vector2Int playerChunk = GetChunkCoordinates(playerTransform.position);
        float best = float.MaxValue;
        bool found = false;
        Vector2Int bestCoord = default;

        foreach (Vector2Int c in pendingGenerate)
        {
            float d = ChunkDistanceSq(playerChunk, c);
            if (d < best)
            {
                best = d;
                bestCoord = c;
                found = true;
            }
        }

        if (!found)
            return false;

        pendingGenerate.Remove(bestCoord);
        coord = bestCoord;
        return true;
    }

    private IEnumerator GenerateChunkStaged(Vector2Int chunkCoord)
    {
        if (activeChunks.ContainsKey(chunkCoord))
            yield break;

        if (!IsChunkInsideWorld(chunkCoord))
            yield break;

        int token = ++generationTokenCounter;
        int lod = GetLodLevel(chunkCoord);
        int resolution = GetResolutionForLod(lod);

        TerrainChunkData data = new TerrainChunkData(chunkCoord, chunkSize);
        data.BeginGeneration(lod, resolution, token);
        chunkDataMap[chunkCoord] = data;

        // Stage 1: deterministic height + biome samples.
        yield return BuildChunkSamples(data, token);
        if (!data.IsTokenValid(token) || !chunkDataMap.ContainsKey(chunkCoord))
            yield break;

        // Stage 2: normals.
        BuildNormals(data);
        if (!data.IsTokenValid(token))
            yield break;

        yield return null;

        // Stage 3: create GameObject + render mesh.
        GameObject chunkObject = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
        chunkObject.transform.SetParent(chunksContainer.transform, false);
        chunkObject.transform.position = new Vector3(chunkCoord.x * chunkSize, 0f, chunkCoord.y * chunkSize);

        TerrainChunk chunk = chunkObject.AddComponent<TerrainChunk>();
        chunk.BindData(data);
        chunk.EnsureHierarchy(groundLayerId);

        Mesh renderMesh = BuildRenderMesh(data);
        chunk.ApplyRenderMesh(renderMesh, sharedTerrainMaterial);

        yield return null;
        if (!data.IsTokenValid(token) || !chunkDataMap.ContainsKey(chunkCoord))
        {
            Destroy(chunkObject);
            yield break;
        }

        // Stage 4: collider (optional lower res).
        Mesh colMesh = BuildColliderMesh(data);
        chunk.ApplyColliderMesh(colMesh);

        yield return null;
        if (!data.IsTokenValid(token) || !chunkDataMap.ContainsKey(chunkCoord))
        {
            Destroy(chunkObject);
            yield break;
        }

        // Stage 5: important objects (GameObjects) if in object distance.
        float objectDistSq = objectLoadDistance * objectLoadDistance;
        if (playerTransform != null && ChunkDistanceSq(GetChunkCoordinates(playerTransform.position), chunkCoord) <= objectDistSq)
        {
            SpawnImportantObjects(chunk, data);
        }

        // Stage 6: vegetation data (GPU instances).
        float vegDistSq = vegetationLoadDistance * vegetationLoadDistance;
        if (enableGrass && playerTransform != null &&
            ChunkDistanceSq(GetChunkCoordinates(playerTransform.position), chunkCoord) <= vegDistSq)
        {
            Dictionary<int, List<Matrix4x4>> matrices = vegetationRenderer.CreateMatrixStore();
            vegetationSystem.GenerateGrass(data, playerTransform.position, vegetationLoadDistance, grassScratch, matrices);
            chunk.SetGrassMatrices(matrices);
        }

        data.MarkGenerated();
        data.MarkVisible();
        data.BuildFingerprint(seed.ResolvedSeed);

        activeChunks[chunkCoord] = chunk;
        totalChunksGenerated++;

        if (showDebugLogs)
            Debug.Log($"[TerrainGenerator] Generated chunk {chunkCoord} | {data.DeterminismFingerprint}", gameObject);
    }

    private IEnumerator BuildChunkSamples(TerrainChunkData data, int token)
    {
        int res = data.Resolution;
        int vertCount = (res + 1) * (res + 1);
        EnsureVertexCapacity(vertCount);

        int processed = 0;
        Vector2Int coord = data.Coordinate;

        // Prefetch chunk corners for cheap bilinear, refine near blends.
        BiomeSample[] corners = biomeSystem.GetOrCreateChunkCornerSamples(coord, chunkSize);

        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                if (!data.IsTokenValid(token))
                    yield break;

                float u = x / (float)res;
                float v = z / (float)res;
                float worldX = coord.x * chunkSize + u * chunkSize;
                float worldZ = coord.y * chunkSize + v * chunkSize;

                BiomeSample sample = biomeSystem.SampleBiomeBilinear(corners, u, v);

                // Refine expensive full sample near borders / spawn ring.
                if (sample.secondaryWeight > 0.05f || sample.primaryWeight < 0.92f)
                    sample = biomeSystem.SampleBiome(worldX, worldZ);

                float prelimHeight = SampleTerrainHeight(worldX, worldZ);
                float normalizedDistance = Mathf.Clamp01(Mathf.Sqrt(worldX * worldX + worldZ * worldZ) / worldRadius);
                sample = biomeSystem.ApplyElevationGate(sample, prelimHeight, normalizedDistance);

                float height = EvaluateBlendedTerrainHeight(sample, worldX, worldZ);

                int idx = z * (res + 1) + x;
                data.Heights[idx] = height;
                data.BiomeSamples[idx] = sample;
                data.VertexColors[idx] = PackBiomeVertexColor(sample, height);

                processed++;
                if (processed >= meshVertexBudgetPerFrame)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }
    }

    private float EvaluateBlendedTerrainHeight(BiomeSample sample, float worldX, float worldZ)
    {
        float h = 0f;
        float w = 0f;

        if (sample.primaryBiome != null)
        {
            float hh = TerrainNoise.EvaluateBiomeTerrain(seed, sample.primaryBiome.terrain, noiseSettings, worldX, worldZ);
            h += hh * sample.primaryWeight;
            w += sample.primaryWeight;
        }

        if (sample.secondaryBiome != null && sample.secondaryWeight > 0f)
        {
            float hh = TerrainNoise.EvaluateBiomeTerrain(seed, sample.secondaryBiome.terrain, noiseSettings, worldX, worldZ);
            h += hh * sample.secondaryWeight;
            w += sample.secondaryWeight;
        }

        if (sample.tertiaryBiome != null && sample.tertiaryWeight > 0f)
        {
            float hh = TerrainNoise.EvaluateBiomeTerrain(seed, sample.tertiaryBiome.terrain, noiseSettings, worldX, worldZ);
            h += hh * sample.tertiaryWeight;
            w += sample.tertiaryWeight;
        }

        if (w <= 0.0001f)
        {
            // Fallback: global layered noise * legacy heightCurve/heightScale.
            float n = TerrainNoise.SampleLayeredBase(seed, noiseSettings, worldX, worldZ);
            return heightCurve.Evaluate(n) * heightScale;
        }

        return h / w;
    }

    /// <summary>
    /// Cheap height estimate used for elevation gating / API when chunk data is missing.
    /// Uses primary biome only (no blend) for speed/determinism consistency on coarse queries.
    /// </summary>
    private float SampleTerrainHeight(float worldX, float worldZ)
    {
        BiomeSample sample = biomeSystem.SampleBiome(worldX, worldZ);
        return EvaluateBlendedTerrainHeight(sample, worldX, worldZ);
    }

    private void BuildNormals(TerrainChunkData data)
    {
        int res = data.Resolution;
        float step = chunkSize / (float)res;

        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                int idx = z * (res + 1) + x;
                float hL = data.GetHeight(Mathf.Max(0, x - 1), z);
                float hR = data.GetHeight(Mathf.Min(res, x + 1), z);
                float hD = data.GetHeight(x, Mathf.Max(0, z - 1));
                float hU = data.GetHeight(x, Mathf.Min(res, z + 1));

                Vector3 normal = new Vector3(hL - hR, step * 2f, hD - hU).normalized;
                data.Normals[idx] = normal;
            }
        }
    }

    private Mesh BuildRenderMesh(TerrainChunkData data)
    {
        int res = data.Resolution;
        int vertsPerSide = res + 1;
        int vertCount = vertsPerSide * vertsPerSide;
        EnsureVertexCapacity(vertCount);
        EnsureTriangleCapacity(res * res * 6);

        Vector2Int coord = data.Coordinate;

        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                int idx = z * vertsPerSide + x;
                float u = x / (float)res;
                float v = z / (float)res;
                float localX = u * chunkSize;
                float localZ = v * chunkSize;
                float height = data.Heights[idx];
                BiomeSample sample = data.BiomeSamples[idx];

                vertexBuffer[idx] = new Vector3(localX, height, localZ);
                uvBuffer[idx] = new Vector2(u * uvScale, v * uvScale);
                colorBuffer[idx] = data.VertexColors[idx];
                normalBuffer[idx] = data.Normals[idx];

                float tertiaryWeight = sample.tertiaryWeight;
                float tertiaryIndex = sample.tertiaryBiome != null
                    ? (sample.tertiaryBiome.runtimeIndex + 0.5f) / BiomeIndexNormalize
                    : 0f;
                uv2Buffer[idx] = new Vector2(tertiaryWeight, tertiaryIndex);

                if (colorByBiome && sample.primaryBiome != null && terrainBlendMaterial == null)
                {
                    // Tint fallback when no blend shader assigned.
                    Color c = sample.primaryBiome.biomeColor;
                    c.r = Mathf.Lerp(c.r, sample.secondaryBiome != null ? sample.secondaryBiome.biomeColor.r : c.r, sample.secondaryWeight);
                    c.g = Mathf.Lerp(c.g, sample.secondaryBiome != null ? sample.secondaryBiome.biomeColor.g : c.g, sample.secondaryWeight);
                    c.b = Mathf.Lerp(c.b, sample.secondaryBiome != null ? sample.secondaryBiome.biomeColor.b : c.b, sample.secondaryWeight);
                    // Preserve packed weights in a debug-friendly way only if blend mat missing:
                    // keep packed channels (shader docs) — designers should assign TerrainBiomeBlend.
                    colorBuffer[idx] = PackBiomeVertexColor(sample, height);
                }
            }
        }

        int t = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int topLeft = z * vertsPerSide + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * vertsPerSide + x;
                int bottomRight = bottomLeft + 1;

                triangleBuffer[t++] = topLeft;
                triangleBuffer[t++] = bottomLeft;
                triangleBuffer[t++] = topRight;
                triangleBuffer[t++] = topRight;
                triangleBuffer[t++] = bottomLeft;
                triangleBuffer[t++] = bottomRight;
            }
        }

        Mesh mesh = new Mesh { name = $"Ground_{coord.x}_{coord.y}" };
        if (vertCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertexBuffer, 0, vertCount);
        mesh.SetNormals(normalBuffer, 0, vertCount);
        mesh.SetColors(colorBuffer, 0, vertCount);
        mesh.SetUVs(0, uvBuffer, 0, vertCount);
        mesh.SetUVs(1, uv2Buffer, 0, vertCount);
        mesh.SetTriangles(triangleBuffer, 0, t, 0, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildColliderMesh(TerrainChunkData data)
    {
        int colRes = Mathf.Clamp(colliderResolution, 2, data.Resolution);
        if (colRes == data.Resolution)
        {
            // Reuse render mesh path data via a dedicated mesh copy of heights at full res.
            // Build a light mesh for collider from existing heights.
            colRes = data.Resolution;
        }

        int vertsPerSide = colRes + 1;
        Vector3[] verts = new Vector3[vertsPerSide * vertsPerSide];
        int[] tris = new int[colRes * colRes * 6];

        for (int z = 0; z <= colRes; z++)
        {
            for (int x = 0; x <= colRes; x++)
            {
                float u = x / (float)colRes;
                float v = z / (float)colRes;
                float localX = u * chunkSize;
                float localZ = v * chunkSize;
                float height = data.SampleHeightBilinear(localX, localZ);
                verts[z * vertsPerSide + x] = new Vector3(localX, height, localZ);
            }
        }

        int t = 0;
        for (int z = 0; z < colRes; z++)
        {
            for (int x = 0; x < colRes; x++)
            {
                int topLeft = z * vertsPerSide + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * vertsPerSide + x;
                int bottomRight = bottomLeft + 1;
                tris[t++] = topLeft;
                tris[t++] = bottomLeft;
                tris[t++] = topRight;
                tris[t++] = topRight;
                tris[t++] = bottomLeft;
                tris[t++] = bottomRight;
            }
        }

        Mesh mesh = new Mesh { name = $"Collider_{data.Coordinate.x}_{data.Coordinate.y}" };
        if (verts.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Color PackBiomeVertexColor(BiomeSample sample, float height)
    {
        float primaryIndex = sample.primaryBiome != null
            ? (sample.primaryBiome.runtimeIndex + 0.5f) / BiomeIndexNormalize
            : 0f;
        float secondaryIndex = sample.secondaryBiome != null
            ? (sample.secondaryBiome.runtimeIndex + 0.5f) / BiomeIndexNormalize
            : 0f;

        return new Color(
            Mathf.Clamp01(sample.primaryWeight),
            Mathf.Clamp01(sample.secondaryWeight),
            Mathf.Clamp01(primaryIndex),
            Mathf.Clamp01(secondaryIndex));
    }

    #endregion

    #region Objects

    private void SpawnImportantObjects(TerrainChunk chunk, TerrainChunkData data)
    {
        objectScratch.Clear();
        Vector2Int coord = data.Coordinate;
        System.Random rng = seed.CreateChunkRandom(coord, WorldSeed.SystemObjects);

        int stride = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(objectSpacing, 1f)));
        int spawned = 0;

        for (int x = 0; x < chunkSize && spawned < maxObjectsPerChunk; x += stride)
        {
            for (int z = 0; z < chunkSize && spawned < maxObjectsPerChunk; z += stride)
            {
                if (rng.NextDouble() > objectAttemptChance)
                    continue;

                float localX = x + (float)rng.NextDouble() * stride;
                float localZ = z + (float)rng.NextDouble() * stride;
                if (localX > chunkSize || localZ > chunkSize)
                    continue;

                float height = data.SampleHeightBilinear(localX, localZ);
                Vector3 normal = data.SampleNormalBilinear(localX, localZ);
                float slope = Vector3.Angle(normal, Vector3.up);

                BiomeSample sample = SampleBiomeFromData(data, localX, localZ);
                Biome biome = sample.primaryBiome;
                if (biome?.objects == null)
                    continue;

                for (int i = 0; i < biome.objects.Length; i++)
                {
                    BiomeObject obj = biome.objects[i];
                    if (obj?.prefab == null)
                        continue;

                    // Decorative objects are skipped here — grass/flowers use VegetationSystem.
                    if (obj.category == BiomeObject.ObjectCategory.Decorative)
                        continue;

                    if (rng.NextDouble() > obj.spawnChance)
                        continue;

                    if (height < obj.minElevation || height > obj.maxElevation)
                        continue;

                    if (slope > obj.maxSlope)
                        continue;

                    float scatter = Mathf.Max(0.1f, obj.minimumDistance > 0f ? obj.minimumDistance : objectSpacing);
                    float worldX = coord.x * chunkSize + localX + ((float)rng.NextDouble() * 2f - 1f) * scatter * 0.35f;
                    float worldZ = coord.y * chunkSize + localZ + ((float)rng.NextDouble() * 2f - 1f) * scatter * 0.35f;
                    float y = data.SampleHeightBilinear(worldX - coord.x * chunkSize, worldZ - coord.y * chunkSize);

                    float xRot = Mathf.Lerp(obj.minXRotation, obj.maxXRotation, (float)rng.NextDouble());
                    float yRot = Mathf.Lerp(obj.minYRotation, obj.maxYRotation, (float)rng.NextDouble());
                    Quaternion rotation = Quaternion.Euler(xRot, yRot, 0f);

                    GameObject instance = Instantiate(obj.prefab, new Vector3(worldX, y, worldZ), rotation, chunk.ObjectsRoot);
                    PositionObjectOnTerrain(instance, y, obj.yOffset);
                    instance.name = $"{obj.prefab.name}_{coord.x}_{coord.y}_{spawned}";
                    chunk.RegisterSpawnedObject(instance);
                    spawned++;
                    break;
                }
            }
        }

        data.ObjectInstanceCount = spawned;
    }

    private static BiomeSample SampleBiomeFromData(TerrainChunkData data, float localX, float localZ)
    {
        float u = localX / data.ChunkSize * data.Resolution;
        float v = localZ / data.ChunkSize * data.Resolution;
        int x = Mathf.Clamp(Mathf.RoundToInt(u), 0, data.Resolution);
        int z = Mathf.Clamp(Mathf.RoundToInt(v), 0, data.Resolution);
        return data.GetBiomeSample(x, z);
    }

    private void PositionObjectOnTerrain(GameObject objectInstance, float terrainElevation, float yOffset = 0f)
    {
        Collider[] colliders = objectInstance.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            SetY(objectInstance, terrainElevation + bounds.extents.y + yOffset);
            return;
        }

        Renderer[] renderers = objectInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            SetY(objectInstance, terrainElevation + bounds.extents.y + yOffset);
            return;
        }

        SetY(objectInstance, terrainElevation + yOffset);
    }

    private static void SetY(GameObject go, float y)
    {
        Vector3 p = go.transform.position;
        p.y = y;
        go.transform.position = p;
    }

    #endregion

    #region Vegetation / Object Distance / Draw

    private void UpdateObjectStreaming()
    {
        if (playerTransform == null)
            return;

        Vector2Int playerChunk = GetChunkCoordinates(playerTransform.position);
        float loadSq = objectLoadDistance * objectLoadDistance;
        float unloadSq = objectUnloadDistance * objectUnloadDistance;

        foreach (var kvp in activeChunks)
        {
            TerrainChunk chunk = kvp.Value;
            if (chunk?.Data == null || chunk.Data.State < TerrainChunkData.ChunkState.Generated)
                continue;

            float distSq = ChunkDistanceSq(playerChunk, kvp.Key);
            bool hasObjects = chunk.SpawnedObjects != null && chunk.SpawnedObjects.Count > 0;

            if (distSq > unloadSq && hasObjects)
            {
                chunk.ClearSpawnedObjects();
                chunk.Data.ObjectInstanceCount = 0;
            }
            else if (distSq <= loadSq && !hasObjects)
            {
                SpawnImportantObjects(chunk, chunk.Data);
                chunk.Data.BuildFingerprint(seed.ResolvedSeed);
            }
        }
    }

    private void UpdateVegetationDistanceFade()
    {
        if (!enableGrass || playerTransform == null || vegetationSystem == null)
            return;

        Vector3 playerPos = playerTransform.position;
        float movedSq = (playerPos - lastVegetationPlayerPos).sqrMagnitude;
        if (movedSq < VegetationRefreshMoveThreshold * VegetationRefreshMoveThreshold)
            return;

        lastVegetationPlayerPos = playerPos;
        Vector2Int playerChunk = GetChunkCoordinates(playerPos);
        float vegLoadSq = vegetationLoadDistance * vegetationLoadDistance;
        float vegUnloadSq = vegetationUnloadDistance * vegetationUnloadDistance;

        foreach (var kvp in activeChunks)
        {
            TerrainChunk chunk = kvp.Value;
            if (chunk?.Data == null)
                continue;

            float distSq = ChunkDistanceSq(playerChunk, kvp.Key);

            if (distSq > vegUnloadSq)
            {
                chunk.ClearGrassMatrices();
                continue;
            }

            if (distSq <= vegLoadSq)
            {
                // Refresh density fade from current player position.
                // Instance transforms remain deterministic for seed+cell; count fades with distance.
                Dictionary<int, List<Matrix4x4>> matrices = vegetationRenderer.CreateMatrixStore();
                vegetationSystem.GenerateGrass(chunk.Data, playerPos, vegetationLoadDistance, grassScratch, matrices);
                chunk.SetGrassMatrices(matrices);

                if (showVegetationDensity && showDebugLogs)
                    Debug.Log($"[Vegetation] Chunk {kvp.Key} instances={chunk.Data.GrassInstanceCount}", gameObject);
            }
        }
    }

    private void DrawVegetation()
    {
        if (!enableGrass || vegetationRenderer == null)
            return;

        vegetationRenderer.BeginFrame();
        foreach (var kvp in activeChunks)
        {
            if (kvp.Value != null && kvp.Value.GrassMatrices != null)
                vegetationRenderer.DrawChunkMatrices(kvp.Value.GrassMatrices);
        }
    }

    #endregion

    #region LOD / Helpers

    private int GetLodLevel(Vector2Int chunkCoord)
    {
        if (playerTransform == null)
            return 0;

        float distSq = ChunkDistanceSq(GetChunkCoordinates(playerTransform.position), chunkCoord);
        float lod0Sq = lod0Distance * lod0Distance;
        float lod1Sq = lod1Distance * lod1Distance;
        if (distSq <= lod0Sq) return 0;
        if (distSq <= lod1Sq) return 1;
        return 2;
    }

    private int GetResolutionForLod(int lod)
    {
        int baseRes = Mathf.Clamp(renderResolution, 4, chunkSize);
        if (lod <= 0) return baseRes;
        if (lod == 1) return Mathf.Max(4, baseRes / 2);
        return Mathf.Max(4, baseRes / 4);
    }

    private Vector2Int GetChunkCoordinates(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / chunkSize),
            Mathf.FloorToInt(position.z / chunkSize));
    }

    private void EnsureSystems()
    {
        if (seed.ResolvedSeed == 0 && !useSeedString)
            seed.Configure(worldSeed, seedString, useSeedString);

        if (biomeSystem == null)
        {
            seed.Resolve();
            biomeSystem = new BiomeSystem(
                seed, biomes, worldRadius, capitalSpacing, capitalJitter,
                borderWarpScale, borderWarpStrength, spawnBiomeRadius, biomeBlendDistance);
        }
    }

    private void EnsureVertexCapacity(int count)
    {
        if (vertexBuffer == null || vertexBuffer.Length < count)
        {
            vertexBuffer = new Vector3[count];
            uvBuffer = new Vector2[count];
            uv2Buffer = new Vector2[count];
            colorBuffer = new Color[count];
            normalBuffer = new Vector3[count];
        }
    }

    private void EnsureTriangleCapacity(int count)
    {
        if (triangleBuffer == null || triangleBuffer.Length < count)
            triangleBuffer = new int[count];
    }

    private void EnsureDefaultMaterials()
    {
        if (grassMaterial == null)
            grassMaterial = CreateDefaultMaterial(new Color(0.2f, 0.8f, 0.2f), "Grass");
        if (dirtMaterial == null)
            dirtMaterial = CreateDefaultMaterial(new Color(0.6f, 0.4f, 0.2f), "Dirt");
        if (rockMaterial == null)
            rockMaterial = CreateDefaultMaterial(new Color(0.5f, 0.5f, 0.5f), "Rock");
        if (snowMaterial == null)
            snowMaterial = CreateDefaultMaterial(new Color(0.9f, 0.9f, 0.95f), "Snow");
    }

    private void EnsureDefaultBiomes()
    {
        if (biomes != null && biomes.Length > 0)
            return;

        biomes = BiomePresets.CreateDefaultSet();
        if (showDebugLogs)
            Debug.Log("[TerrainGenerator] No biomes configured — using BiomePresets.CreateDefaultSet().", gameObject);
    }

    private void EnsureDefaultGrassPrototypes()
    {
        if (!createDefaultGrassIfMissing || biomes == null)
            return;

        defaultGrassMesh = VegetationRenderer.CreateDefaultGrassBladeMesh();
        defaultGrassMaterial = VegetationRenderer.CreateDefaultGrassMaterial(new Color(0.3f, 0.7f, 0.2f));

        for (int i = 0; i < biomes.Length; i++)
        {
            Biome biome = biomes[i];
            if (biome == null)
                continue;

            if (biome.vegetation == null)
                biome.vegetation = new BiomeVegetationSettings();

            if (biome.terrain == null)
                biome.terrain = BiomeTerrainSettings.CreateDefault();

            if (biome.vegetation.grassTypes == null || biome.vegetation.grassTypes.Length == 0)
            {
                biome.vegetation.grassTypes = new[]
                {
                    new GrassType
                    {
                        mesh = defaultGrassMesh,
                        material = defaultGrassMaterial,
                        density = Mathf.Clamp01(biome.vegetation.density > 0f ? biome.vegetation.density : 0.7f),
                        minScale = biome.vegetation.minScale,
                        maxScale = biome.vegetation.maxScale,
                        randomYRotation = biome.vegetation.randomRotation,
                        maxSlope = biome.vegetation.slopeLimit
                    }
                };
            }
            else
            {
                for (int g = 0; g < biome.vegetation.grassTypes.Length; g++)
                {
                    GrassType gt = biome.vegetation.grassTypes[g];
                    if (gt == null)
                        continue;
                    if (gt.mesh == null)
                        gt.mesh = defaultGrassMesh;
                    if (gt.material == null)
                        gt.material = defaultGrassMaterial;
                    else
                        gt.material.enableInstancing = true;
                }
            }
        }
    }

    private Material CreateDefaultMaterial(Color color, string name)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader) { color = color, name = name };
        return mat;
    }

    private Material CreateFallbackTerrainMaterial()
    {
        // Prefer URP blend shader when available, then Built-in surface shader.
        Shader blend = Shader.Find("Custom/TerrainBiomeBlendURP");
        if (blend == null)
            blend = Shader.Find("Custom/TerrainBiomeBlend");

        if (blend != null)
        {
            Material mat = new Material(blend) { name = "TerrainBiomeBlend_Runtime" };
            ApplyBiomeTexturesToMaterial(mat);
            return mat;
        }

        // Vertex-color friendly fallback.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material fallback = new Material(shader) { name = "TerrainFallback" };
        return fallback;
    }

    private void ApplyBiomeTexturesToMaterial(Material mat)
    {
        if (biomes == null || mat == null)
            return;

        // Support up to 4 biome texture slots for the shared material.
        for (int i = 0; i < biomes.Length && i < 4; i++)
        {
            Biome b = biomes[i];
            if (b == null)
                continue;

            string texProp = $"_BiomeTex{i}";
            string colorProp = $"_BiomeColor{i}";
            if (b.groundTexture != null && mat.HasProperty(texProp))
                mat.SetTexture(texProp, b.groundTexture);
            if (mat.HasProperty(colorProp))
                mat.SetColor(colorProp, b.biomeColor);
        }

        if (mat.HasProperty("_BiomeCount"))
            mat.SetFloat("_BiomeCount", Mathf.Min(biomes.Length, 4));
    }

    private void RefreshDeterminismProbe()
    {
        if (!chunkDataMap.TryGetValue(determinismProbeChunk, out TerrainChunkData data) || data.Heights == null)
            return;

        float centerHeight = data.SampleHeightBilinear(chunkSize * 0.5f, chunkSize * 0.5f);
        BiomeSample centerBiome = SampleBiomeFromData(data, chunkSize * 0.5f, chunkSize * 0.5f);
        lastDeterminismReport =
            $"Seed: {seed.ResolvedSeed}\n" +
            $"Chunk: {determinismProbeChunk}\n" +
            $"Biome: {(centerBiome.primaryBiome != null ? centerBiome.primaryBiome.biomeName : "None")}\n" +
            $"Height sample: {centerHeight:F3}\n" +
            $"Grass count: {data.GrassInstanceCount}\n" +
            $"Object count: {data.ObjectInstanceCount}\n" +
            $"Fingerprint: {data.DeterminismFingerprint}";
    }

    private void OnGUI()
    {
        if (!enableDeterminismProbe || string.IsNullOrEmpty(lastDeterminismReport))
            return;

        GUI.color = Color.white;
        GUI.Box(new Rect(12, 12, 420, 140), "Determinism Probe");
        GUI.Label(new Rect(24, 36, 400, 110), lastDeterminismReport);
    }

    #endregion
}
