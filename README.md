# Bens-Game-

Procedural survival-game terrain system for Unity.

## World generation

See **[Assets/Documentation/TERRAIN_SYSTEM.md](Assets/Documentation/TERRAIN_SYSTEM.md)** for architecture, inspector setup, shader channel layout, and determinism testing.

### Quick start

1. Open / import `Assets/` in Unity **2022.3 LTS** (Built-in or URP).
2. Add `TerrainGenerator` to a scene object.
3. Assign a Player transform (or tag a GameObject `Player`).
4. Populate the **Biomes** array (or use `BiomePresets.CreateDefaultSet()` from a setup script).
5. Create a material with `Custom/TerrainBiomeBlend` (Built-in) or `Custom/TerrainBiomeBlendURP` (URP) and assign it to **Terrain Blend Material**.
6. Enter Play Mode — chunks stream around the player with blended biomes and GPU grass.

### Core scripts

`Assets/Scripts/WorldGeneration/`

- `TerrainGenerator.cs` — orchestrator
- `WorldSeed.cs` — deterministic seed streams
- `BiomeSystem.cs` — warped Voronoi regions + blending
- `Vegetation/` — GPU-instanced grass (no grass GameObjects)
