using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene representation of a terrain chunk.
/// Holds references to mesh/collider/objects; pure data lives in TerrainChunkData.
/// </summary>
public class TerrainChunk : MonoBehaviour
{
    private TerrainChunkData data;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh renderMesh;
    private Mesh colliderMesh;
    private GameObject groundObject;
    private Transform objectsRoot;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>(32);
    private Dictionary<int, List<Matrix4x4>> grassMatrices;

    public TerrainChunkData Data => data;
    public Vector2Int Coordinate => data != null ? data.Coordinate : default;
    public Dictionary<int, List<Matrix4x4>> GrassMatrices => grassMatrices;
    public IReadOnlyList<GameObject> SpawnedObjects => spawnedObjects;

    public void BindData(TerrainChunkData chunkData)
    {
        data = chunkData;
    }

    public void EnsureHierarchy(int groundLayerId)
    {
        if (groundObject != null)
            return;

        groundObject = new GameObject("Ground");
        groundObject.transform.SetParent(transform, false);
        groundObject.transform.localPosition = Vector3.zero;
        groundObject.tag = "Ground";
        groundObject.layer = groundLayerId;

        meshFilter = groundObject.AddComponent<MeshFilter>();
        meshRenderer = groundObject.AddComponent<MeshRenderer>();
        meshCollider = groundObject.AddComponent<MeshCollider>();

        GameObject objects = new GameObject("Objects");
        objects.transform.SetParent(transform, false);
        objectsRoot = objects.transform;

        grassMatrices = new Dictionary<int, List<Matrix4x4>>(16);
    }

    public Transform ObjectsRoot => objectsRoot;

    public void ApplyRenderMesh(Mesh mesh, Material sharedMaterial)
    {
        if (renderMesh != null)
            Destroy(renderMesh);

        renderMesh = mesh;
        meshFilter.sharedMesh = renderMesh;
        meshRenderer.sharedMaterial = sharedMaterial;
    }

    public void ApplyColliderMesh(Mesh mesh)
    {
        if (colliderMesh != null && colliderMesh != renderMesh)
            Destroy(colliderMesh);

        colliderMesh = mesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = colliderMesh;
    }

    public void SetGrassMatrices(Dictionary<int, List<Matrix4x4>> matrices)
    {
        grassMatrices = matrices;
    }

    public void ClearGrassMatrices()
    {
        if (grassMatrices == null)
            return;

        foreach (var kvp in grassMatrices)
            kvp.Value.Clear();
        grassMatrices.Clear();
    }

    public void RegisterSpawnedObject(GameObject go)
    {
        if (go != null)
            spawnedObjects.Add(go);
    }

    public void ClearSpawnedObjects()
    {
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
                Destroy(spawnedObjects[i]);
        }

        spawnedObjects.Clear();
    }

    public void SetVisible(bool visible)
    {
        if (groundObject != null)
            groundObject.SetActive(visible);

        if (objectsRoot != null)
            objectsRoot.gameObject.SetActive(visible);

        if (data != null)
        {
            if (visible)
                data.MarkVisible();
            else
                data.MarkHidden();
        }
    }

    private void OnDestroy()
    {
        ClearSpawnedObjects();

        if (renderMesh != null)
            Destroy(renderMesh);

        if (colliderMesh != null && colliderMesh != renderMesh)
            Destroy(colliderMesh);
    }
}
