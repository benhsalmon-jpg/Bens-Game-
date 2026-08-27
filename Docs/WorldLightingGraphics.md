# World grounding, lighting, and graphics quality

## 1. Objects touching the ground

`TerrainGenerator.SpawnImportantObjects` used to place with:

`terrainY + bounds.extents.y`

That only works when the prefab pivot is at the **center**. Trees/rocks with pivots at the base (or offset meshes) floated or sank.

**Fix:** ground using the lowest world bound:

`position.y += (terrainY + yOffset) - bounds.min.y`

Also:
- Clamps scatter back into the chunk before sampling height
- Optional `alignToTerrainNormal` on `BiomeObject` so props sit flush on slopes

### BiomeObject new fields
- `alignToTerrainNormal` (default true)
- `normalAlignStrength` (0–1)
- `yOffset` still works (negative = bury slightly)

Replace your old `TerrainGenerator.cs` with `Assets/Scripts/World/TerrainGenerator.cs`.

---

## 2. Optimized lighting

Add an empty object in `Current` with **`OptimizedWorldLighting`**:

- One directional **Sun** only (extra directional lights are disabled)
- Cheap **Trilight** ambient (stable after Menu → Current loads)
- Optional exponential fog
- Skips bounce intensity / keeps shadow casting on the sun only
- Calls `DynamicGI.UpdateEnvironment()` on scene load

Also keep `SceneLoadLightingFix` for Menu → Current ambient refresh.

---

## 3. Graphics settings: Low / Medium / High / Max (shadows)

| Preset | Shadows | Resolution | Distance | Cascades | Soft |
|--------|---------|------------|----------|----------|------|
| **Low** | Off | Low | 35 | 1 | No |
| **Medium** | Hard | Medium | 60 | 2 | No |
| **High** | All | High | 100 | 4 | Yes |
| **Max** | All | Very High | 150 | 4 | Yes |

### Setup
1. Create empty `GraphicsSettings` → add **`GraphicsSettingsController`**
2. Assign / add **`OptimizedWorldLighting`** (controller finds it if left empty)
3. On your settings menu:
   - Add **`GraphicsSettingsUI`**
   - Assign a Dropdown with options **Low, Medium, High, Max**  
     **or** wire four buttons to `OnClickLow/Medium/High/Max`

Preset is saved in `PlayerPrefs` (`GraphicsQualityPreset`).

---

## Efficiency notes

- Shadows dominate cost outdoors — Low/Medium help the most on weak GPUs
- Keep grass on `ShadowCastingMode.Off` (already in TerrainGenerator)
- Only one directional light
- Prefer Trilight ambient over realtime GI for procedural worlds
