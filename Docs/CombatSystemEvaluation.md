# Combat System Evaluation

## Verdict

The current design (ScriptableObject combos + timed trigger windows) is solid for a prototype, but **absolute damage on AttackSO**, **per-attack AnimatorController swaps**, and **trigger-only hitboxes** are the main bottlenecks for feel and reliability. Prefer **weapon base damage × attack multipliers**, **cached weapon/combo state**, and **swept overlap sampling** during active frames (FromSoftware-style).

---

## Where It Is Slow / Expensive

### 1. Animator override swap every attack (highest cost)
`animator.runtimeAnimatorController = attack.animatorOverride` forces Unity to rebuild animation bindings. Doing this on every swing is far more expensive than triggering states on a single controller.

**Better:** one AnimatorController (or one `AnimatorOverrideController` instance) with clip slots swapped once when the weapon equips, or discrete attack states (`Attack1`, `Attack2`, …).

### 2. `UpdateCombo()` on every click
Each attack calls `GetComponentInChildren<Weapon>` and `GetComponentsInChildren<Transform>` + tag compares. That allocates and walks the hierarchy every swing.

**Better:** refresh only on equip/unequip (or when `handSlot` children change). Cache `Weapon` + `WeaponType`.

### 3. String / tag weapon identity
Tag walks and `switch` on strings are brittle and slower to reason about than an enum on `Weapon`.

### 4. Debug string building
Even behind `showDebugLogs`, interpolated multi-line logs allocate when enabled. Prefer a single gated helper or `#if UNITY_EDITOR`.

### 5. Trigger-only hit detection (misses, not CPU)
Thin BoxColliders that move fast between FixedUpdates **tunnel** through targets. Elden Ring / Souls games avoid relying solely on continuous collision; they **sample the blade path** during active frames.

---

## Damage Multiplier Model

| Attack | Multiplier | Example (base 20) |
|--------|------------|-------------------|
| Swing 1 | 1.0× | 20 |
| Swing 2 | 1.5× | 30 |
| Swing 3 | 2.0× | 40 |

- `Weapon.baseDamage` = weapon power
- `AttackSO.damageMultiplier` = combo step scale
- Final damage = `baseDamage * damageMultiplier` (optional crit/status later)

This keeps balancing on the weapon asset, not duplicated on every AttackSO.

---

## Elden Ring–Style Hit Detection

FromSoftware-style melee typically:

1. **Active frames** define when hits are allowed (you already have `hitStartTime` / `hitEndTime`).
2. Each frame (or FixedUpdate) during the window, record blade tip / hilt points.
3. **Sweep** between previous and current positions with `Physics.OverlapCapsule` / sphere samples along the segment.
4. Deduplicate hits per swing (`HashSet`).
5. Optionally filter by facing / root motion so back-hits feel intentional.

Triggers alone are fine as a fallback for slow weapons; swept sampling is what makes fast arcs reliable.

---

## Other Improvements Worth Making

- **Input buffering:** queue an attack input during the last portion of cooldown instead of hard-blocking with `isAttacking`.
- **Animation Events** for hit open/close (more accurate than fixed seconds if clip speed changes).
- **Knockback from AttackSO** applied in `TakeDamage` / a hit payload struct (`DamageInfo`).
- **LayerMask** stays; keep it as the first reject (you already do this well).
- Avoid `GetComponent` in `OnTriggerEnter` hot paths when possible — cache `IDamageable` via a registry or require it on known layers only.

See `Assets/Scripts/Combat/` for a refactored implementation of these ideas.
