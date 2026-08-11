using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor/runtime helper to build a translucent ghost material.
/// In the editor, use the context menu or Assets > Create > Placement > Ghost Material.
/// </summary>
public static class GhostMaterialFactory
{
    public enum GhostKind
    {
        Valid,
        Invalid,
        Snapped
    }

    public static Material Create(GhostKind kind, Shader shader = null)
    {
        if (shader == null)
            shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");

        Material mat = new Material(shader);
        Color color = kind switch
        {
            GhostKind.Valid => new Color(0.25f, 0.95f, 0.45f, 0.35f),
            GhostKind.Snapped => new Color(0.25f, 0.75f, 1f, 0.45f),
            _ => new Color(0.95f, 0.25f, 0.25f, 0.35f)
        };

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        // Transparent surface setup for Built-in / URP-ish shaders.
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        mat.name = kind switch
        {
            GhostKind.Valid => "Ghost_Valid",
            GhostKind.Snapped => "Ghost_Snapped",
            _ => "Ghost_Invalid"
        };

        return mat;
    }

#if UNITY_EDITOR
    [MenuItem("Assets/Create/Placement/Ghost Materials (Valid/Invalid/Snapped)")]
    private static void CreateGhostMaterialAssets()
    {
        string folder = "Assets/Materials/Ghosts";
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Materials", "Ghosts");

        Save(Create(GhostKind.Valid), $"{folder}/Ghost_Valid.mat");
        Save(Create(GhostKind.Invalid), $"{folder}/Ghost_Invalid.mat");
        Save(Create(GhostKind.Snapped), $"{folder}/Ghost_Snapped.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created Valid / Invalid / Snapped ghost materials in " + folder);
    }

    private static void Save(Material mat, string path)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mat, existing);
            Object.DestroyImmediate(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, path);
        }
    }
#endif
}
