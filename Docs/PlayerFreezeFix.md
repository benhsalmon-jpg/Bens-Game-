# Player freeze / can't move

## Why it happened "randomly after a while"

`Inventory.FreezePlayer(true)` **disabled the CharacterController** and saved `enabled` flags. That fails in two ways that show up as a delayed, permanent freeze:

1. **Lock leak** — a second freeze (chest while inventory is open, `FreezePlayer(true)` from another menu, a chest trigger that fires extra times) overwrote the saved flags with `false`, or incremented a count that never returned to 0. Unfreeze then restored `enabled = false`. Movement never came back until reload.

2. **Disabling CharacterController** — while the capsule is off, streamed terrain **rebuilds MeshColliders** under the player. Re-enabling the controller later can leave it embedded in geometry so `Move()` does nothing even though scripts are on.

## Fix

- Freeze is a **named lock set** (`inventory`, `chest`, `external`), not a counter or saved `enabled` snapshot.
- **CharacterController stays enabled.** Only the movement and camera scripts are toggled.
- **Watchdog** (~4 Hz): if no pause UI is open but movement is still locked, it releases the player.
- `FreezePlayer(bool)` still exists for chests/crafting; it maps to the `external` lock.

Replace your project's `Inventory.cs` with `Assets/Scripts/Inventory/Inventory.cs`.
