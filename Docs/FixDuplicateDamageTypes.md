# Fix: DamageInfo / IDamageable ambiguity

## Cause
Unity had **two copies** of `DamageInfo` and `IDamageable` in the global namespace (e.g. one from `IDamageable.cs` and another pasted as a second interface file). That produces:

- `The namespace '<global namespace>' already contains a definition for 'DamageInfo'`
- `Ambiguity between 'DamageInfo.Amount' and 'DamageInfo.Amount'`
- Unassigned-field noise on the duplicate struct

## Fix
1. Keep **one** `IDamageable.cs` (contains `DamageInfo`, `IDamageable`, `DamageableBehaviour`).
2. Delete any other file that re-declares `DamageInfo` or `IDamageable`.
3. Keep **one** `WeaponType` enum.
4. `DestroyOnHit` must implement `void TakeDamage(DamageInfo info)` (not only `float`).

Do **not** put default interface methods on `IDamageable` for the float overload — that needs newer C# and still duplicates concepts. Use `DamageInfo.FromAmount(amount)` or a concrete `TakeDamage(float)` on the MonoBehaviour instead.
