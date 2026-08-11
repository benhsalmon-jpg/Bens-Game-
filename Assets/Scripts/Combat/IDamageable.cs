using UnityEngine;

/// <summary>
/// Payload passed to damageables so knockback / sources stay consistent.
/// Keep ONLY ONE copy of this struct in the project.
/// </summary>
public struct DamageInfo
{
    public float Amount;
    public float KnockbackForce;
    public Vector3 HitPoint;
    public Vector3 HitDirection;
    public GameObject Source;

    public DamageInfo(float amount, float knockback, Vector3 hitPoint, Vector3 hitDirection, GameObject source)
    {
        Amount = amount;
        KnockbackForce = knockback;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        Source = source;
    }

    /// <summary>Convenience for callers that only have a damage number.</summary>
    public static DamageInfo FromAmount(float amount)
    {
        return new DamageInfo(amount, 0f, Vector3.zero, Vector3.zero, null);
    }
}

/// <summary>
/// Objects that can take damage. Keep ONLY ONE copy of this interface.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

/// <summary>
/// Optional helper when you only care about the numeric amount.
/// </summary>
public abstract class DamageableBehaviour : MonoBehaviour, IDamageable
{
    public void TakeDamage(DamageInfo info) => ApplyDamage(info.Amount);

    protected abstract void ApplyDamage(float amount);
}
