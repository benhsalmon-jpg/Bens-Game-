using UnityEngine;
using System.Collections;

/// <summary>
/// AI controller for enemies.
/// Implements IDamageable for System 2 combat (DamageInfo).
/// </summary>
public class EnemyAI : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float stoppingDistance = 1f;

    [Header("Combat")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackKnockback = 3f;

    [Header("Death")]
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private float destroyDelay = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Transform player;
    private Rigidbody rb;
    private Animator animator;
    private Renderer meshRenderer;
    private EnemyAnimationController animationController;
    private Color originalColor;

    private float currentHealth;
    private float lastAttackTime;
    private bool isDead;

    private void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<Renderer>();
        animationController = GetComponent<EnemyAnimationController>();

        if (rb != null)
        {
            if (rb.isKinematic)
                rb.isKinematic = false;
            if (!rb.useGravity)
                rb.useGravity = true;

            rb.constraints = RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationY
                | RigidbodyConstraints.FreezeRotationZ;
        }

        if (meshRenderer != null)
            originalColor = meshRenderer.material.color;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogError($"[EnemyAI] {name} could not find Player tag.", this);

        currentHealth = maxHealth;
        lastAttackTime = -attackCooldown;
    }

    private void Update()
    {
        if (isDead || player == null)
            return;

        ChasePlayer();
        TryAttackPlayer();
    }

    private void ChasePlayer()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > stoppingDistance)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            rb.linearVelocity = new Vector3(direction.x * chaseSpeed, rb.linearVelocity.y, direction.z * chaseSpeed);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    private void TryAttackPlayer()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > attackRange || Time.time - lastAttackTime < attackCooldown)
            return;

        IDamageable playerDamageable = player.GetComponent<IDamageable>();
        if (playerDamageable == null)
        {
            Debug.LogError($"[EnemyAI] Player has no IDamageable.", this);
            return;
        }

        Vector3 hitPoint = player.position;
        Vector3 hitDirection = (player.position - transform.position).normalized;
        var info = new DamageInfo(attackDamage, attackKnockback, hitPoint, hitDirection, gameObject);

        playerDamageable.TakeDamage(info);
        lastAttackTime = Time.time;

        if (showDebugLogs)
            Debug.Log($"[EnemyAI] {name} hit player for {attackDamage}", this);
    }

    /// <summary>Required by IDamageable.</summary>
    public void TakeDamage(DamageInfo info)
    {
        if (isDead)
            return;

        currentHealth -= info.Amount;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[EnemyAI] {name} took {info.Amount} | HP {currentHealth}/{maxHealth} | " +
                $"from {(info.Source != null ? info.Source.name : "Unknown")}",
                this);
        }

        if (rb != null && info.KnockbackForce > 0f)
        {
            Vector3 knockbackVelocity = info.HitDirection * info.KnockbackForce;
            knockbackVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = knockbackVelocity;
        }

        if (meshRenderer != null)
            StartCoroutine(HitFlash());

        if (animationController != null)
            animationController.PlayHurtAnimation();

        if (currentHealth <= 0f)
            Die();
    }

    /// <summary>Optional helper for non-interface callers.</summary>
    public void TakeDamage(float amount)
    {
        TakeDamage(DamageInfo.FromAmount(amount));
    }

    private IEnumerator HitFlash()
    {
        meshRenderer.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        meshRenderer.material.color = originalColor;
    }

    private void Die()
    {
        isDead = true;

        if (animator != null)
            animator.SetTrigger("Die");

        if (rb != null)
            rb.isKinematic = true;

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject, destroyDelay);
    }

    public float GetAttackRange() => attackRange;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;
}
