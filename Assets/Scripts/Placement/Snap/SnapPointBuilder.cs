using UnityEngine;

/// <summary>
/// Optional helper: adds a standard set of opposing edge snap points under a prefab.
/// Useful when authoring walls/floors quickly in the editor or at runtime.
/// </summary>
public static class SnapPointBuilder
{
    public static void BuildBoxEdges(
        Transform root,
        SnapType type,
        Vector3 footprint,
        float snapRadius = 1.25f,
        bool includeFrontBack = true)
    {
        if (root == null || type == null)
            return;

        Create(root, "Snap_Right", new Vector3(footprint.x * 0.5f, 0f, 0f), Vector3.right, type, "Right", "Left", snapRadius);
        Create(root, "Snap_Left", new Vector3(-footprint.x * 0.5f, 0f, 0f), Vector3.left, type, "Left", "Right", snapRadius);

        if (!includeFrontBack)
            return;

        Create(root, "Snap_Front", new Vector3(0f, 0f, footprint.z * 0.5f), Vector3.forward, type, "Front", "Back", snapRadius);
        Create(root, "Snap_Back", new Vector3(0f, 0f, -footprint.z * 0.5f), Vector3.back, type, "Back", "Front", snapRadius);
    }

    private static void Create(
        Transform root,
        string pointName,
        Vector3 localPos,
        Vector3 outward,
        SnapType type,
        string tag,
        string requiredPartner,
        float snapRadius)
    {
        Transform existing = root.Find(pointName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(pointName);
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

        SnapPoint point = go.GetComponent<SnapPoint>();
        if (point == null)
            point = go.AddComponent<SnapPoint>();

        point.snapType = type;
        point.socketTag = tag;
        point.requiredPartnerTag = requiredPartner;
        point.snapRadius = snapRadius;
        point.alignRotation = true;
        point.yawOffset = 180f;
        point.CacheLocalOffset();
    }
}
