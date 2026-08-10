using System;
using UnityEngine;

/// <summary>
/// Master world seed and independent deterministic streams.
/// Changing grass/objects/etc. must never alter unrelated system streams.
/// </summary>
[Serializable]
public class WorldSeed
{
    public const string SystemWorld = "World";
    public const string SystemTerrain = "Terrain";
    public const string SystemBiome = "Biome";
    public const string SystemObjects = "Objects";
    public const string SystemGrass = "Grass";
    public const string SystemRocks = "Rocks";
    public const string SystemTrees = "Trees";
    public const string SystemDecorations = "Decorations";

    [SerializeField] private int worldSeed = 12345;
    [SerializeField] private string seedString = "";
    [SerializeField] private bool useSeedString;

    private int resolvedSeed;
    private bool initialized;

    public int WorldSeedValue => worldSeed;
    public string SeedString => seedString;
    public bool UseSeedString => useSeedString;
    public int ResolvedSeed => initialized ? resolvedSeed : Resolve();

    public void Configure(int seed, string text, bool useText)
    {
        worldSeed = seed;
        seedString = text ?? string.Empty;
        useSeedString = useText;
        initialized = false;
    }

    public int Resolve()
    {
        resolvedSeed = useSeedString && !string.IsNullOrWhiteSpace(seedString)
            ? HashSeedString(seedString)
            : worldSeed;

        if (resolvedSeed == 0)
            resolvedSeed = 1;

        initialized = true;
        return resolvedSeed;
    }

    public int GetSystemSeed(string systemName)
    {
        EnsureResolved();
        return Mix(resolvedSeed, HashSeedString(systemName ?? string.Empty));
    }

    public int GetChunkSeed(Vector2Int chunkCoord, string systemName)
    {
        int seed = GetSystemSeed(systemName);
        seed = Mix(seed, chunkCoord.x);
        seed = Mix(seed, chunkCoord.y);
        return seed;
    }

    public int GetPositionSeed(Vector2Int chunkCoord, int x, int z, string systemName)
    {
        int seed = GetChunkSeed(chunkCoord, systemName);
        seed = Mix(seed, x);
        seed = Mix(seed, z);
        return seed;
    }

    public int GetWorldPositionSeed(int worldX, int worldZ, string systemName)
    {
        int seed = GetSystemSeed(systemName);
        seed = Mix(seed, worldX);
        seed = Mix(seed, worldZ);
        return seed;
    }

    /// <summary>
    /// Stable non-cryptographic string hash (FNV-1a style).
    /// Same string always maps to the same int across sessions.
    /// </summary>
    public static int HashSeedString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 1;

        unchecked
        {
            int hash = 216613626;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }

            return hash == 0 ? 1 : hash;
        }
    }

    public static int Mix(int seed, int value)
    {
        unchecked
        {
            int h = seed;
            h ^= value * -862048943;
            h = (h << 13) | (h >> 19);
            h *= 5 + -2048144789;
            h ^= h >> 16;
            return h == 0 ? 1 : h;
        }
    }

    /// <summary>
    /// Deterministic float in [0,1) from an integer seed/hash.
    /// </summary>
    public static float ToFloat01(int hash)
    {
        unchecked
        {
            uint u = (uint)hash;
            return (u & 0x00FFFFFF) / 16777216f;
        }
    }

    public static float ToFloatRange(int hash, float min, float max)
    {
        return min + ToFloat01(hash) * (max - min);
    }

    public System.Random CreateRandom(string systemName)
    {
        return new System.Random(GetSystemSeed(systemName));
    }

    public System.Random CreateChunkRandom(Vector2Int chunkCoord, string systemName)
    {
        return new System.Random(GetChunkSeed(chunkCoord, systemName));
    }

    private void EnsureResolved()
    {
        if (!initialized)
            Resolve();
    }
}
