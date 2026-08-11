using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Preview + place held buildables with grid snap, modular socket snap, and ghost feedback.
/// </summary>
public class PlaceableObject : MonoBehaviour
{
    public enum GhostVisualState
    {
        Invalid,
        Valid,
        Snapped
    }

    [Header("Placement Settings")]
    public GameObject placedPrefab;
    public float maxPlaceDistance = 5f;
    public LayerMask groundMask;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Grid Snapping")]
    public float snapGridSize = 0.5f;
    [FormerlySerializedAs("useSnapping")]
    public bool useGridSnapping = true;
    public float groundOffset = 0f;

    [Header("Modular Socket Snapping")]
    [Tooltip("Prefer snapping ghost to nearby SnapPoints on already-placed objects.")]
    public bool useModularSnapping = true;
    public float modularSnapSearchRadius = 2.5f;

    [Header("Ghost Materials")]
    public Material validGhostMat;
    public Material invalidGhostMat;
    [Tooltip("Shown when the ghost is locked to a modular snap socket.")]
    public Material snappedGhostMat;

    private GameObject ghostObject;
    private Renderer[] ghostRenderers;
    private readonly List<SnapPoint> ghostSnapPoints = new List<SnapPoint>();

    private bool canPlace;
    private bool isSnapped;
    private float currentRotationY;
    private GhostVisualState currentVisualState = GhostVisualState.Invalid;

    private SnapPoint pendingTargetSnap;
    private SnapPoint pendingGhostSnap;

    private void Start()
    {
        CreateGhostObject();
    }

    private void OnDisable()
    {
        // Hotbar swaps destroy / disable the hand item — clean the ghost immediately
        // so materials / objects never linger in the world.
        CleanupGhost();
    }

    private void OnDestroy()
    {
        CleanupGhost();
    }

    private void Update()
    {
        if (ghostObject == null)
            return;

        UpdateGhostPositionAndValidity();
        HandleRotationInput();

        if (Input.GetMouseButtonDown(0))
            TryPlaceObject();
    }

    private void HandleRotationInput()
    {
        // Manual rotate is ignored while modular-snapped (pose comes from the socket).
        if (isSnapped)
            return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationY += 45f;
            if (currentRotationY >= 360f)
                currentRotationY -= 360f;
        }
    }

    private void CreateGhostObject()
    {
        CleanupGhost();

        // Prefer the world prefab so snap sockets match what will actually be placed
        // (hand visuals often differ from the build piece).
        GameObject ghostSource = placedPrefab != null ? placedPrefab : gameObject;
        ghostObject = Instantiate(ghostSource);
        ghostObject.name = "Ghost_" + ghostSource.name;

        // Strip gameplay components from the preview copy.
        PlaceableObject placeable = ghostObject.GetComponent<PlaceableObject>();
        if (placeable != null)
            DestroyImmediate(placeable);

        // Ghost must not register as a real snappable target.
        SnappableObject snappable = ghostObject.GetComponent<SnappableObject>();
        if (snappable != null)
            DestroyImmediate(snappable);

        // Also strip any nested PlaceableObject / Item scripts that may live on prefabs.
        MonoBehaviour[] behaviours = ghostObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is SnapPoint)
                continue;

            // Keep SnapPoints only; remove other gameplay scripts from the preview.
            if (behaviour.GetType().Name == "Item" || behaviour is PlaceableObject || behaviour is SnappableObject)
                DestroyImmediate(behaviour);
        }

        Collider[] colliders = ghostObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = ghostObject.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                DestroyImmediate(bodies[i]);
        }

        ghostRenderers = ghostObject.GetComponentsInChildren<Renderer>();
        GhostMaterialUtil.ApplyToRenderers(ghostRenderers, invalidGhostMat);
        currentVisualState = GhostVisualState.Invalid;

        ghostSnapPoints.Clear();
        ghostObject.GetComponentsInChildren(true, ghostSnapPoints);
        for (int i = 0; i < ghostSnapPoints.Count; i++)
        {
            if (ghostSnapPoints[i] != null)
                ghostSnapPoints[i].CacheLocalOffset();
        }
    }

    private void UpdateGhostPositionAndValidity()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        pendingTargetSnap = null;
        pendingGhostSnap = null;
        isSnapped = false;

        Ray forwardRay = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(forwardRay, out RaycastHit forwardHit, maxPlaceDistance))
        {
            Vector3 downRayStart = forwardHit.point + Vector3.up * 3f;

            if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit downHit, 10f, groundMask))
            {
                Vector3 placementPosition = downHit.point + Vector3.up * groundOffset;

                if (useGridSnapping)
                    placementPosition = SnapToGrid(placementPosition);

                Quaternion placementRotation = Quaternion.Euler(
                    rotationOffset.x,
                    currentRotationY + rotationOffset.y,
                    rotationOffset.z);

                // Modular socket snap overrides free/grid pose when a valid neighbor is nearby.
                if (useModularSnapping && ghostSnapPoints.Count > 0)
                {
                    SnapResolver.Result snap = SnapResolver.TryResolve(
                        placementPosition,
                        ghostSnapPoints,
                        rotationOffset,
                        currentRotationY,
                        modularSnapSearchRadius);

                    if (snap.success)
                    {
                        placementPosition = snap.pose.position;
                        placementRotation = snap.pose.rotation;
                        isSnapped = true;
                        pendingTargetSnap = snap.targetPoint;
                        pendingGhostSnap = snap.ghostPoint;
                    }
                }

                ghostObject.transform.SetPositionAndRotation(placementPosition, placementRotation);

                canPlace = isSnapped || ValidatePlacement(downHit);
                UpdateGhostMaterial(canPlace, isSnapped);
                return;
            }
        }

        canPlace = false;
        UpdateGhostMaterial(false, false);
        ghostObject.transform.position = cam.transform.position + cam.transform.forward * (maxPlaceDistance * 0.5f);
        ghostObject.transform.rotation = Quaternion.Euler(
            rotationOffset.x,
            currentRotationY + rotationOffset.y,
            rotationOffset.z);
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        if (snapGridSize <= 0.0001f)
            return position;

        return new Vector3(
            Mathf.Round(position.x / snapGridSize) * snapGridSize,
            Mathf.Round(position.y / snapGridSize) * snapGridSize,
            Mathf.Round(position.z / snapGridSize) * snapGridSize);
    }

    private bool ValidatePlacement(RaycastHit groundHit)
    {
        if (groundHit.distance > 5f)
            return false;

        if (Vector3.Angle(groundHit.normal, Vector3.up) > 40f)
            return false;

        return true;
    }

    private void UpdateGhostMaterial(bool placeable, bool snapped)
    {
        GhostVisualState desired;
        if (!placeable)
            desired = GhostVisualState.Invalid;
        else if (snapped)
            desired = GhostVisualState.Snapped;
        else
            desired = GhostVisualState.Valid;

        if (desired == currentVisualState)
            return;

        currentVisualState = desired;

        Material targetMat = desired switch
        {
            GhostVisualState.Snapped => snappedGhostMat != null ? snappedGhostMat : validGhostMat,
            GhostVisualState.Valid => validGhostMat,
            _ => invalidGhostMat
        };

        GhostMaterialUtil.ApplyToRenderers(ghostRenderers, targetMat);
    }

    private void TryPlaceObject()
    {
        if (!canPlace)
            return;

        if (Inventory.instance == null || !Inventory.instance.HasEquippedItem())
        {
            Debug.Log("No item equipped!");
            return;
        }

        if (placedPrefab == null)
        {
            Debug.LogWarning("PlaceableObject: placedPrefab is not assigned.", this);
            return;
        }

        Quaternion finalRotation = ghostObject.transform.rotation;
        GameObject placed = Instantiate(placedPrefab, ghostObject.transform.position, finalRotation);

        // Ensure placed object participates in future modular snaps.
        SnappableObject snappable = placed.GetComponent<SnappableObject>();
        if (snappable == null)
            snappable = placed.AddComponent<SnappableObject>();

        snappable.CacheSnapPoints();
        snappable.Register();

        // Pair sockets so the used edge is occupied and the new wall exposes free neighbors.
        if (isSnapped && pendingTargetSnap != null)
        {
            SnapPoint placedGhostPartner = FindMatchingSnapPoint(snappable, pendingGhostSnap);
            if (placedGhostPartner != null)
                SnappableObject.Connect(pendingTargetSnap, placedGhostPartner);
        }

        // ConsumeItem refreshes the hand item, which destroys this PlaceableObject.
        // Ghost cleanup runs from OnDisable / OnDestroy — do not touch this object after.
        Inventory.instance.ConsumeItem();
    }

    private static SnapPoint FindMatchingSnapPoint(SnappableObject snappable, SnapPoint template)
    {
        if (snappable == null || template == null)
            return null;

        IReadOnlyList<SnapPoint> points = snappable.SnapPoints;
        for (int i = 0; i < points.Count; i++)
        {
            SnapPoint point = points[i];
            if (point == null)
                continue;

            if (point.snapType == template.snapType &&
                point.socketTag == template.socketTag &&
                point.requiredPartnerTag == template.requiredPartnerTag)
            {
                return point;
            }
        }

        // Fallback: closest matching type by local offset.
        Vector3 templateOffset = template.GetLocalOffsetFromRoot();
        SnapPoint best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            SnapPoint point = points[i];
            if (point == null || point.snapType != template.snapType)
                continue;

            float dist = (point.GetLocalOffsetFromRoot() - templateOffset).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = point;
            }
        }

        return best;
    }

    private void CleanupGhost()
    {
        if (ghostObject == null)
            return;

        Destroy(ghostObject);
        ghostObject = null;
        ghostRenderers = null;
        ghostSnapPoints.Clear();
        pendingTargetSnap = null;
        pendingGhostSnap = null;
        isSnapped = false;
        canPlace = false;
        currentVisualState = GhostVisualState.Invalid;
    }

    /// <summary>
    /// Called by Inventory when the equipped hotbar slot changes so placement mode tears down cleanly.
    /// Only clears the ghost; Inventory owns destroying the hand item itself.
    /// </summary>
    public void CancelPlacement()
    {
        CleanupGhost();
    }
}
