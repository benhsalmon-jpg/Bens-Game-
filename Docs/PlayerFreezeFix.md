# Player freeze / can't move fix

## Root cause
`Inventory.FreezePlayer(true)` saved `CharacterController` / movement / camera `enabled` flags, then disabled them. A **second** freeze (opening a chest twice, chest while another UI froze the player, etc.) overwrote those saved flags with `false`.  

`FreezePlayer(false)` then restored `enabled = false` → character permanently stuck until reload.

## Fix
- **Ref-counted freeze**: only the first freeze captures enabled state; unfreeze restores only when the count returns to 0
- Tab inventory open/close participates in the same freeze system
- `OpenChestUI` ignores duplicate opens / closes previous chest cleanly
- `EnsurePlayerUnfrozen()` + `OnDisable` safety so scene/UI teardown can't leave the player locked
- Cursor lock/visibility restored consistently (`locked` ⇒ not visible)

Replace your `Inventory.cs` with `Assets/Scripts/Inventory/Inventory.cs`.
