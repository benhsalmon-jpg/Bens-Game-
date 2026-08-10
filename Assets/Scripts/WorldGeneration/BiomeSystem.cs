using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic biome-region system:
/// World Position → Domain Warp → Nearest capitals → Weights → Distance/elevation rules → Blend.
/// </summary>
public class BiomeSystem
{
    private struct BiomeCapital
    {
        public Vector2 worldPosition;
        public Biome biome;
        public float influence;
        public float claimRadius;
    }

    private readonly WorldSeed worldSeed;
    private readonly Biome[] biomes;
    private readonly float worldRadius;
    private readonly float capitalSpacing;
    private readonly float capitalJitter;
    private readonly float borderWarpScale;
    private readonly float borderWarpStrength;
    private readonly float spawnBiomeRadius;
    private readonly float biomeBlendDistance;

    private readonly List<BiomeCapital> capitals = new List<BiomeCapital>(256);
    private Biome spawnBiome;

    // Chunk-level coarse cache: corner samples reused by vertices.
    private readonly Dictionary<Vector2Int, BiomeSample[]> chunkCornerCache = new Dictionary<Vector2Int, BiomeSample[]>(256);

    public Biome SpawnBiome => spawnBiome;
    public int CapitalCount => capitals.Count;
    public float BiomeBlendDistance => biomeBlendDistance;

    public BiomeSystem(
        WorldSeed worldSeed,
        Biome[] biomes,
        float worldRadius,
        float capitalSpacing,
        float capitalJitter,
        float borderWarpScale,
        float borderWarpStrength,
        float spawnBiomeRadius,
        float biomeBlendDistance)
    {
        this.worldSeed = worldSeed;
        this.biomes = biomes;
        this.worldRadius = Mathf.Max(1f, worldRadius);
        this.capitalSpacing = Mathf.Max(40f, capitalSpacing);
        this.capitalJitter = Mathf.Clamp(capitalJitter, 0f, 0.5f);
        this.borderWarpScale = Mathf.Max(1f, borderWarpScale);
        this.borderWarpStrength = borderWarpStrength;
        this.spawnBiomeRadius = Mathf.Max(0f, spawnBiomeRadius);
        this.biomeBlendDistance = Mathf.Max(1f, biomeBlendDistance);

        AssignRuntimeIndices();
        spawnBiome = FindSpawnBiome();
        GenerateCapitals();
    }

    public void ClearCaches()
    {
        chunkCornerCache.Clear();
    }

    private void AssignRuntimeIndices()
    {
        if (biomes == null)
            return;

        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i] != null)
                biomes[i].runtimeIndex = i;
        }
    }

    private Biome FindSpawnBiome()
    {
        if (biomes == null || biomes.Length == 0)
            return null;

        for (int i = 0; i < biomes.Length; i++)
        {
            Biome b = biomes[i];
            if (b != null && b.isSpawnBiome)
                return b;
        }

        return biomes[0];
    }

    private void GenerateCapitals()
    {
        capitals.Clear();

        if (biomes == null || biomes.Length == 0)
            return;

        System.Random capitalRandom = worldSeed.CreateRandom(WorldSeed.SystemBiome);
        float spacing = capitalSpacing;
        int gridRadius = Mathf.CeilToInt(worldRadius / spacing) + 1;

        if (spawnBiome != null)
        {
            capitals.Add(new BiomeCapital
            {
                worldPosition = Vector2.zero,
                biome = spawnBiome,
                influence = Mathf.Clamp01(spawnBiome.spreadChance + 0.25f),
                claimRadius = Mathf.Max(spawnBiomeRadius, spacing * 0.9f)
            });
        }

        for (int gx = -gridRadius; gx <= gridRadius; gx++)
        {
            for (int gz = -gridRadius; gz <= gridRadius; gz++)
            {
                if (gx == 0 && gz == 0)
                    continue;

                float cellX = gx * spacing;
                float cellZ = gz * spacing;
                float jitterX = ((float)capitalRandom.NextDouble() * 2f - 1f) * spacing * capitalJitter;
                float jitterZ = ((float)capitalRandom.NextDouble() * 2f - 1f) * spacing * capitalJitter;

                Vector2 worldPos = new Vector2(cellX + jitterX, cellZ + jitterZ);
                float distanceFromCenter = worldPos.magnitude;
                if (distanceFromCenter > worldRadius)
                    continue;

                if (distanceFromCenter < spawnBiomeRadius * 0.75f)
                    continue;

                float normalizedDistance = Mathf.Clamp01(distanceFromCenter / worldRadius);
                Biome biome = SelectBiomeForDistance(normalizedDistance, capitalRandom);
                if (biome == null)
                    continue;

                float influence = Mathf.Clamp01(biome.spreadChance);
                float claimRadius = spacing * Mathf.Lerp(0.85f, 1.45f, influence);

                capitals.Add(new BiomeCapital
                {
                    worldPosition = worldPos,
                    biome = biome,
                    influence = influence,
                    claimRadius = claimRadius
                });
            }
        }
    }

    private Biome SelectBiomeForDistance(float normalizedDistance, System.Random random)
    {
        // Manual weighted pick — no LINQ in generation paths.
        Biome bestFallback = null;
        float bestFallbackScore = float.MaxValue;

        float totalWeight = 0f;
        int candidateCount = 0;

        // First pass: collect weights without allocating lists when possible.
        // Use a small fixed scratch via two parallel arrays sized to biomes.Length.
        int n = biomes.Length;
        Biome[] candidates = new Biome[n];
        float[] weights = new float[n];

        for (int i = 0; i < n; i++)
        {
            Biome biome = biomes[i];
            if (biome == null)
                continue;

            float mid = (biome.minDistanceFromCenter + biome.maxDistanceFromCenter) * 0.5f;
            float score = Mathf.Abs(mid - normalizedDistance);
            if (score < bestFallbackScore)
            {
                bestFallbackScore = score;
                bestFallback = biome;
            }

            if (normalizedDistance < biome.minDistanceFromCenter || normalizedDistance > biome.maxDistanceFromCenter)
                continue;

            float weight = Mathf.Max(0.01f, biome.spawnChance);
            if (biome.isSpawnBiome && normalizedDistance > 0.15f)
                weight *= 0.15f;

            candidates[candidateCount] = biome;
            weights[candidateCount] = weight;
            totalWeight += weight;
            candidateCount++;
        }

        if (candidateCount == 0)
            return bestFallback ?? biomes[0];

        float roll = (float)random.NextDouble() * totalWeight;
        float accumulated = 0f;
        for (int i = 0; i < candidateCount; i++)
        {
            accumulated += weights[i];
            if (roll <= accumulated)
                return candidates[i];
        }

        return candidates[candidateCount - 1];
    }

    /// <summary>
    /// Full biome sample at a world XZ position with organic borders + blend weights.
    /// </summary>
    public BiomeSample SampleBiome(float worldX, float worldZ)
    {
        if (biomes == null || biomes.Length == 0)
            return default;

        float distanceFromCenter = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);

        // Hard spawn protection near center (still allows soft outer blend via blend distance).
        if (spawnBiome != null && distanceFromCenter <= spawnBiomeRadius - biomeBlendDistance)
            return BiomeSample.FromSingle(spawnBiome);

        Vector2 warped = TerrainNoise.DomainWarp(
            worldSeed,
            WorldSeed.SystemBiome,
            worldX,
            worldZ,
            borderWarpScale,
            borderWarpStrength);

        // Find top-3 nearest capitals by soft score.
        BiomeCapital c0 = default, c1 = default, c2 = default;
        float s0 = float.MaxValue, s1 = float.MaxValue, s2 = float.MaxValue;
        bool has0 = false, has1 = false, has2 = false;

        for (int i = 0; i < capitals.Count; i++)
        {
            BiomeCapital capital = capitals[i];
            float dx = warped.x - capital.worldPosition.x;
            float dz = warped.y - capital.worldPosition.y;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            float softness = Mathf.Lerp(1.15f, 0.55f, capital.influence);
            float score = distance * softness / Mathf.Max(0.01f, capital.claimRadius);

            // Tiny deterministic bias — never uses a shared RNG stream.
            int biasHash = WorldSeed.Mix(
                worldSeed.GetSystemSeed(WorldSeed.SystemBiome),
                WorldSeed.Mix((int)(capital.worldPosition.x * 10f), (int)(capital.worldPosition.y * 10f)));
            score += WorldSeed.ToFloat01(biasHash) * 0.05f;

            if (score < s0)
            {
                s2 = s1; c2 = c1; has2 = has1;
                s1 = s0; c1 = c0; has1 = has0;
                s0 = score; c0 = capital; has0 = true;
            }
            else if (score < s1)
            {
                s2 = s1; c2 = c1; has2 = has1;
                s1 = score; c1 = capital; has1 = true;
            }
            else if (score < s2)
            {
                s2 = score; c2 = capital; has2 = true;
            }
        }

        if (!has0)
            return BiomeSample.FromSingle(spawnBiome ?? biomes[0]);

        BiomeSample sample = BuildBlendSample(c0, s0, has1 ? c1 : c0, s1, has2 ? c2 : c0, s2, has1, has2);

        // Soft spawn ring blend.
        if (spawnBiome != null && distanceFromCenter < spawnBiomeRadius + biomeBlendDistance)
        {
            float t = Mathf.InverseLerp(spawnBiomeRadius + biomeBlendDistance, spawnBiomeRadius - biomeBlendDistance, distanceFromCenter);
            t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (t > 0.001f)
                sample = LerpTowardBiome(sample, spawnBiome, t);
        }

        return sample;
    }

    private BiomeSample BuildBlendSample(
        BiomeCapital nearest, float nearestScore,
        BiomeCapital second, float secondScore,
        BiomeCapital third, float thirdScore,
        bool hasSecond, bool hasThird)
    {
        // Convert Voronoi scores into blend weights using configurable transition width.
        float edge = biomeBlendDistance / Mathf.Max(1f, capitalSpacing);

        float d0 = nearestScore;
        float d1 = hasSecond ? secondScore : d0 + 10f;
        float d2 = hasThird ? thirdScore : d1 + 10f;

        // Relative closeness in "score space".
        float gap01 = Mathf.Max(0.0001f, d1 - d0);
        float blend01 = 1f - Mathf.SmoothStep(0f, edge, gap01);

        float w0 = 1f;
        float w1 = 0f;
        float w2 = 0f;

        if (hasSecond && nearest.biome != second.biome)
        {
            w1 = blend01 * 0.5f;
            w0 = 1f - w1;
        }

        if (hasThird && third.biome != nearest.biome && third.biome != second.biome)
        {
            float gap02 = Mathf.Max(0.0001f, d2 - d0);
            float blend02 = 1f - Mathf.SmoothStep(0f, edge * 1.25f, gap02);
            w2 = blend02 * 0.25f;
            float rem = 1f - w2;
            w0 *= rem;
            w1 *= rem;
        }

        BiomeSample sample = new BiomeSample
        {
            primaryBiome = nearest.biome,
            secondaryBiome = hasSecond ? second.biome : null,
            tertiaryBiome = hasThird ? third.biome : null,
            primaryWeight = w0,
            secondaryWeight = w1,
            tertiaryWeight = w2
        };
        sample.Normalize();
        return sample;
    }

    private static BiomeSample LerpTowardBiome(BiomeSample sample, Biome target, float t)
    {
        if (sample.primaryBiome == target)
        {
            sample.primaryWeight = Mathf.Lerp(sample.primaryWeight, 1f, t);
            sample.secondaryWeight *= 1f - t;
            sample.tertiaryWeight *= 1f - t;
            sample.Normalize();
            return sample;
        }

        // Push target into primary, demote previous primary to secondary.
        sample.tertiaryBiome = sample.secondaryBiome;
        sample.tertiaryWeight = sample.secondaryWeight * (1f - t);
        sample.secondaryBiome = sample.primaryBiome;
        sample.secondaryWeight = sample.primaryWeight * (1f - t);
        sample.primaryBiome = target;
        sample.primaryWeight = Mathf.Lerp(0f, 1f, t);
        sample.Normalize();
        return sample;
    }

    /// <summary>
    /// Apply elevation constraints: if primary is invalid at this elevation, reweight toward valid biomes.
    /// Uses a cheap preliminary elevation estimate for gating only.
    /// </summary>
    public BiomeSample ApplyElevationGate(BiomeSample sample, float elevationEstimate, float normalizedDistance)
    {
        if (sample.primaryBiome != null &&
            elevationEstimate >= sample.primaryBiome.minElevation &&
            elevationEstimate <= sample.primaryBiome.maxElevation)
        {
            return sample;
        }

        Biome replacement = FindBiomeForElevation(elevationEstimate, normalizedDistance);
        if (replacement == null)
            return sample;

        return LerpTowardBiome(sample, replacement, 0.85f);
    }

    private Biome FindBiomeForElevation(float elevation, float normalizedDistance)
    {
        Biome best = null;
        float bestScore = float.MaxValue;

        if (biomes == null)
            return null;

        for (int i = 0; i < biomes.Length; i++)
        {
            Biome biome = biomes[i];
            if (biome == null)
                continue;

            if (elevation < biome.minElevation || elevation > biome.maxElevation)
                continue;

            float mid = (biome.minDistanceFromCenter + biome.maxDistanceFromCenter) * 0.5f;
            float score = Mathf.Abs(mid - normalizedDistance) - biome.spawnChance;
            if (score < bestScore)
            {
                bestScore = score;
                best = biome;
            }
        }

        return best;
    }

    /// <summary>
    /// Cache 2x2 corner biome samples per chunk for cheap bilinear reuse by vertices.
    /// </summary>
    public BiomeSample[] GetOrCreateChunkCornerSamples(Vector2Int chunkCoord, int chunkSize)
    {
        if (chunkCornerCache.TryGetValue(chunkCoord, out BiomeSample[] cached))
            return cached;

        float x0 = chunkCoord.x * chunkSize;
        float z0 = chunkCoord.y * chunkSize;
        float x1 = x0 + chunkSize;
        float z1 = z0 + chunkSize;

        BiomeSample[] corners = new BiomeSample[4];
        corners[0] = SampleBiome(x0, z0);
        corners[1] = SampleBiome(x1, z0);
        corners[2] = SampleBiome(x0, z1);
        corners[3] = SampleBiome(x1, z1);
        chunkCornerCache[chunkCoord] = corners;
        return corners;
    }

    /// <summary>
    /// Accurate sample (full query). Prefer for sparse sampling / objects.
    /// For dense mesh vertices, use SampleBiomeBilinear after caching corners,
    /// then refine edges with full SampleBiome when blend is high.
    /// </summary>
    public BiomeSample SampleBiomeBilinear(BiomeSample[] corners, float u, float v)
    {
        // corners: 0=(0,0), 1=(1,0), 2=(0,1), 3=(1,1)
        BiomeSample a = LerpSamples(corners[0], corners[1], u);
        BiomeSample b = LerpSamples(corners[2], corners[3], u);
        return LerpSamples(a, b, v);
    }

    private static BiomeSample LerpSamples(BiomeSample a, BiomeSample b, float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0f) return a;
        if (t >= 1f) return b;

        // If same primary, just lerp weights; else treat as blend between the two primaries.
        if (a.primaryBiome == b.primaryBiome)
        {
            BiomeSample s = a;
            s.primaryWeight = Mathf.Lerp(a.primaryWeight, b.primaryWeight, t);
            s.secondaryWeight = Mathf.Lerp(a.secondaryWeight, b.secondaryWeight, t);
            s.tertiaryWeight = Mathf.Lerp(a.tertiaryWeight, b.tertiaryWeight, t);
            if (s.secondaryBiome == null) s.secondaryBiome = b.secondaryBiome;
            s.Normalize();
            return s;
        }

        BiomeSample blended = new BiomeSample
        {
            primaryBiome = t < 0.5f ? a.primaryBiome : b.primaryBiome,
            secondaryBiome = t < 0.5f ? b.primaryBiome : a.primaryBiome,
            tertiaryBiome = a.secondaryBiome ?? b.secondaryBiome,
            primaryWeight = t < 0.5f ? 1f - t : t,
            secondaryWeight = t < 0.5f ? t : 1f - t,
            tertiaryWeight = 0f
        };
        blended.Normalize();
        return blended;
    }

    public IReadOnlyList<Vector2> GetCapitalPositionsForDebug()
    {
        List<Vector2> list = new List<Vector2>(capitals.Count);
        for (int i = 0; i < capitals.Count; i++)
            list.Add(capitals[i].worldPosition);
        return list;
    }

    public Color GetCapitalBiomeColor(int index)
    {
        if (index < 0 || index >= capitals.Count || capitals[index].biome == null)
            return Color.magenta;
        return capitals[index].biome.biomeColor;
    }
}
