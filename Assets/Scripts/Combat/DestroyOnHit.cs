using UnityEngine;

/// <summary>
/// Destructible resources (rocks, trees, etc.)
/// Takes damage only from specific weapon layers.
/// Implements IDamageable for System 2 combat (DamageInfo).
/// </summary>
public class DestroyOnHit : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private GameObject lootPrefab;
    [SerializeField] private int lootCount = 1;
    [SerializeField] private float lootSpread = 1f;
    [SerializeField] private GameObject destroyEffect;

    [Header("Weapon Restrictions")]
    [SerializeField] private LayerMask damageFromLayers;

    [Header("Loot Drop Point")]
    [SerializeField] private Transform dropPoint;

    [SerializeField] private bool showDebugLogs = true;

    private float currentHealth;
    private bool isDestroyed;

    private void Start()
    {
        currentHealth = maxHealth;

        if (dropPoint == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[DestroyOnHit] {name} has no drop point, using transform.", this);
            dropPoint = transform;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[DestroyOnHit] {name} ready | HP {maxHealth} | layers {GetLayerMaskNames(damageFromLayers)} | drop {dropPoint.name}",
                this);
        }
    }

    /// <summary>Required by IDamageable — Weapon calls this.</summary>
    public void TakeDamage(DamageInfo info)
    {
        if (isDestroyed)
            return;

        if (info.Source != null && !CanBeDamagedBy(info.Source))
            return;

        ApplyDamage(info.Amount);
    }

    /// <summary>Optional helper for non-combat callers (not part of IDamageable).</summary>
    public void TakeDamage(float amount)
    {
        if (isDestroyed)
            return;

        ApplyDamage(amount);
    }

    private void ApplyDamage(float amount)
    {
        currentHealth -= amount;

        if (showDebugLogs)
            Debug.Log($"[DestroyOnHit] {name} took {amount}. HP {currentHealth}/{maxHealth}", this);

        if (currentHealth <= 0f)
            BreakApart();
    }

    public bool CanBeDamagedBy(GameObject weaponGameObject)
    {
        int weaponLayer = 1 << weaponGameObject.layer;
        bool canDamage = (weaponLayer & damageFromLayers) != 0;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[DestroyOnHit] {weaponGameObject.name} ({LayerMask.LayerToName(weaponGameObject.layer)}) " +
                $"vs {name}: {(canDamage ? "YES" : "NO")}",
                this);
        }

        return canDamage;
    }

    private string GetLayerMaskNames(LayerMask mask)
    {
        string names = "";
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
                names += LayerMask.LayerToName(i) + ", ";
        }
        return names.TrimEnd(',', ' ');
    }

    private void BreakApart()
    {
        isDestroyed = true;

        if (destroyEffect != null)
            Instantiate(destroyEffect, transform.position, Quaternion.identity);

        DropLoot();

        if (showDebugLogs)
            Debug.Log($"[DestroyOnHit] destroyed {name}", this);

        Destroy(gameObject);
    }

    private void DropLoot()
    {
        if (lootPrefab == null)
            return;

        Vector3 dropPosition = dropPoint != null ? dropPoint.position : transform.position;

        for (int i = 0; i < lootCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-lootSpread, lootSpread),
                0.5f,
                Random.Range(-lootSpread, lootSpread));

            Instantiate(lootPrefab, dropPosition + offset, Quaternion.identity);
        }
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}
