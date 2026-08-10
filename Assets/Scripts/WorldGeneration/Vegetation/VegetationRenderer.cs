using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU-instanced vegetation renderer.
/// Uses Graphics.DrawMeshInstanced (compatible with Built-in RP and URP).
/// Grass is NEVER spawned as individual GameObjects.
/// </summary>
public class VegetationRenderer : MonoBehaviour
{
    private const int MaxInstancesPerBatch = 1023;

    private Biome[] biomes;
    private readonly Dictionary<int, List<Matrix4x4>> matricesByKey = new Dictionary<int, List<Matrix4x4>>(32);
    private readonly Matrix4x4[] batchBuffer = new Matrix4x4[MaxInstancesPerBatch];
    private MaterialPropertyBlock propertyBlock;
    private bool enabledRendering = true;
    private ShadowCastingMode shadowMode = ShadowCastingMode.Off;
    private bool receiveShadows;

    public int LastDrawnInstanceCount { get; private set; }

    public void Initialize(Biome[] biomeDefs)
    {
        biomes = biomeDefs;
        propertyBlock = new MaterialPropertyBlock();
    }

    public void SetShadowMode(ShadowCastingMode mode, bool receive)
    {
        shadowMode = mode;
        receiveShadows = receive;
    }

    public void SetEnabled(bool enabled)
    {
        enabledRendering = enabled;
    }

    /// <summary>
    /// Replace this chunk's contribution. Caller owns lifetime of matricesByKey contents.
    /// </summary>
    public Dictionary<int, List<Matrix4x4>> CreateMatrixStore()
    {
        return new Dictionary<int, List<Matrix4x4>>(16);
    }

    public void DrawChunkMatrices(Dictionary<int, List<Matrix4x4>> matricesByGrassKey)
    {
        if (!enabledRendering || matricesByGrassKey == null || biomes == null)
            return;

        int drawn = 0;
        foreach (KeyValuePair<int, List<Matrix4x4>> kvp in matricesByGrassKey)
        {
            VegetationSystem.UnpackGrassKey(kvp.Key, out int biomeIndex, out int grassTypeIndex);
            if (biomeIndex < 0 || biomeIndex >= biomes.Length)
                continue;

            Biome biome = biomes[biomeIndex];
            if (biome?.vegetation?.grassTypes == null)
                continue;

            if (grassTypeIndex < 0 || grassTypeIndex >= biome.vegetation.grassTypes.Length)
                continue;

            GrassType grass = biome.vegetation.grassTypes[grassTypeIndex];
            if (grass == null || grass.mesh == null || grass.material == null)
                continue;

            List<Matrix4x4> matrices = kvp.Value;
            int count = matrices.Count;
            int offset = 0;
            while (offset < count)
            {
                int batchCount = Mathf.Min(MaxInstancesPerBatch, count - offset);
                for (int i = 0; i < batchCount; i++)
                    batchBuffer[i] = matrices[offset + i];

                Graphics.DrawMeshInstanced(
                    grass.mesh,
                    0,
                    grass.material,
                    batchBuffer,
                    batchCount,
                    propertyBlock,
                    shadowMode,
                    receiveShadows);

                drawn += batchCount;
                offset += batchCount;
            }
        }

        LastDrawnInstanceCount += drawn;
    }

    public void BeginFrame()
    {
        LastDrawnInstanceCount = 0;
    }

    /// <summary>
    /// Builds a simple default quad/blade mesh when designer has not assigned one.
    /// </summary>
    public static Mesh CreateDefaultGrassBladeMesh()
    {
        Mesh mesh = new Mesh { name = "DefaultGrassBlade" };
        Vector3[] vertices =
        {
            new Vector3(-0.05f, 0f, 0f),
            new Vector3(0.05f, 0f, 0f),
            new Vector3(-0.04f, 0.35f, 0f),
            new Vector3(0.04f, 0.45f, 0f),
            new Vector3(0f, 0.7f, 0f)
        };
        int[] triangles =
        {
            0, 2, 1,
            1, 2, 3,
            2, 4, 3
        };
        Vector2[] uv =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.65f),
            new Vector2(0.5f, 1f)
        };
        Color[] colors =
        {
            new Color(0.25f, 0.55f, 0.15f, 1f),
            new Color(0.25f, 0.55f, 0.15f, 1f),
            new Color(0.35f, 0.7f, 0.2f, 1f),
            new Color(0.35f, 0.7f, 0.2f, 1f),
            new Color(0.45f, 0.85f, 0.25f, 1f)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Material CreateDefaultGrassMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader) { name = "DefaultGrass", color = color, enableInstancing = true };
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        return mat;
    }
}
