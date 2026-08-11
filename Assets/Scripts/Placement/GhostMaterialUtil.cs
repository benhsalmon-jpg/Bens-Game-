using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared helper so valid / invalid / snapped ghost materials stay consistent.
/// </summary>
public static class GhostMaterialUtil
{
    public static Material CreateGhostInstance(Material source)
    {
        if (source == null)
            return null;

        Material ghostMat = new Material(source);
        ghostMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        ghostMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        ghostMat.SetInt("_ZWrite", 0);
        ghostMat.renderQueue = 3000;
        return ghostMat;
    }

    public static void ApplyToRenderers(Renderer[] renderers, Material source)
    {
        if (renderers == null || source == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            Material ghostMat = CreateGhostInstance(source);
            r.material = ghostMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
