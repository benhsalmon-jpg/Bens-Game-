using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Combo-driven player combat.
/// - Damage = weapon.baseDamage × AttackSO.damageMultiplier
/// - Caches weapon/combo on equip instead of scanning every click
/// - Uses swept hit windows on Weapon (Elden Ring–style sampling)
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Setup")]
    [SerializeField] private List<WeaponComboBinding> comboBindings = new List<WeaponComboBinding>();
    [SerializeField] private ComboSequence defaultCombo;
    [SerializeField] private GameObject handSlot;

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Input Buffer")]
    [Tooltip("Seconds before cooldown ends where a press is queued")]
    [SerializeField, Min(0f)] private float inputBufferTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;

    private int comboIndex;
    private float lastAttackTime = -Mathf.Infinity;
    private float lastComboInputTime = -Mathf.Infinity;
    private bool isAttacking;
    private bool bufferedAttack;
    private ComboSequence currentCombo;
    private Weapon currentWeapon;
    private Coroutine attackRoutine;
    private readonly Dictionary<WeaponType, ComboSequence> comboLookup = new Dictionary<WeaponType, ComboSequence>();

    [System.Serializable]
    public struct WeaponComboBinding
    {
        public WeaponType weaponType;
        public ComboSequence combo;
    }

    private void Awake()
    {
        comboLookup.Clear();
        foreach (var binding in comboBindings)
        {
            if (binding.combo == null)
                continue;
            comboLookup[binding.weaponType] = binding.combo;
        }
    }

    private void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        RefreshEquippedWeapon();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            TryAttack();
    }

    /// <summary>
    /// Call this from your inventory/equip system when the hand weapon changes.
    /// Avoids hierarchy scans every swing.
    /// </summary>
    public void RefreshEquippedWeapon()
    {
        currentWeapon = null;
        currentCombo = defaultCombo;

        if (handSlot == null)
        {
            LogError("Hand Slot is not assigned.");
            return;
        }

        currentWeapon = handSlot.GetComponentInChildren<Weapon>(true);
        if (currentWeapon == null)
        {
            LogError($"No Weapon under {handSlot.name}.");
            return;
        }

        if (!comboLookup.TryGetValue(currentWeapon.Type, out currentCombo) || currentCombo == null)
            currentCombo = defaultCombo;

        comboIndex = 0;

        if (showDebugLogs && currentCombo != null)
            Debug.Log($"[PlayerCombat] Equipped {currentWeapon.Type}, combo attacks={currentCombo.GetAttackCount()}", this);
    }

    private void TryAttack()
    {
        if (isAttacking)
        {
            // Buffer late inputs near the end of the current swing (Souls-like).
            if (attackRoutine != null)
                bufferedAttack = true;
            return;
        }

        Attack();
    }

    private void Attack()
    {
        if (currentWeapon == null || currentCombo == null || currentCombo.GetAttackCount() == 0)
        {
            RefreshEquippedWeapon();
            if (currentWeapon == null || currentCombo == null || currentCombo.GetAttackCount() == 0)
            {
                LogError("No combo/weapon loaded.");
                return;
            }
        }

        if (Time.time - lastComboInputTime > currentCombo.ComboResetTime)
            comboIndex = 0;

        if (Time.time - lastAttackTime < currentCombo.ClickInputWindow)
            return;

        if (comboIndex >= currentCombo.GetAttackCount())
            comboIndex = 0;

        AttackSO attack = currentCombo.GetAttack(comboIndex);
        if (attack == null)
            return;

        ExecuteAttack(attack);
        lastComboInputTime = Time.time;
        comboIndex++;
    }

    private void ExecuteAttack(AttackSO attack)
    {
        if (animator == null || currentWeapon == null)
        {
            LogError("Animator or weapon missing.");
            return;
        }

        // Prefer triggers on a stable controller. Override swap is optional and costly.
        if (attack.AnimatorOverride != null)
            animator.runtimeAnimatorController = attack.AnimatorOverride;

        string trigger = string.IsNullOrEmpty(attack.AnimatorTrigger) ? "Attack" : attack.AnimatorTrigger;
        animator.SetTrigger(trigger);

        float swingDamage = attack.GetDamage(currentWeapon.BaseDamage);
        // Keep legacy SetDamage in sync for any external readers.
        currentWeapon.SetDamage(swingDamage);

        isAttacking = true;
        bufferedAttack = false;
        lastAttackTime = Time.time;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(ExecuteAttackCoroutine(attack, swingDamage));

        if (showDebugLogs)
        {
            Debug.Log(
                $"[PlayerCombat] {attack.name} dmg={swingDamage} " +
                $"({currentWeapon.BaseDamage} × {attack.DamageMultiplier})",
                this);
        }
    }

    private IEnumerator ExecuteAttackCoroutine(AttackSO attack, float swingDamage)
    {
        yield return new WaitForSeconds(attack.HitStartTime);

        currentWeapon.BeginSwing(swingDamage, attack.KnockbackForce);

        float hitDuration = Mathf.Max(0f, attack.HitEndTime - attack.HitStartTime);
        yield return new WaitForSeconds(hitDuration);

        currentWeapon.EndSwing();

        float remainingCooldown = Mathf.Max(0f, attack.AttackCooldown - attack.HitEndTime);
        float preBuffer = Mathf.Max(0f, remainingCooldown - inputBufferTime);

        if (preBuffer > 0f)
            yield return new WaitForSeconds(preBuffer);

        // Recovery tail: accept a buffered press, then finish cooldown.
        float bufferWindow = Mathf.Min(inputBufferTime, remainingCooldown);
        float elapsed = 0f;
        while (elapsed < bufferWindow)
        {
            if (bufferedAttack)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        isAttacking = false;
        attackRoutine = null;

        if (bufferedAttack)
        {
            bufferedAttack = false;
            Attack();
        }
    }

    public int GetComboIndex() => comboIndex;
    public int GetComboCount() => currentCombo != null ? currentCombo.GetAttackCount() : 0;
    public Weapon GetCurrentWeapon() => currentWeapon;

    private void LogError(string message)
    {
        Debug.LogError($"[PlayerCombat] {message}", this);
    }
}
