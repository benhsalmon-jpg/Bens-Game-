# Deterministic World Generation System

Valheim-style procedural terrain with independent seed streams, blended biomes, per-biome elevation profiles, staged chunk streaming, and GPU-instanced grass.

## Project assumptions

This repository did not contain a full Unity project when the system was authored. The implementation therefore targets:

| Item | Choice |
|------|--------|
| Unity | **2022.3 LTS** (see `ProjectSettings/ProjectVersion.txt`) |
| Render pipeline | **Built-in RP** primary (`Custom/TerrainBiomeBlend` surface shader). Works with **URP** for meshes/instancing; assign a URP-compatible terrain material if you use URP |
| Jobs / Burst | **Not required** — staged **coroutines** avoid frame spikes |
| Grass rendering | `Graphics.DrawMeshInstanced` (Built-in + URP, max 1023 / batch) |

Drop the `Assets/` folder into your Unity project (or open this folder as a Unity project and let Unity generate `.meta` files).

---

## Architecture

```
WorldSeed
   ↓ independent streams (Terrain / Biome / Objects / Grass / ...)
BiomeSystem (warped Voronoi capitals + blend weights)
   ↓
Terrain sampling (per-biome profiles blended by weights)
   ↓
TerrainChunkData (pure data)
   ↓ staged coroutine
Mesh + Collider + Important Objects + GPU Vegetation
   ↓
Chunk streaming (priority + hysteresis + LOD)
```

### Files

| Script | Role |
|--------|------|
| `WorldSeed.cs` | Master seed + `GetSystemSeed` / `GetChunkSeed` / `GetPositionSeed` |
| `TerrainNoise.cs` | Deterministic layered Perlin + biome terrain evaluation |
| `BiomeDefinition.cs` | `Biome`, terrain/vegetation settings, `BiomeSample`, objects |
| `BiomeSystem.cs` | Capitals, domain warp, blend weights, elevation gate |
| `TerrainChunkData.cs` | Pure chunk data + lifecycle states |
| `TerrainChunk.cs` | Scene wrapper (mesh/collider/objects) |
| `TerrainGenerator.cs` | Orchestrator: streaming, LOD, mesh, API |
| `VegetationSystem.cs` | Deterministic grass placement (matrices only) |
| `VegetationRenderer.cs` | GPU instancing draw calls |
| `TerrainDeterminismDebugger.cs` | Reload/verify fingerprint UI |
| `TerrainBiomeBlend.shader` | Shared material biome blend |

---

## Determinism

- One master seed: `worldSeed` **or** stable hashed `seedString`
- Systems derive **independent** streams via `WorldSeed.GetSystemSeed("Grass")` etc.
- Changing grass settings cannot alter terrain/biome RNG
- Placement uses `GetPositionSeed(chunk, x, z, system)` / integer hashes — **never** `UnityEngine.Random` for world content
- Same seed + coordinates ⇒ same height, biome weights, objects, grass cells

---

## Biome blending

`BiomeSample` exposes:

- `primaryBiome` / `secondaryBiome` / `tertiaryBiome`
- `primaryWeight` / `secondaryWeight` / `tertiaryWeight`

Borders use domain warp + `biomeBlendDistance` with `SmoothStep` falloff. Terrain height is:

```
finalHeight = Σ EvaluateBiomeTerrain(biome_i, pos) * weight_i
```

Materials blend in the shader from packed vertex data (not hard chunk materials).

### Vertex channels

| Channel | Meaning |
|---------|---------|
| Color.R | Primary weight |
| Color.G | Secondary weight |
| Color.B | Primary biome index / 32 |
| Color.A | Secondary biome index / 32 |
| UV2.x | Tertiary weight |
| UV2.y | Tertiary biome index / 32 |

---

## Grass (GPU)

- **No grass GameObjects**
- Cell grid (`grassCellSize`) + deterministic jitter
- Slope / elevation filters from biome vegetation settings
- Density fades with distance (`nearDistance` → `farDistance`) without destroying instances every frame
- Drawn with `Graphics.DrawMeshInstanced`

Important objects (trees, resources) remain GameObjects. Decorative grass/flowers use instancing.

---

## Chunk streaming

| Distance | Purpose |
|----------|---------|
| `terrainLoadDistance` / `terrainUnloadDistance` | Mesh chunks + hysteresis |
| `vegetationLoadDistance` / `vegetationUnloadDistance` | Grass batches |
| `objectLoadDistance` / `objectUnloadDistance` | Important prefabs |

- Squared-distance checks
- Nearest chunks generated first
- Staged coroutine: samples → normals → mesh → collider → objects → vegetation
- Generation tokens discard stale work after unload
- Simple LOD: full / half / quarter resolution by distance

---

## Inspector setup

1. Create empty GameObject → add `TerrainGenerator`
2. Assign **Player** (`playerTransform` or tag `Player`)
3. Configure **Seed** (`worldSeed` or `seedString` + `useSeedString`)
4. Create biomes in the **Biomes** array:
   - Mark one `isSpawnBiome`
   - Set world-ring min/max distance
   - Tune `terrain` profile (Meadows / Forest / Mountains / Swamp / Plains helpers exist on `BiomeTerrainSettings`)
   - Set `vegetation.density` and `grassTypes` (mesh + material with **GPU Instancing** enabled)
   - Add `objects` with `category = Important` for trees/rocks
5. Create material using shader `Custom/TerrainBiomeBlend`, assign textures/colors for biome slots 0–3, assign to **Terrain Blend Material**
6. Tune streaming distances (unload > load)
7. Optional: add `TerrainDeterminismDebugger`, enable probe

### Suggested biome densities

| Biome | Grass density |
|-------|---------------|
| Meadows | 0.80 |
| Black Forest | 0.65 |
| Swamp | 0.50 |
| Mountains | 0.10 |
| Plains | 0.95 |

---

## Shader Graph / URP notes

Built-in surface shader `Custom/TerrainBiomeBlend` will not compile under URP/HDRP.

For URP Shader Graph:

1. Create a Lit Graph
2. Sample up to 4 textures
3. Read **Vertex Color** → split R/G (weights), B/A (indices × 32)
4. Read **UV1** (UV2 in mesh) for tertiary weight/index
5. Lerp/add albedo by weights
6. Assign that material to `terrainBlendMaterial`

Instanced grass materials must have **Enable GPU Instancing** checked.

---

## Determinism test

1. Seed `12345`, enter Play Mode near chunk `(0,0)`
2. Enable **Enable Determinism Probe** (or use `TerrainDeterminismDebugger`)
3. Record Height / Grass / Object counts
4. Exit and re-enter Play Mode — values for the same seed + chunk center must match
5. Change only grass density — terrain height fingerprint core must stay stable for the same seed (grass count may change by design)

Debug gizmos (on selected generator): chunk bounds, biome capitals, blend weights, normals.

---

## Public API

```csharp
int GetResolvedSeed();
BiomeSample GetBiomeSample(Vector3 worldPosition);
float GetTerrainHeight(Vector3 worldPosition);
TerrainChunkData GetChunkData(Vector2Int chunkCoordinate);
bool IsChunkLoaded(Vector2Int coordinate);
```

---

## Migration from the previous single-file script

| Old | New |
|-----|-----|
| `worldSeed` / `seedString` / `useSeedString` | Same fields on `TerrainGenerator` |
| `chunkLoadDistance` | `terrainLoadDistance` |
| `chunkUnloadDistance` | `terrainUnloadDistance` |
| `Biome` fields | Extended with `terrain`, `vegetation`; old spawn/elevation/object fields kept |
| `BiomeObject` | Added `category`, slope/elevation filters |
| Per-chunk biome material | Shared `terrainBlendMaterial` + vertex weights |
| Grass as prefab instances | GPU `GrassType` meshes (prefabs under Decorative are skipped by object spawner) |

Re-assign biome arrays on the component after importing; ScriptableObject migration is not required (biomes remain serializable inline arrays).
