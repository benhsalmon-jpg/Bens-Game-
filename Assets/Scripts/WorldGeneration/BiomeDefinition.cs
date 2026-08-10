using System;
using UnityEngine;

/// <summary>
/// Per-biome elevation / landform profile. Terrain blends these by biome weights.
/// </summary>
[Serializable]
public class BiomeTerrainSettings
{
    public float baseHeight = 2f;
    public float heightMultiplier = 12f;

    public float continentalScale = 420f;
    public float detailScale = 55f;
    [Range(0f, 2f)]
    public float detailStrength = 0.35f;

    public AnimationCurve elevationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Range(0f, 40f)]
    public float mountainStrength = 4f;
    public float mountainScale = 180f;

    [Range(0f, 1.5f)]
    public float roughness = 0.25f;

    public static BiomeTerrainSettings CreateDefault()
    {
        return new BiomeTerrainSettings();
    }

    public static BiomeTerrainSettings CreateMeadows()
    {
        return new BiomeTerrainSettings
        {
            baseHeight = 1.5f,
            heightMultiplier = 8f,
            continentalScale = 480f,
            detailScale = 70f,
            detailStrength = 0.22f,
            mountainStrength = 1.5f,
            mountainScale = 220f,
            roughness = 0.12f,
            elevationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.85f)
        };
    }

    public static BiomeTerrainSettings CreateForest()
    {
        return new BiomeTerrainSettings
        {
            baseHeight = 3f,
            heightMultiplier = 14f,
            continentalScale = 400f,
            detailScale = 48f,
            detailStrength = 0.45f,
            mountainStrength = 5f,
            mountainScale = 170f,
            roughness = 0.35f,
            elevationCurve = AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f)
        };
    }

    public static BiomeTerrainSettings CreateMountains()
    {
        return new BiomeTerrainSettings
        {
            baseHeight = 8f,
            heightMultiplier = 28f,
            continentalScale = 360f,
            detailScale = 40f,
            detailStrength = 0.55f,
            mountainStrength = 22f,
            mountainScale = 140f,
            roughness = 0.7f,
            elevationCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.45f, 0.25f),
                new Keyframe(1f, 1f))
        };
    }

    public static BiomeTerrainSettings CreateSwamp()
    {
        return new BiomeTerrainSettings
        {
            baseHeight = 0.4f,
            heightMultiplier = 3.5f,
            continentalScale = 520f,
            detailScale = 90f,
            detailStrength = 0.12f,
            mountainStrength = 0.4f,
            mountainScale = 260f,
            roughness = 0.08f,
            elevationCurve = AnimationCurve.Linear(0f, 0.15f, 1f, 0.45f)
        };
    }

    public static BiomeTerrainSettings CreatePlains()
    {
        return new BiomeTerrainSettings
        {
            baseHeight = 2f,
            heightMultiplier = 6f,
            continentalScale = 600f,
            detailScale = 110f,
            detailStrength = 0.15f,
            mountainStrength = 1.2f,
            mountainScale = 280f,
            roughness = 0.1f,
            elevationCurve = AnimationCurve.EaseInOut(0f, 0.1f, 1f, 0.65f)
        };
    }
}

/// <summary>
/// One grass / decorative vegetation prototype rendered via GPU instancing.
/// </summary>
[Serializable]
public class GrassType
{
    [Header("Prototype")]
    public Mesh mesh;
    public Material material;

    [Range(0f, 1f)]
    [Tooltip("Relative density contribution for this grass type.")]
    public float density = 0.8f;

    [Header("Scale")]
    public float minScale = 0.8f;
    public float maxScale = 1.25f;

    [Header("Rotation")]
    [Range(0f, 360f)]
    public float randomYRotation = 360f;

    [Header("Placement Filters")]
    public float minElevation = -999f;
    public float maxElevation = 999f;
    [Tooltip("Maximum terrain slope in degrees.")]
    public float maxSlope = 35f;
}

/// <summary>
/// Biome-level vegetation / grass settings (GPU instanced, not GameObjects).
/// </summary>
[Serializable]
public class BiomeVegetationSettings
{
    public GrassType[] grassTypes;

    [Range(0f, 1f)]
    [Tooltip("Overall grass density for this biome (0-1).")]
    public float density = 0.7f;

    public float minScale = 0.85f;
    public float maxScale = 1.2f;

    [Range(0f, 360f)]
    public float randomRotation = 360f;

    public float minElevation = -999f;
    public float maxElevation = 999f;

    [Tooltip("Maximum slope in degrees for any vegetation in this biome.")]
    public float slopeLimit = 35f;

    [Tooltip("Full density inside this distance from the player.")]
    public float nearDistance = 40f;

    [Tooltip("Vegetation fades toward zero approaching this distance.")]
    public float farDistance = 120f;
}

/// <summary>
/// Gameplay / important object (trees, resources) — may be a real GameObject.
/// </summary>
[Serializable]
public class BiomeObject
{
    public enum ObjectCategory
    {
        Important,
        Decoration
    }

    [Header("Object")]
    public GameObject prefab;

    [Tooltip("Important = GameObject (interactable). Decorative = prefer instancing when possible.")]
    public ObjectCategory category = ObjectCategory.Important;

    [Range(0f, 1f)]
    public float spawnChance = 0.1f;

    [Tooltip("Preferred spacing from other candidate spawn cells.")]
    public float minimumDistance = 1f;

    [Header("Placement Filters")]
    public float minElevation = -999f;
    public float maxElevation = 999f;
    public float maxSlope = 25f;

    [Header("Position Adjustment")]
    public float yOffset;

    [Header("X-Axis Rotation")]
    public float minXRotation;
    public float maxXRotation;

    [Header("Y-Axis Rotation")]
    public float minYRotation;
    public float maxYRotation = 360f;
}

/// <summary>
/// Weighted biome sample at a world position. Borders blend via weights.
/// </summary>
[Serializable]
public struct BiomeSample
{
    public Biome primaryBiome;
    public Biome secondaryBiome;
    public Biome tertiaryBiome;

    public float primaryWeight;
    public float secondaryWeight;
    public float tertiaryWeight;

    public float PrimaryWeight01 => Mathf.Clamp01(primaryWeight);
    public float SecondaryWeight01 => Mathf.Clamp01(secondaryWeight);

    public static BiomeSample FromSingle(Biome biome)
    {
        return new BiomeSample
        {
            primaryBiome = biome,
            secondaryBiome = null,
            tertiaryBiome = null,
            primaryWeight = 1f,
            secondaryWeight = 0f,
            tertiaryWeight = 0f
        };
    }

    public void Normalize()
    {
        float sum = primaryWeight + secondaryWeight + tertiaryWeight;
        if (sum <= 0.0001f)
        {
            primaryWeight = 1f;
            secondaryWeight = 0f;
            tertiaryWeight = 0f;
            return;
        }

        primaryWeight /= sum;
        secondaryWeight /= sum;
        tertiaryWeight /= sum;
    }
}

/// <summary>
/// Designer-facing biome definition (Inspector). Migrated from the original Biome class.
/// </summary>
[Serializable]
public class Biome
{
    [Header("Biome Info")]
    public string biomeName = "Meadows";
    [TextArea(1, 2)]
    public string biomeDescription = "Open grasslands near spawn";

    [Header("World Ring (Valheim-style)")]
    [Tooltip("Mark one biome as the protected spawn biome near world center.")]
    public bool isSpawnBiome;

    [Range(0f, 1f)]
    [Tooltip("Normalized min distance from world center (0 = center, 1 = world edge).")]
    public float minDistanceFromCenter;

    [Range(0f, 1f)]
    [Tooltip("Normalized max distance from world center (0 = center, 1 = world edge).")]
    public float maxDistanceFromCenter = 1f;

    [Header("Biome Spawning")]
    [Range(0f, 1f)]
    [Tooltip("Relative weight when choosing among valid biomes for a capital.")]
    public float spawnChance = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("How aggressively this capital claims neighboring territory.")]
    public float spreadChance = 0.7f;

    [Header("Elevation Range")]
    public float minElevation;
    public float maxElevation = 100f;

    [Header("Terrain Profile")]
    public BiomeTerrainSettings terrain = BiomeTerrainSettings.CreateMeadows();

    [Header("Biome Material")]
    [Tooltip("Albedo/tint used by the shared biome-blend terrain material.")]
    public Material biomeMaterial;
    public Color biomeColor = Color.green;
    public Texture2D groundTexture;
    public Vector2 textureTiling = new Vector2(2f, 2f);

    [Header("Vegetation (GPU Instanced Grass)")]
    public BiomeVegetationSettings vegetation = new BiomeVegetationSettings();

    [Header("Objects (GameObjects)")]
    public BiomeObject[] objects;

    [Header("Visual")]
    [Tooltip("Legacy visual color (also used for debug biomes / vertex tint).")]
    public Color debugColor = Color.green;

    /// <summary>Runtime index assigned by BiomeSystem (stable for a session).</summary>
    [NonSerialized] public int runtimeIndex = -1;
}
