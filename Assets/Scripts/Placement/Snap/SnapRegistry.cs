using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight global registry of all live snappable objects / snap points.
/// Avoids expensive scene-wide searches every frame during placement.
/// </summary>
public static class SnapRegistry
{
    private static readonly HashSet<SnappableObject> snappables = new HashSet<SnappableObject>();
    private static readonly List<SnapPoint> pointBuffer = new List<SnapPoint>(128);

    public static void Register(SnappableObject snappable)
    {
        if (snappable != null)
            snappables.Add(snappable);
    }

    public static void Unregister(SnappableObject snappable)
    {
        if (snappable != null)
            snappables.Remove(snappable);
    }

    public static void Clear()
    {
        snappables.Clear();
    }

    /// <summary>
    /// Collect free snap points near a world position that are compatible with any of the ghost's points.
    /// </summary>
    public static void GetCandidatesNear(
        Vector3 worldPosition,
        float searchRadius,
        IReadOnlyList<SnapPoint> ghostPoints,
        List<SnapCandidate> results)
    {
        results.Clear();
        if (ghostPoints == null || ghostPoints.Count == 0)
            return;

        float searchRadiusSqr = searchRadius * searchRadius;

        foreach (SnappableObject snappable in snappables)
        {
            if (snappable == null)
                continue;

            IReadOnlyList<SnapPoint> points = snappable.SnapPoints;
            for (int i = 0; i < points.Count; i++)
            {
                SnapPoint target = points[i];
                if (target == null || target.IsOccupied)
                    continue;

                Vector3 delta = target.WorldPosition - worldPosition;
                float distSqr = delta.sqrMagnitude;
                if (distSqr > searchRadiusSqr)
                    continue;

                for (int g = 0; g < ghostPoints.Count; g++)
                {
                    SnapPoint ghostPoint = ghostPoints[g];
                    if (ghostPoint == null)
                        continue;

                    if (!target.CanConnectTo(ghostPoint))
                        continue;

                    float scoreRadius = Mathf.Max(0.01f, target.snapRadius);
                    if (distSqr > scoreRadius * scoreRadius)
                        continue;

                    results.Add(new SnapCandidate
                    {
                        targetPoint = target,
                        ghostPoint = ghostPoint,
                        distanceSqr = distSqr
                    });
                }
            }
        }

        results.Sort(static (a, b) => a.distanceSqr.CompareTo(b.distanceSqr));
    }

    public static int Count => snappables.Count;
}

public struct SnapCandidate
{
    public SnapPoint targetPoint;
    public SnapPoint ghostPoint;
    public float distanceSqr;
}
