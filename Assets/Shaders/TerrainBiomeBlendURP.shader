Shader "Custom/TerrainBiomeBlendURP"
{
    // Lightweight URP-compatible unlit blend shader.
    // Prefer this when the project uses Universal Render Pipeline.
    // Vertex color packing matches TerrainGenerator / TERRAIN_SYSTEM.md.

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
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BiomeTex0); SAMPLER(sampler_BiomeTex0);
            TEXTURE2D(_BiomeTex1); SAMPLER(sampler_BiomeTex1);
            TEXTURE2D(_BiomeTex2); SAMPLER(sampler_BiomeTex2);
            TEXTURE2D(_BiomeTex3); SAMPLER(sampler_BiomeTex3);

            CBUFFER_START(UnityPerMaterial)
                float4 _BiomeColor0;
                float4 _BiomeColor1;
                float4 _BiomeColor2;
                float4 _BiomeColor3;
                float _Tiling;
                float _BiomeCount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 SampleBiome(int index, float2 uv)
            {
                float2 tiled = uv * _Tiling;
                if (index <= 0) return SAMPLE_TEXTURE2D(_BiomeTex0, sampler_BiomeTex0, tiled).rgb * _BiomeColor0.rgb;
                if (index == 1) return SAMPLE_TEXTURE2D(_BiomeTex1, sampler_BiomeTex1, tiled).rgb * _BiomeColor1.rgb;
                if (index == 2) return SAMPLE_TEXTURE2D(_BiomeTex2, sampler_BiomeTex2, tiled).rgb * _BiomeColor2.rgb;
                return SAMPLE_TEXTURE2D(_BiomeTex3, sampler_BiomeTex3, tiled).rgb * _BiomeColor3.rgb;
            }

            int DecodeBiomeIndex(float packed)
            {
                int idx = (int)floor(packed * 32.0 + 0.001);
                int maxIndex = (int)max(1.0, _BiomeCount) - 1;
                idx = clamp(idx, 0, maxIndex);
                return min(idx, 3);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.uv2 = input.uv2;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float w0 = saturate(input.color.r);
                float w1 = saturate(input.color.g);
                float w2 = saturate(input.uv2.x);
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

                int i0 = DecodeBiomeIndex(input.color.b);
                int i1 = DecodeBiomeIndex(input.color.a);
                int i2 = DecodeBiomeIndex(input.uv2.y);

                float3 c0 = SampleBiome(i0, input.uv);
                float3 c1 = SampleBiome(i1, input.uv);
                float3 c2 = SampleBiome(i2, input.uv);

                return half4(c0 * w0 + c1 * w1 + c2 * w2, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
