# Ben's Game

Unity combat prototype.

## Combat refactor

See `Docs/CombatSystemEvaluation.md` and `Assets/Scripts/Combat/`.

**Damage model:** `finalDamage = weapon.baseDamage × attack.damageMultiplier`

**Hit detection:** swept `OverlapCapsule` along hilt→tip each FixedUpdate during the active window (Elden Ring–style), with optional BoxCollider trigger fallback.
