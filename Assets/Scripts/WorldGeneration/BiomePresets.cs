using UnityEngine;

/// <summary>
/// Convenience factory for Valheim-like starter biomes.
/// Call from a setup script or copy values into the TerrainGenerator inspector.
/// </summary>
public static class BiomePresets
{
    public static Biome CreateMeadows()
    {
        return new Biome
        {
            biomeName = "Meadows",
            biomeDescription = "Open grasslands near spawn",
            isSpawnBiome = true,
            minDistanceFromCenter = 0f,
            maxDistanceFromCenter = 0.35f,
            spawnChance = 0.9f,
            spreadChance = 0.85f,
            minElevation = 0f,
            maxElevation = 40f,
            biomeColor = new Color(0.35f, 0.75f, 0.25f),
            terrain = BiomeTerrainSettings.CreateMeadows(),
            vegetation = new BiomeVegetationSettings
            {
                density = 0.80f,
                minScale = 0.85f,
                maxScale = 1.2f,
                slopeLimit = 35f,
                nearDistance = 40f,
                farDistance = 120f
            }
        };
    }

    public static Biome CreateBlackForest()
    {
        return new Biome
        {
            biomeName = "Black Forest",
            biomeDescription = "Dense woodland with rolling hills",
            minDistanceFromCenter = 0.2f,
            maxDistanceFromCenter = 0.65f,
            spawnChance = 0.7f,
            spreadChance = 0.75f,
            minElevation = 1f,
            maxElevation = 55f,
            biomeColor = new Color(0.12f, 0.35f, 0.14f),
            terrain = BiomeTerrainSettings.CreateForest(),
            vegetation = new BiomeVegetationSettings
            {
                density = 0.65f,
                slopeLimit = 32f,
                nearDistance = 35f,
                farDistance = 110f
            }
        };
    }

    public static Biome CreateSwamp()
    {
        return new Biome
        {
            biomeName = "Swamp",
            biomeDescription = "Low, wet terrain",
            minDistanceFromCenter = 0.35f,
            maxDistanceFromCenter = 0.75f,
            spawnChance = 0.45f,
            spreadChance = 0.6f,
            minElevation = 0f,
            maxElevation = 12f,
            biomeColor = new Color(0.28f, 0.35f, 0.18f),
            terrain = BiomeTerrainSettings.CreateSwamp(),
            vegetation = new BiomeVegetationSettings
            {
                density = 0.50f,
                slopeLimit = 25f,
                nearDistance = 30f,
                farDistance = 90f
            }
        };
    }

    public static Biome CreateMountains()
    {
        return new Biome
        {
            biomeName = "Mountains",
            biomeDescription = "High elevation rocky peaks",
            minDistanceFromCenter = 0.45f,
            maxDistanceFromCenter = 1f,
            spawnChance = 0.55f,
            spreadChance = 0.7f,
            minElevation = 10f,
            maxElevation = 200f,
            biomeColor = new Color(0.55f, 0.55f, 0.58f),
            terrain = BiomeTerrainSettings.CreateMountains(),
            vegetation = new BiomeVegetationSettings
            {
                density = 0.10f,
                slopeLimit = 40f,
                nearDistance = 25f,
                farDistance = 80f
            }
        };
    }

    public static Biome CreatePlains()
    {
        return new Biome
        {
            biomeName = "Plains",
            biomeDescription = "Wide flat grasslands",
            minDistanceFromCenter = 0.15f,
            maxDistanceFromCenter = 0.55f,
            spawnChance = 0.5f,
            spreadChance = 0.65f,
            minElevation = 0f,
            maxElevation = 30f,
            biomeColor = new Color(0.55f, 0.75f, 0.30f),
            terrain = BiomeTerrainSettings.CreatePlains(),
            vegetation = new BiomeVegetationSettings
            {
                density = 0.95f,
                slopeLimit = 30f,
                nearDistance = 45f,
                farDistance = 130f
            }
        };
    }

    public static Biome[] CreateDefaultSet()
    {
        return new[]
        {
            CreateMeadows(),
            CreateBlackForest(),
            CreateSwamp(),
            CreateMountains(),
            CreatePlains()
        };
    }
}
