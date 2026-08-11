using UnityEngine;

/// <summary>
/// Stub kept for Weapon restriction checks.
/// Replace with your existing DestroyOnHit if you already have one.
/// </summary>
public class DestroyOnHit : MonoBehaviour
{
    [SerializeField] private WeaponType[] allowedWeapons;

    public bool CanBeDamagedBy(GameObject weaponObject)
    {
        if (allowedWeapons == null || allowedWeapons.Length == 0)
            return true;

        Weapon weapon = weaponObject.GetComponentInParent<Weapon>();
        if (weapon == null)
            return false;

        for (int i = 0; i < allowedWeapons.Length; i++)
        {
            if (allowedWeapons[i] == weapon.Type)
                return true;
        }

        return false;
    }
}
