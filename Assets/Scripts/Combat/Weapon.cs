using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Weapon damage source with Elden Ring–style swept hit sampling.
/// During the active hit window, samples the blade path each FixedUpdate
/// with OverlapCapsule between previous and current tip/hilt positions
/// so fast swings do not tunnel through targets.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private WeaponType weaponType = WeaponType.Sword;
    [SerializeField, Min(0f)] private float baseDamage = 10f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask damageableLayers;
    [SerializeField] private Transform hiltPoint;
    [SerializeField] private Transform tipPoint;
    [SerializeField, Min(0.01f)] private float sweepRadius = 0.12f;
    [SerializeField, Range(1, 8)] private int sweepSegments = 3;
    [Tooltip("Fallback BoxCollider used only if sweep bones are missing")]
    [SerializeField] private bool useTriggerFallback = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;
    [SerializeField] private bool drawSweepGizmos = true;

    private BoxCollider triggerCollider;
    private int hitCount;
    private readonly HashSet<int> hitInstanceIds = new HashSet<int>();
    private readonly Collider[] overlapBuffer = new Collider[32];

    private bool hitWindowActive;
    private float currentSwingDamage;
    private float currentKnockback;
    private Vector3 previousHilt;
    private Vector3 previousTip;
    private bool hasPreviousSample;

    public WeaponType Type => weaponType;
    public float BaseDamage => baseDamage;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();

        if (tipPoint == null)
        {
            var tip = new GameObject("TipPoint");
            tip.transform.SetParent(transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            tipPoint = tip.transform;
        }

        if (hiltPoint == null)
            hiltPoint = transform;

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.enabled = false;
        }
        else if (useTriggerFallback && showDebugLogs)
        {
            Debug.LogWarning($"[Weapon] {name}: no BoxCollider — sweep-only mode.", this);
        }
    }

    private void FixedUpdate()
    {
        if (!hitWindowActive)
            return;

        SampleSweepHits();
    }

    /// <summary>
    /// Opens the hit window for this swing. Damage should already be
    /// weapon.baseDamage × attack.damageMultiplier.
    /// </summary>
    public void BeginSwing(float swingDamage, float knockbackForce)
    {
        currentSwingDamage = Mathf.Max(0f, swingDamage);
        currentKnockback = Mathf.Max(0f, knockbackForce);
        hitInstanceIds.Clear();
        hitCount = 0;
        hitWindowActive = true;
        hasPreviousSample = false;

        if (useTriggerFallback && triggerCollider != null)
            triggerCollider.enabled = true;

        if (showDebugLogs)
            Debug.Log($"[Weapon] {name} swing start dmg={currentSwingDamage}", this);
    }

    public void EndSwing()
    {
        hitWindowActive = false;
        hasPreviousSample = false;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (showDebugLogs)
            Debug.Log($"[Weapon] {name} swing end hits={hitCount}", this);
    }

    // --- Legacy API (kept so old PlayerCombat still compiles) ---

    public void EnableTriggerBox()
    {
        BeginSwing(baseDamage, 0f);
    }

    public void DisableTriggerBox()
    {
        EndSwing();
    }

    public void SetDamage(float newDamage)
    {
        // Treated as per-swing damage when using legacy PlayerCombat path.
        currentSwingDamage = Mathf.Max(0f, newDamage);
        if (showDebugLogs)
            Debug.Log($"[Weapon] {name} swing damage set to {currentSwingDamage}", this);
    }

    public void SetBaseDamage(float value)
    {
        baseDamage = Mathf.Max(0f, value);
    }

    public bool IsActive() => hitWindowActive;

    private void SampleSweepHits()
    {
        Vector3 hilt = hiltPoint.position;
        Vector3 tip = tipPoint.position;

        if (!hasPreviousSample)
        {
            previousHilt = hilt;
            previousTip = tip;
            hasPreviousSample = true;
            QueryCapsule(hilt, tip);
            return;
        }

        // Sample along the motion of the blade (Elden Ring–style continuous volume).
        for (int i = 1; i <= sweepSegments; i++)
        {
            float t = i / (float)sweepSegments;
            Vector3 sampleHilt = Vector3.Lerp(previousHilt, hilt, t);
            Vector3 sampleTip = Vector3.Lerp(previousTip, tip, t);
            QueryCapsule(sampleHilt, sampleTip);
        }

        previousHilt = hilt;
        previousTip = tip;
    }

    private void QueryCapsule(Vector3 a, Vector3 b)
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            a, b, sweepRadius, overlapBuffer, damageableLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col.transform.IsChildOf(transform))
                continue;

            TryDamage(col, (a + b) * 0.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Fallback only — swept sampling is the primary path.
        if (!hitWindowActive || !useTriggerFallback)
            return;

        if (!IsOnDamageableLayer(other.gameObject))
            return;

        TryDamage(other, other.ClosestPoint(tipPoint.position));
    }

    private void TryDamage(Collider other, Vector3 hitPoint)
    {
        int id = other.GetInstanceID();
        if (hitInstanceIds.Contains(id))
            return;

        DestroyOnHit destroyable = other.GetComponent<DestroyOnHit>();
        if (destroyable != null && !destroyable.CanBeDamagedBy(gameObject))
            return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            return;

        hitInstanceIds.Add(id);
        hitCount++;

        Vector3 direction = (other.transform.position - transform.position).normalized;
        var info = new DamageInfo(currentSwingDamage, currentKnockback, hitPoint, direction, gameObject);
        damageable.TakeDamage(info);

        if (showDebugLogs)
            Debug.Log($"[Weapon] hit {other.name} for {currentSwingDamage}", this);
    }

    private bool IsOnDamageableLayer(GameObject obj)
    {
        return ((1 << obj.layer) & damageableLayers) != 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSweepGizmos)
            return;

        Transform tip = tipPoint != null ? tipPoint : transform;
        Transform hilt = hiltPoint != null ? hiltPoint : transform;

        Gizmos.color = hitWindowActive ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(tip.position, sweepRadius);
        Gizmos.DrawWireSphere(hilt.position, sweepRadius);
        Gizmos.DrawLine(hilt.position, tip.position);
    }
}
