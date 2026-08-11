#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SnapSetupEditor
{
    [MenuItem("GameObject/Placement/Add Wall Edge Snap Points", false, 10)]
    private static void AddWallEdgeSnapPoints()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Snap Setup", "Select a wall prefab root first.", "OK");
            return;
        }

        SnapType type = FindOrPromptSnapType();
        if (type == null)
            return;

        SnappableObject snappable = selected.GetComponent<SnappableObject>();
        if (snappable == null)
            snappable = Undo.AddComponent<SnappableObject>(selected);

        Bounds bounds = CalculateRenderBounds(selected);
        Vector3 footprint = new Vector3(
            Mathf.Max(0.1f, bounds.size.x),
            0f,
            Mathf.Max(0.1f, bounds.size.z));

        Undo.RegisterFullObjectHierarchyUndo(selected, "Add Wall Edge Snap Points");
        SnapPointBuilder.BuildBoxEdges(selected.transform, type, footprint, includeFrontBack: false);
        snappable.CacheSnapPoints();

        EditorUtility.SetDirty(selected);
        Debug.Log($"Added Left/Right wall snap points to {selected.name} (footprint x={footprint.x:F2}).", selected);
    }

    [MenuItem("GameObject/Placement/Add Floor Edge Snap Points", false, 11)]
    private static void AddFloorEdgeSnapPoints()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Snap Setup", "Select a floor prefab root first.", "OK");
            return;
        }

        SnapType type = FindOrPromptSnapType();
        if (type == null)
            return;

        SnappableObject snappable = selected.GetComponent<SnappableObject>();
        if (snappable == null)
            snappable = Undo.AddComponent<SnappableObject>(selected);

        Bounds bounds = CalculateRenderBounds(selected);
        Vector3 footprint = new Vector3(
            Mathf.Max(0.1f, bounds.size.x),
            0f,
            Mathf.Max(0.1f, bounds.size.z));

        Undo.RegisterFullObjectHierarchyUndo(selected, "Add Floor Edge Snap Points");
        SnapPointBuilder.BuildBoxEdges(selected.transform, type, footprint, includeFrontBack: true);
        snappable.CacheSnapPoints();

        EditorUtility.SetDirty(selected);
        Debug.Log($"Added 4-edge floor snap points to {selected.name}.", selected);
    }

    private static SnapType FindOrPromptSnapType()
    {
        string[] guids = AssetDatabase.FindAssets("t:SnapType");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Snap Setup",
                "No SnapType assets found.\nCreate one via Create > Placement > Snap Type first.",
                "OK");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<SnapType>(path);
    }

    private static Bounds CalculateRenderBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // Convert to approximate local footprint size.
        Vector3 localSize = go.transform.InverseTransformVector(bounds.size);
        localSize.x = Mathf.Abs(localSize.x);
        localSize.y = Mathf.Abs(localSize.y);
        localSize.z = Mathf.Abs(localSize.z);
        return new Bounds(Vector3.zero, localSize);
    }
}
#endif
