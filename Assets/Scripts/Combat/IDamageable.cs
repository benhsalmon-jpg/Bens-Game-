using UnityEngine;

/// <summary>
/// Payload passed to damageables so knockback / sources stay consistent.
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
}

public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

/// <summary>
/// Optional helper for scripts that only care about the numeric amount.
/// </summary>
public abstract class DamageableBehaviour : MonoBehaviour, IDamageable
{
    public void TakeDamage(DamageInfo info) => TakeDamage(info.Amount);
    protected abstract void TakeDamage(float amount);
}
