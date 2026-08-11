# Modular Placement & Snapping

Drop-in system for grid placement plus modular socket snapping (walls attaching side-by-side, floors tiling, etc.).

## Scripts

| Script | Role |
|---|---|
| `PlaceableObject` | Ghost preview, grid snap, modular snap, place on click |
| `SnapType` | ScriptableObject defining compatible socket categories |
| `SnapPoint` | Child transform socket on a prefab edge/corner |
| `SnappableObject` | Registers placed objects into the snap registry |
| `SnapRegistry` / `SnapResolver` | Find & score nearby free sockets |
| `GhostMaterialUtil` / `GhostMaterialFactory` | Valid / invalid / **snapped** ghost materials |

## Unity setup (walls example)

1. **Create a SnapType asset**  
   `Create > Placement > Snap Type` → name it `WallEdge`. Leave `selfCompatible` on.

2. **Create ghost materials**  
   `Assets > Create > Placement > Ghost Materials (Valid/Invalid/Snapped)`  
   Assign them on your `PlaceableObject` (`validGhostMat`, `invalidGhostMat`, `snappedGhostMat`).

3. **Author the placed wall prefab**
   - Add `SnappableObject` on the root.
   - Add empty children at the left/right (and optionally front/back) edges.
   - Add `SnapPoint` to each child:
     - `snapType` = WallEdge
     - Left: `socketTag = Left`, `requiredPartnerTag = Right`, forward pointing outward
     - Right: `socketTag = Right`, `requiredPartnerTag = Left`, forward pointing outward
     - `yawOffset = 180` so the next wall faces correctly
   - Or enable `autoGenerateEdgePoints` on `SnappableObject` and assign `autoSnapType` + `footprint`.

4. **Hand / placement item**
   - Put `PlaceableObject` on the hand prefab (or a child).
   - Set `placedPrefab` to the world wall prefab from step 3.
   - Enable `useModularSnapping`.
   - Assign the three ghost materials.

5. **Ground**
   - Ensure ground uses a layer included in `groundMask`.

## Behaviour notes

- While aiming near a free socket, the ghost locks to it and uses the **snapped** material.
- Placing connects the two sockets (`occupied`) so that edge won’t accept another piece.
- Neighbor sockets on the new wall stay free, so you can keep extending the run.
- Switching hotbar slots calls `CancelPlacement()` and destroys the ghost — no leftover ghost materials.

## Extending

- Floors: new `SnapType` FloorEdge, sockets on all four sides, same PlaceableObject.
- Corners / pillars: unique `socketTag` pairs (`CornerA` ↔ `CornerB`).
- Cross-type links: add the other type to `SnapType.compatibleWith`.
