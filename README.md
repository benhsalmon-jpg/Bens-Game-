# Bens-Game-

Procedural survival-game terrain system for Unity.

## Drop-in script

**Full finished script:** [`Assets/Scripts/TerrainGenerator.cs`](Assets/Scripts/TerrainGenerator.cs)

Copy that one file into your Unity project (or open this repo in Unity 2022.3+).

Optional shaders for biome material blending:

- `Assets/Shaders/TerrainBiomeBlend.shader` (Built-in RP)
- `Assets/Shaders/TerrainBiomeBlendURP.shader` (URP)

Docs: [`Assets/Documentation/TERRAIN_SYSTEM.md`](Assets/Documentation/TERRAIN_SYSTEM.md)

### Quick start

1. Add an empty GameObject → attach `TerrainGenerator`
2. Assign your Player (or tag a GameObject `Player`)
3. Leave **Biomes** empty to auto-load Meadows / Black Forest / Swamp / Mountains / Plains
4. Enter Play Mode

### What is included in the single script

- Deterministic `WorldSeed` streams (Terrain / Biome / Objects / Grass / …)
- Warped Voronoi biomes with blend weights
- Per-biome elevation profiles (height blends across borders)
- Staged coroutine chunk streaming + LOD
- GPU-instanced grass (no grass GameObjects)
- Determinism probe / debugger component
