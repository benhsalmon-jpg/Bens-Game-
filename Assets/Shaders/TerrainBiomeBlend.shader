Shader "Custom/TerrainBiomeBlend"
{
    Properties
    {
        _BiomeTex0 ("Biome Texture 0", 2D) = "white" {}
        _BiomeTex1 ("Biome Texture 1", 2D) = "white" {}
        _BiomeTex2 ("Biome Texture 2", 2D) = "white" {}
        _BiomeTex3 ("Biome Texture 3", 2D) = "white" {}

        _BiomeColor0 ("Biome Color 0", Color) = (0.30, 0.65, 0.20, 1)
        _BiomeColor1 ("Biome Color 1", Color) = (0.15, 0.35, 0.12, 1)
        _BiomeColor2 ("Biome Color 2", Color) = (0.45, 0.45, 0.45, 1)
        _BiomeColor3 ("Biome Color 3", Color) = (0.35, 0.40, 0.20, 1)

        _Tiling ("UV Tiling", Float) = 2.0
        _BiomeCount ("Biome Count", Float) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _BiomeTex0;
        sampler2D _BiomeTex1;
        sampler2D _BiomeTex2;
        sampler2D _BiomeTex3;

        fixed4 _BiomeColor0;
        fixed4 _BiomeColor1;
        fixed4 _BiomeColor2;
        fixed4 _BiomeColor3;

        float _Tiling;
        float _BiomeCount;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_BiomeTex0;
            float2 uv2_BiomeTex0;
            float4 color : COLOR;
        };

        // Vertex color packing (from TerrainGenerator):
        //   R = primary biome weight
        //   G = secondary biome weight
        //   B = primary biome index / 32
        //   A = secondary biome index / 32
        // UV2:
        //   X = tertiary weight
        //   Y = tertiary biome index / 32

        fixed3 SampleBiome(int index, float2 uv)
        {
            float2 tiled = uv * _Tiling;
            if (index <= 0) return tex2D(_BiomeTex0, tiled).rgb * _BiomeColor0.rgb;
            if (index == 1) return tex2D(_BiomeTex1, tiled).rgb * _BiomeColor1.rgb;
            if (index == 2) return tex2D(_BiomeTex2, tiled).rgb * _BiomeColor2.rgb;
            return tex2D(_BiomeTex3, tiled).rgb * _BiomeColor3.rgb;
        }

        int DecodeBiomeIndex(float packed)
        {
            int idx = (int)floor(packed * 32.0 + 0.001);
            int maxIndex = (int)max(1.0, _BiomeCount) - 1;
            if (idx < 0) idx = 0;
            if (idx > maxIndex) idx = maxIndex;
            if (idx > 3) idx = 3;
            return idx;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float w0 = saturate(IN.color.r);
            float w1 = saturate(IN.color.g);
            float w2 = saturate(IN.uv2_BiomeTex0.x);

            // Ensure weights sum to 1.
            float sum = w0 + w1 + w2;
            if (sum < 1e-4)
            {
                w0 = 1.0;
                w1 = 0.0;
                w2 = 0.0;
                sum = 1.0;
            }
            w0 /= sum;
            w1 /= sum;
            w2 /= sum;

            int i0 = DecodeBiomeIndex(IN.color.b);
            int i1 = DecodeBiomeIndex(IN.color.a);
            int i2 = DecodeBiomeIndex(IN.uv2_BiomeTex0.y);

            fixed3 c0 = SampleBiome(i0, IN.uv_BiomeTex0);
            fixed3 c1 = SampleBiome(i1, IN.uv_BiomeTex0);
            fixed3 c2 = SampleBiome(i2, IN.uv_BiomeTex0);

            o.Albedo = c0 * w0 + c1 * w1 + c2 * w2;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
