using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a category of snap connection (e.g. WallEdge, FloorTile, FencePost).
/// Compatibility is data-driven so new placeables can snap without code changes.
/// </summary>
[CreateAssetMenu(fileName = "SnapType_", menuName = "Placement/Snap Type", order = 0)]
public class SnapType : ScriptableObject
{
    [Tooltip("Unique id used for debugging and matching.")]
    public string typeId = "WallEdge";

    [Tooltip("Snap types this one can connect to. Leave empty to only match itself.")]
    public List<SnapType> compatibleWith = new List<SnapType>();

    [Tooltip("If true, this type is always compatible with itself.")]
    public bool selfCompatible = true;

    public bool IsCompatibleWith(SnapType other)
    {
        if (other == null)
            return false;

        if (selfCompatible && other == this)
            return true;

        if (compatibleWith == null)
            return false;

        for (int i = 0; i < compatibleWith.Count; i++)
        {
            if (compatibleWith[i] == other)
                return true;
        }

        return false;
    }
}
