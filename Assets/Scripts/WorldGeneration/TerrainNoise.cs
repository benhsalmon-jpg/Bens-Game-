using System;
using UnityEngine;

/// <summary>
/// Reusable deterministic multi-layer noise for terrain generation.
/// All samples are driven by WorldSeed system streams (never UnityEngine.Random).
/// </summary>
[Serializable]
public class TerrainNoiseSettings
{
    [Header("Continental / Large Scale")]
    [Tooltip("Low-frequency landmass shape.")]
    public float continentalScale = 420f;
    [Range(0f, 2f)]
    public float continentalStrength = 0.55f;

    [Header("Mountain / Landform")]
    public float mountainScale = 180f;
    [Range(0f, 2f)]
    public float mountainStrength = 0.35f;

    [Header("Detail")]
    public float detailScale = 55f;
    [Range(0f, 1.5f)]
    public float detailStrength = 0.25f;

    [Header("Micro Detail")]
    public float microScale = 18f;
    [Range(0f, 1f)]
    public float microStrength = 0.08f;

    [Header("Domain Warp")]
    public float warpScale = 90f;
    public float warpStrength = 28f;
}

/// <summary>
/// Deterministic seeded Perlin utilities + layered terrain helpers.
/// </summary>
public static class TerrainNoise
{
    public static float SamplePerlin(WorldSeed worldSeed, string systemName, float x, float z, float scale, float channelOffset)
    {
        float safeScale = Mathf.Max(0.0001f, scale);
        int systemSeed = worldSeed != null ? worldSeed.GetSystemSeed(systemName) : 1;
        float seedOffsetX = (systemSeed % 100000) * 0.137f + channelOffset * 17.13f;
        float seedOffsetZ = (systemSeed % 100000) * 0.311f + channelOffset * 9.71f;
        return Mathf.PerlinNoise((x + seedOffsetX) / safeScale, (z + seedOffsetZ) / safeScale);
    }

    public static Vector2 DomainWarp(WorldSeed worldSeed, string systemName, float x, float z, float warpScale, float warpStrength)
    {
        float wx = (SamplePerlin(worldSeed, systemName, x, z, warpScale, 11.3f) - 0.5f) * 2f * warpStrength;
        float wz = (SamplePerlin(worldSeed, systemName, x, z, warpScale, 47.8f) - 0.5f) * 2f * warpStrength;
        return new Vector2(x + wx, z + wz);
    }

    /// <summary>
    /// Shared continental / mountain / detail stack used before biome-specific shaping.
    /// </summary>
    public static float SampleLayeredBase(
        WorldSeed worldSeed,
        TerrainNoiseSettings settings,
        float worldX,
        float worldZ)
    {
        if (settings == null)
            settings = new TerrainNoiseSettings();

        float continental = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, settings.continentalScale, 1.1f);
        float mountain = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, settings.mountainScale, 2.7f);
        float detail = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, settings.detailScale, 4.2f);
        float micro = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, settings.microScale, 6.9f);

        // Ridged-ish mountain contribution.
        float mountainRidged = 1f - Mathf.Abs(mountain * 2f - 1f);
        mountainRidged *= mountainRidged;

        float value =
            continental * settings.continentalStrength +
            mountainRidged * settings.mountainStrength +
            detail * settings.detailStrength +
            micro * settings.microStrength;

        float norm = settings.continentalStrength + settings.mountainStrength + settings.detailStrength + settings.microStrength;
        if (norm > 0.0001f)
            value /= norm;

        return Mathf.Clamp01(value);
    }

    public static float EvaluateBiomeTerrain(
        WorldSeed worldSeed,
        BiomeTerrainSettings terrain,
        TerrainNoiseSettings globalNoise,
        float worldX,
        float worldZ)
    {
        if (terrain == null)
            terrain = BiomeTerrainSettings.CreateDefault();

        float continentalScale = terrain.continentalScale > 0.01f ? terrain.continentalScale : globalNoise.continentalScale;
        float detailScale = terrain.detailScale > 0.01f ? terrain.detailScale : globalNoise.detailScale;
        float mountainScale = terrain.mountainScale > 0.01f ? terrain.mountainScale : globalNoise.mountainScale;

        float continental = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, continentalScale, 10.1f);
        float detail = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, detailScale, 20.2f);
        float mountain = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, mountainScale, 30.3f);
        float mountainRidged = 1f - Mathf.Abs(mountain * 2f - 1f);
        mountainRidged *= mountainRidged;

        float roughness = SamplePerlin(worldSeed, WorldSeed.SystemTerrain, worldX, worldZ, detailScale * 0.45f, 40.4f);

        float shaped = continental * 0.55f + detail * terrain.detailStrength + roughness * terrain.roughness * 0.35f;
        shaped = Mathf.Clamp01(shaped);
        shaped = terrain.elevationCurve != null ? terrain.elevationCurve.Evaluate(shaped) : shaped;

        float height =
            terrain.baseHeight +
            shaped * terrain.heightMultiplier +
            mountainRidged * terrain.mountainStrength;

        return height;
    }
}
