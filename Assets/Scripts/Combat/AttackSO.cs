using UnityEngine;

/// <summary>
/// Single attack step in a combo.
/// Damage is a multiplier of the equipped weapon's base damage
/// (e.g. 1.0 = first swing, 1.5 = second swing).
/// </summary>
[CreateAssetMenu(menuName = "Combat/Attack")]
public class AttackSO : ScriptableObject
{
    [Header("Damage")]
    [Tooltip("Final damage = weapon.baseDamage × this multiplier")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    [Header("Feel")]
    [SerializeField, Min(0f)] private float knockbackForce = 5f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.6f;

    [Header("Hit Window (seconds from attack start)")]
    [SerializeField, Min(0f)] private float hitStartTime = 0.15f;
    [SerializeField, Min(0f)] private float hitEndTime = 0.35f;

    [Header("Animation")]
    [Tooltip("Prefer discrete animator states over swapping controllers per attack")]
    [SerializeField] private string animatorTrigger = "Attack";
    [SerializeField] private AnimatorOverrideController animatorOverride;

    public float DamageMultiplier => damageMultiplier;
    public float KnockbackForce => knockbackForce;
    public float AttackCooldown => attackCooldown;
    public float HitStartTime => hitStartTime;
    public float HitEndTime => hitEndTime;
    public string AnimatorTrigger => animatorTrigger;
    public AnimatorOverrideController AnimatorOverride => animatorOverride;

    /// <summary>Compute outgoing damage from a weapon's base damage.</summary>
    public float GetDamage(float weaponBaseDamage)
    {
        return Mathf.Max(0f, weaponBaseDamage * damageMultiplier);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hitEndTime < hitStartTime)
            hitEndTime = hitStartTime;

        if (attackCooldown < hitEndTime)
            attackCooldown = hitEndTime;
    }
#endif
}
