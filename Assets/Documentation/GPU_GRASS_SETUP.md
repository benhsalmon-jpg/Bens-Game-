# GPU Grass Setup (Inspector)

## 1. Create the grass material

1. Create → Material → name it `GrassGPU`
2. Shader = `Custom/GrassInstanced` (or `Universal Render Pipeline/Unlit` / `Unlit/Color`)
3. Check **Enable GPU Instancing**
4. Set a bright green color

## 2. Assign on TerrainGenerator

On your TerrainGenerator component:

| Field | Value |
|-------|--------|
| Enable Grass | ✅ |
| Grass Mesh | drag ANY mesh (quad, FBX blade, bush…) |
| Grass Material | your `GrassGPU` material |
| Grass Cell Size | `1.5` (try `1` for denser) |
| Grass Density Multiplier | `1` |
| Create Default Grass If Missing | ✅ |
| Force Enable Instancing On Materials | ✅ |
| Vegetation Load Distance | `120`+ |
| Show Vegetation Density | ✅ (shows drawn count on screen) |

## 3. Optional per-biome override

Biomes → Vegetation → Grass Types → Element 0:
- Mesh / Material (overrides global if set)
- Density `1`
- Min/Max Scale `0.8` / `1.5`

## 4. Verify

Play Mode → on-screen **Drawn grass instances** should be > 0.

If 0: player too far, density 0, or no biomes.
If >0 but invisible: material missing GPU Instancing or wrong shader.
