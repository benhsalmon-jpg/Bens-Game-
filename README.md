# Bens-Game-

Unity survival / building / procedural world prototype.

## Systems

- **World gen / grounding:** `Assets/Scripts/World/TerrainGenerator.cs` — objects now sit on terrain via `bounds.min.y` (see `Docs/WorldLightingGraphics.md`)
- **Lighting + quality:** `OptimizedWorldLighting`, `GraphicsSettingsController` (Low / Medium / High / Max shadows)
- **Modular snapping:** see other branches / `Docs/ModularSnapping.md` when present
