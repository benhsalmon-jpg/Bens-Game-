using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any placed prefab that should participate in modular snapping.
/// Collects child SnapPoints and registers them with the global SnapRegistry.
/// </summary>
[DisallowMultipleComponent]
public class SnappableObject : MonoBehaviour
{
    [Tooltip("Optional bounds size used when auto-building edge snap points.")]
    public Vector3 footprint = new Vector3(1f, 0f, 1f);

    [Tooltip("If true and no SnapPoints exist, create temporary Left/Right/Front/Back points from footprint.")]
    public bool autoGenerateEdgePoints;

    [Tooltip("Snap type assigned to auto-generated edge points.")]
    public SnapType autoSnapType;

    private readonly List<SnapPoint> snapPoints = new List<SnapPoint>();
    private bool registered;

    public IReadOnlyList<SnapPoint> SnapPoints => snapPoints;

    private void Awake()
    {
        CacheSnapPoints();
        if (autoGenerateEdgePoints && snapPoints.Count == 0)
            GenerateEdgePoints();
    }

    private void OnEnable()
    {
        Register();
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        // Free connections so neighbors can be reused / rebuilt later.
        for (int i = 0; i < snapPoints.Count; i++)
        {
            SnapPoint point = snapPoints[i];
            if (point == null)
                continue;

            if (point.ConnectedTo != null)
                point.ConnectedTo.ClearConnection();

            point.ClearConnection();
        }

        Unregister();
    }

    public void CacheSnapPoints()
    {
        snapPoints.Clear();
        GetComponentsInChildren(true, snapPoints);
    }

    public void Register()
    {
        if (registered)
            return;

        CacheSnapPoints();
        SnapRegistry.Register(this);
        registered = true;
    }

    public void Unregister()
    {
        if (!registered)
            return;

        SnapRegistry.Unregister(this);
        registered = false;
    }

    /// <summary>
    /// After placement, pair the used sockets so they can't be double-used.
    /// </summary>
    public static void Connect(SnapPoint a, SnapPoint b)
    {
        if (a == null || b == null)
            return;

        a.MarkConnected(b);
        b.MarkConnected(a);
    }

    private void GenerateEdgePoints()
    {
        if (autoSnapType == null)
        {
            Debug.LogWarning($"SnappableObject on {name}: autoGenerateEdgePoints is on but autoSnapType is missing.", this);
            return;
        }

        CreateEdge("Snap_Right", new Vector3(footprint.x * 0.5f, 0f, 0f), Vector3.right, "Right", "Left");
        CreateEdge("Snap_Left", new Vector3(-footprint.x * 0.5f, 0f, 0f), Vector3.left, "Left", "Right");
        CreateEdge("Snap_Front", new Vector3(0f, 0f, footprint.z * 0.5f), Vector3.forward, "Front", "Back");
        CreateEdge("Snap_Back", new Vector3(0f, 0f, -footprint.z * 0.5f), Vector3.back, "Back", "Front");

        CacheSnapPoints();
    }

    private void CreateEdge(string pointName, Vector3 localPos, Vector3 outward, string tag, string requiredPartner)
    {
        GameObject go = new GameObject(pointName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

        SnapPoint point = go.AddComponent<SnapPoint>();
        point.snapType = autoSnapType;
        point.socketTag = tag;
        point.requiredPartnerTag = requiredPartner;
        point.alignRotation = true;
        point.yawOffset = 180f;
    }
}
