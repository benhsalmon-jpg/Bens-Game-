using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ordered list of attacks for one weapon type's combo.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Combo Sequence")]
public class ComboSequence : ScriptableObject
{
    [SerializeField] private List<AttackSO> attacks = new List<AttackSO>();
    [SerializeField, Min(0f)] private float comboResetTime = 1.5f;
    [SerializeField, Min(0f)] private float clickInputWindow = 0.3f;

    public float ComboResetTime => comboResetTime;
    public float ClickInputWindow => clickInputWindow;

    public int GetAttackCount() => attacks.Count;

    public AttackSO GetAttack(int index)
    {
        if (index < 0 || index >= attacks.Count)
            return null;
        return attacks[index];
    }
}
