using System;
using UnityEngine;

/// <summary>
/// Pure deterministic chunk data — independent of scene GameObjects.
/// A chunk can exist as data before (or without) a visual representation.
/// </summary>
public class TerrainChunkData
{
    public enum ChunkState
    {
        Unloaded = 0,
        Loaded = 1,
        Generating = 2,
        Generated = 3,
        Visible = 4,
        Hidden = 5,
        Unloading = 6
    }

    public Vector2Int Coordinate { get; }
    public int ChunkSize { get; }
    public int Resolution { get; private set; }
    public int LodLevel { get; private set; }

    public ChunkState State { get; set; } = ChunkState.Unloaded;

    /// <summary>Generation token. Stale async/coroutine work must discard results if token mismatches.</summary>
    public int GenerationToken { get; private set; }

    public float[] Heights { get; private set; }
    public BiomeSample[] BiomeSamples { get; private set; }
    public Vector3[] Normals { get; private set; }

    /// <summary>Vertex colors packed for shader biome blend. See TerrainGenerator docs.</summary>
    public Color[] VertexColors { get; private set; }

    public int GrassInstanceCount { get; set; }
    public int ObjectInstanceCount { get; set; }

    /// <summary>Fingerprint for determinism debug (seed + coord + height/grass/object counts).</summary>
    public string DeterminismFingerprint { get; set; }

    public TerrainChunkData(Vector2Int coordinate, int chunkSize)
    {
        Coordinate = coordinate;
        ChunkSize = chunkSize;
        State = ChunkState.Loaded;
    }

    public void BeginGeneration(int lodLevel, int resolution, int token)
    {
        LodLevel = lodLevel;
        Resolution = resolution;
        GenerationToken = token;
        State = ChunkState.Generating;

        int vertCount = (resolution + 1) * (resolution + 1);
        Heights = new float[vertCount];
        BiomeSamples = new BiomeSample[vertCount];
        Normals = new Vector3[vertCount];
        VertexColors = new Color[vertCount];
        GrassInstanceCount = 0;
        ObjectInstanceCount = 0;
        DeterminismFingerprint = null;
    }

    /// <summary>Invalidate in-flight generation without allocating new buffers.</summary>
    public void Invalidate(int newToken)
    {
        GenerationToken = newToken;
        State = ChunkState.Unloading;
    }

    public bool IsTokenValid(int token) => GenerationToken == token;

    public void MarkGenerated()
    {
        State = ChunkState.Generated;
    }

    public void MarkVisible()
    {
        State = ChunkState.Visible;
    }

    public void MarkHidden()
    {
        State = ChunkState.Hidden;
    }

    public void MarkUnloading()
    {
        State = ChunkState.Unloading;
    }

    public float GetHeight(int localX, int localZ)
    {
        if (Heights == null)
            return 0f;

        int x = Mathf.Clamp(localX, 0, Resolution);
        int z = Mathf.Clamp(localZ, 0, Resolution);
        return Heights[z * (Resolution + 1) + x];
    }

    public BiomeSample GetBiomeSample(int localX, int localZ)
    {
        if (BiomeSamples == null)
            return default;

        int x = Mathf.Clamp(localX, 0, Resolution);
        int z = Mathf.Clamp(localZ, 0, Resolution);
        return BiomeSamples[z * (Resolution + 1) + x];
    }

    public float SampleHeightBilinear(float localX, float localZ)
    {
        if (Heights == null || Resolution <= 0)
            return 0f;

        float u = Mathf.Clamp(localX / ChunkSize * Resolution, 0f, Resolution);
        float v = Mathf.Clamp(localZ / ChunkSize * Resolution, 0f, Resolution);

        int x0 = Mathf.FloorToInt(u);
        int z0 = Mathf.FloorToInt(v);
        int x1 = Mathf.Min(x0 + 1, Resolution);
        int z1 = Mathf.Min(z0 + 1, Resolution);
        float tx = u - x0;
        float tz = v - z0;

        float h00 = Heights[z0 * (Resolution + 1) + x0];
        float h10 = Heights[z0 * (Resolution + 1) + x1];
        float h01 = Heights[z1 * (Resolution + 1) + x0];
        float h11 = Heights[z1 * (Resolution + 1) + x1];

        float h0 = Mathf.Lerp(h00, h10, tx);
        float h1 = Mathf.Lerp(h01, h11, tx);
        return Mathf.Lerp(h0, h1, tz);
    }

    public Vector3 SampleNormalBilinear(float localX, float localZ)
    {
        if (Normals == null || Resolution <= 0)
            return Vector3.up;

        float u = Mathf.Clamp(localX / ChunkSize * Resolution, 0f, Resolution);
        float v = Mathf.Clamp(localZ / ChunkSize * Resolution, 0f, Resolution);

        int x0 = Mathf.FloorToInt(u);
        int z0 = Mathf.FloorToInt(v);
        int x1 = Mathf.Min(x0 + 1, Resolution);
        int z1 = Mathf.Min(z0 + 1, Resolution);
        float tx = u - x0;
        float tz = v - z0;

        Vector3 n00 = Normals[z0 * (Resolution + 1) + x0];
        Vector3 n10 = Normals[z0 * (Resolution + 1) + x1];
        Vector3 n01 = Normals[z1 * (Resolution + 1) + x0];
        Vector3 n11 = Normals[z1 * (Resolution + 1) + x1];

        Vector3 n0 = Vector3.Lerp(n00, n10, tx);
        Vector3 n1 = Vector3.Lerp(n01, n11, tx);
        return Vector3.Normalize(Vector3.Lerp(n0, n1, tz));
    }

    public void BuildFingerprint(int resolvedSeed)
    {
        float heightSum = 0f;
        if (Heights != null)
        {
            for (int i = 0; i < Heights.Length; i++)
                heightSum += Heights[i];
        }

        string primary = BiomeSamples != null && BiomeSamples.Length > 0 && BiomeSamples[0].primaryBiome != null
            ? BiomeSamples[0].primaryBiome.biomeName
            : "None";

        DeterminismFingerprint =
            $"Seed:{resolvedSeed} Chunk:{Coordinate} Biome:{primary} HeightSum:{heightSum:F3} Grass:{GrassInstanceCount} Objects:{ObjectInstanceCount}";
    }
}

/// <summary>
/// Lightweight GPU vegetation instance (no GameObject).
/// </summary>
public struct VegetationInstance
{
    public Matrix4x4 matrix;
    public int grassTypeIndex;
    public int biomeIndex;
}

/// <summary>
/// Important object spawn record (deterministic, GameObject created later).
/// </summary>
public struct ObjectSpawnRecord
{
    public int biomeObjectIndex;
    public Vector3 position;
    public Quaternion rotation;
    public float yOffset;
}
