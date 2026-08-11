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
| Vegetation Mesh | drag ANY mesh (quad, FBX blade, bush…) — or leave empty for auto default |
| Vegetation Material | your `GrassGPU` material — or leave empty for auto default |
| Grass Cell Size | `1.25` (try `1` for denser) |
| Grass Density Multiplier | `1` |
| Force Grass When Enabled | ✅ (plants even if biome density was 0) |
| Create Default Grass If Missing | ✅ |
| Force Enable Instancing On Materials | ✅ |
| Vegetation Load Distance | `120`+ |
| Player Transform | required |

**Important:** `Vegetation Material` is separate from elevation `Grass Material` (dirt/rock layering). Do not confuse them.

## 3. Optional per-biome override

Biomes → Vegetation → Grass Types → Element 0:
- Mesh / Material (overrides global if set)
- Density `1`
- Min/Max Scale `0.8` / `1.5`

## 4. Verify

Play Mode → on-screen **GPU Grass Debug**:
- **Generated** and **Drawn** should both be > 0

If Generated=0: player missing, Enable Grass off, or chunks not loaded yet (walk a few meters).
If Generated>0 but Drawn=0: Vegetation Mesh/Material missing.
If Drawn>0 but invisible: material missing GPU Instancing or wrong shader (use `Custom/GrassInstanced`).

## 5. Paste-into-Unity (two scripts)

If you cannot pull from GitHub, paste these two files as scripts (delete old copies first):

1. `PASTE_TerrainGenerator.cs.txt` → `Assets/Scripts/TerrainGenerator.cs`
2. `PASTE_TerrainWorldSupport.cs.txt` → `Assets/Scripts/TerrainWorldSupport.cs`

Only attach **TerrainGenerator** to a GameObject. Also copy `Assets/Shaders/GrassInstanced.shader`.
