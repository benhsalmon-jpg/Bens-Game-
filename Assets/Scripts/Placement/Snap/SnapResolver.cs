using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves the best modular snap pose for a ghost object given free-placement position.
/// </summary>
public static class SnapResolver
{
    private static readonly List<SnapCandidate> candidates = new List<SnapCandidate>(32);

    public struct Result
    {
        public bool success;
        public Pose pose;
        public SnapPoint targetPoint;
        public SnapPoint ghostPoint;
    }

    public static Result TryResolve(
        Vector3 freePlacementPosition,
        IReadOnlyList<SnapPoint> ghostPoints,
        Vector3 rotationOffset,
        float currentRotationY,
        float searchRadius)
    {
        Result result = default;
        if (ghostPoints == null || ghostPoints.Count == 0)
            return result;

        SnapRegistry.GetCandidatesNear(freePlacementPosition, searchRadius, ghostPoints, candidates);
        if (candidates.Count == 0)
            return result;

        SnapCandidate best = candidates[0];
        Pose pose = best.targetPoint.GetIncomingPose(best.ghostPoint, rotationOffset, currentRotationY);

        result.success = true;
        result.pose = pose;
        result.targetPoint = best.targetPoint;
        result.ghostPoint = best.ghostPoint;
        return result;
    }
}
