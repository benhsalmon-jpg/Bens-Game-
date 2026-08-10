using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic grass / decoration placement (data only).
/// Never Instantiates grass GameObjects — produces instance matrices for GPU rendering.
/// </summary>
public class VegetationSystem
{
    private readonly WorldSeed worldSeed;
    private readonly float grassCellSize;
    private readonly float maxSlopeDegrees;

    public VegetationSystem(WorldSeed worldSeed, float grassCellSize, float maxSlopeDegrees = 45f)
    {
        this.worldSeed = worldSeed;
        this.grassCellSize = Mathf.Max(0.5f, grassCellSize);
        this.maxSlopeDegrees = maxSlopeDegrees;
    }

    public float GrassCellSize => grassCellSize;

    /// <summary>
    /// Generate grass instances for a chunk. Density fades with distance from the player.
    /// Results are deterministic for (seed, chunk, cell).
    /// </summary>
    public void GenerateGrass(
        TerrainChunkData chunkData,
        Vector3 playerPosition,
        float vegetationFarDistance,
        List<VegetationInstance> output,
        Dictionary<int, List<Matrix4x4>> matricesByGrassKey)
    {
        output.Clear();
        matricesByGrassKey.Clear();

        if (chunkData == null || chunkData.BiomeSamples == null || chunkData.Heights == null)
            return;

        int chunkSize = chunkData.ChunkSize;
        Vector2Int coord = chunkData.Coordinate;
        float originX = coord.x * chunkSize;
        float originZ = coord.y * chunkSize;

        float cell = grassCellSize;
        int cells = Mathf.Max(1, Mathf.CeilToInt(chunkSize / cell));

        float farDistSq = vegetationFarDistance * vegetationFarDistance;

        for (int cz = 0; cz < cells; cz++)
        {
            for (int cx = 0; cx < cells; cx++)
            {
                int hash = worldSeed.GetPositionSeed(coord, cx, cz, WorldSeed.SystemGrass);
                float roll = WorldSeed.ToFloat01(hash);
                float jitterX = WorldSeed.ToFloat01(WorldSeed.Mix(hash, 11));
                float jitterZ = WorldSeed.ToFloat01(WorldSeed.Mix(hash, 29));

                float localX = (cx + jitterX) * cell;
                float localZ = (cz + jitterZ) * cell;
                if (localX < 0f || localZ < 0f || localX > chunkSize || localZ > chunkSize)
                    continue;

                float worldX = originX + localX;
                float worldZ = originZ + localZ;

                float dx = worldX - playerPosition.x;
                float dz = worldZ - playerPosition.z;
                float distSq = dx * dx + dz * dz;
                if (distSq > farDistSq)
                    continue;

                float height = chunkData.SampleHeightBilinear(localX, localZ);
                Vector3 normal = chunkData.SampleNormalBilinear(localX, localZ);
                float slope = Vector3.Angle(normal, Vector3.up);
                if (slope > maxSlopeDegrees)
                    continue;

                // Sample biome at nearest vertex for vegetation settings.
                BiomeSample biomeSample = SampleBiomeAtLocal(chunkData, localX, localZ);
                Biome primary = biomeSample.primaryBiome;
                Biome secondary = biomeSample.secondaryBiome;

                TryPlaceGrassForBiome(
                    primary,
                    biomeSample.primaryWeight,
                    hash,
                    roll,
                    worldX, height, worldZ,
                    normal,
                    slope,
                    distSq,
                    output,
                    matricesByGrassKey);

                if (secondary != null && biomeSample.secondaryWeight > 0.15f)
                {
                    TryPlaceGrassForBiome(
                        secondary,
                        biomeSample.secondaryWeight,
                        WorldSeed.Mix(hash, 77),
                        WorldSeed.ToFloat01(WorldSeed.Mix(hash, 77)),
                        worldX, height, worldZ,
                        normal,
                        slope,
                        distSq,
                        output,
                        matricesByGrassKey);
                }
            }
        }

        chunkData.GrassInstanceCount = output.Count;
    }

    private void TryPlaceGrassForBiome(
        Biome biome,
        float biomeWeight,
        int hash,
        float roll,
        float worldX,
        float height,
        float worldZ,
        Vector3 terrainNormal,
        float slope,
        float distSq,
        List<VegetationInstance> output,
        Dictionary<int, List<Matrix4x4>> matricesByGrassKey)
    {
        if (biome?.vegetation == null)
            return;

        BiomeVegetationSettings veg = biome.vegetation;
        if (veg.grassTypes == null || veg.grassTypes.Length == 0)
            return;

        if (height < veg.minElevation || height > veg.maxElevation)
            return;

        if (slope > veg.slopeLimit)
            return;

        float near = Mathf.Max(1f, veg.nearDistance);
        float far = Mathf.Max(near + 0.01f, veg.farDistance);
        float dist = Mathf.Sqrt(distSq);
        float distanceFade = 1f - Mathf.SmoothStep(near, far, dist);
        if (distanceFade <= 0.001f)
            return;

        float density = Mathf.Clamp01(veg.density) * Mathf.Clamp01(biomeWeight) * distanceFade;
        if (roll > density)
            return;

        // Pick grass type deterministically.
        int typeIndex = SelectGrassType(veg.grassTypes, hash);
        if (typeIndex < 0)
            return;

        GrassType grass = veg.grassTypes[typeIndex];
        if (grass == null || grass.mesh == null)
            return;

        if (WorldSeed.ToFloat01(WorldSeed.Mix(hash, 13)) > Mathf.Clamp01(grass.density))
            return;

        if (height < grass.minElevation || height > grass.maxElevation)
            return;

        if (slope > grass.maxSlope)
            return;

        float minScale = grass.minScale > 0f ? grass.minScale : veg.minScale;
        float maxScale = grass.maxScale > 0f ? grass.maxScale : veg.maxScale;
        float scale = WorldSeed.ToFloatRange(WorldSeed.Mix(hash, 41), minScale, maxScale);

        float yawMax = grass.randomYRotation > 0f ? grass.randomYRotation : veg.randomRotation;
        float yaw = WorldSeed.ToFloat01(WorldSeed.Mix(hash, 53)) * yawMax;

        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        // Align slightly to terrain without full slope-follow (keeps grass upright-ish).
        Vector3 alignNormal = Vector3.Normalize(Vector3.Lerp(Vector3.up, terrainNormal, 0.25f));
        rotation = Quaternion.FromToRotation(Vector3.up, alignNormal) * rotation;

        Vector3 position = new Vector3(worldX, height, worldZ);
        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one * scale);

        int biomeIndex = biome.runtimeIndex >= 0 ? biome.runtimeIndex : 0;
        int key = PackGrassKey(biomeIndex, typeIndex);

        VegetationInstance instance = new VegetationInstance
        {
            matrix = matrix,
            grassTypeIndex = typeIndex,
            biomeIndex = biomeIndex
        };
        output.Add(instance);

        if (!matricesByGrassKey.TryGetValue(key, out List<Matrix4x4> list))
        {
            list = new List<Matrix4x4>(64);
            matricesByGrassKey[key] = list;
        }

        list.Add(matrix);
    }

    private static int SelectGrassType(GrassType[] types, int hash)
    {
        float total = 0f;
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] != null && types[i].mesh != null)
                total += Mathf.Max(0.01f, types[i].density);
        }

        if (total <= 0f)
            return -1;

        float pick = WorldSeed.ToFloat01(WorldSeed.Mix(hash, 19)) * total;
        float acc = 0f;
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == null || types[i].mesh == null)
                continue;

            acc += Mathf.Max(0.01f, types[i].density);
            if (pick <= acc)
                return i;
        }

        return types.Length - 1;
    }

    private static BiomeSample SampleBiomeAtLocal(TerrainChunkData data, float localX, float localZ)
    {
        if (data.BiomeSamples == null || data.Resolution <= 0)
            return default;

        float u = localX / data.ChunkSize * data.Resolution;
        float v = localZ / data.ChunkSize * data.Resolution;
        int x = Mathf.Clamp(Mathf.RoundToInt(u), 0, data.Resolution);
        int z = Mathf.Clamp(Mathf.RoundToInt(v), 0, data.Resolution);
        return data.BiomeSamples[z * (data.Resolution + 1) + x];
    }

    public static int PackGrassKey(int biomeIndex, int grassTypeIndex)
    {
        return (biomeIndex << 16) | (grassTypeIndex & 0xFFFF);
    }

    public static void UnpackGrassKey(int key, out int biomeIndex, out int grassTypeIndex)
    {
        biomeIndex = key >> 16;
        grassTypeIndex = key & 0xFFFF;
    }
}
