using UnityEngine;

/// <summary>
/// A single modular snap socket on a placeable (wall edge, floor corner, etc.).
/// Place these as child transforms on your prefab at the exact connect position.
/// Forward should point outward from the object (the direction a neighbor attaches from).
/// </summary>
public class SnapPoint : MonoBehaviour
{
    [Header("Identity")]
    public SnapType snapType;

    [Tooltip("Optional tag so only matching partners connect (e.g. Left/Right). Leave empty to ignore.")]
    public string socketTag = "";

    [Tooltip("Partner tag required on the other snap point. Empty = any.")]
    public string requiredPartnerTag = "";

    [Header("Rules")]
    [Tooltip("How close the ghost must be to prefer this snap over free placement.")]
    public float snapRadius = 1.25f;

    [Tooltip("If true, aligning to this point also copies its yaw (useful for walls).")]
    public bool alignRotation = true;

    [Tooltip("Extra yaw applied when snapping into this socket (degrees). Typically 180 for opposing edges.")]
    public float yawOffset = 180f;

    [Header("Occupancy")]
    [SerializeField] private bool occupied;
    [SerializeField] private SnapPoint connectedTo;

    private Vector3 cachedLocalOffsetFromRoot;
    private bool hasCachedOffset;

    public bool IsOccupied => occupied;
    public SnapPoint ConnectedTo => connectedTo;
    public Vector3 WorldPosition => transform.position;
    public Vector3 Outward => transform.forward;

    private void Awake()
    {
        CacheLocalOffset();
    }

    public void CacheLocalOffset()
    {
        Transform root = GetRootTransform();
        if (root == null)
        {
            cachedLocalOffsetFromRoot = transform.localPosition;
        }
        else
        {
            cachedLocalOffsetFromRoot = Quaternion.Inverse(root.rotation) * (transform.position - root.position);
        }

        hasCachedOffset = true;
    }

    public Vector3 GetLocalOffsetFromRoot()
    {
        if (!hasCachedOffset)
            CacheLocalOffset();

        return cachedLocalOffsetFromRoot;
    }

    /// <summary>
    /// World pose the incoming object should adopt so its partner snap lands on this point.
    /// </summary>
    public Pose GetIncomingPose(SnapPoint incomingPoint, Vector3 rotationOffset, float currentRotationY)
    {
        Quaternion targetRotation;
        if (alignRotation)
        {
            float yaw = transform.eulerAngles.y + yawOffset;
            targetRotation = Quaternion.Euler(rotationOffset.x, yaw + rotationOffset.y, rotationOffset.z);
        }
        else
        {
            targetRotation = Quaternion.Euler(rotationOffset.x, currentRotationY + rotationOffset.y, rotationOffset.z);
        }

        Vector3 localOffset = incomingPoint != null
            ? incomingPoint.GetLocalOffsetFromRoot()
            : Vector3.zero;

        Vector3 rootPosition = WorldPosition - (targetRotation * localOffset);
        return new Pose(rootPosition, targetRotation);
    }

    public Transform GetRootTransform()
    {
        SnappableObject snappable = GetComponentInParent<SnappableObject>();
        if (snappable != null)
            return snappable.transform;

        PlaceableObject placeable = GetComponentInParent<PlaceableObject>();
        if (placeable != null)
            return placeable.transform;

        return transform.root != transform ? transform.root : transform.parent;
    }

    public bool CanConnectTo(SnapPoint other)
    {
        if (other == null || other == this)
            return false;

        if (occupied || other.occupied)
            return false;

        if (snapType == null || other.snapType == null)
            return false;

        if (!snapType.IsCompatibleWith(other.snapType))
            return false;

        if (!string.IsNullOrEmpty(requiredPartnerTag) && other.socketTag != requiredPartnerTag)
            return false;

        if (!string.IsNullOrEmpty(other.requiredPartnerTag) && socketTag != other.requiredPartnerTag)
            return false;

        return true;
    }

    public void MarkConnected(SnapPoint other)
    {
        occupied = true;
        connectedTo = other;
    }

    public void ClearConnection()
    {
        occupied = false;
        connectedTo = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = occupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.08f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.35f);
    }
#endif
}
