Shader "MonsterHotel/RoomWallDither"
{
    Properties
    {
        _BaseColor   ("Color",  Color)         = (1,1,1,1)
        [NoScaleOffset] _BaseMap ("Albedo (triplanaire — projetée depuis le monde, pas les UV du mesh — voir Texture World Scale ci-dessous, pas Tiling)", 2D) = "white" {}
        _TexScale    ("Texture World Scale", Float) = 1
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 32)) = 4
        _DitherAlpha ("Dither Alpha", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward ───────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _TexScale;
                float  _TriplanarSharpness;
                float  _DitherAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            // Matrice de Bayer 4x4 — retourne un seuil [0,1]
            float Bayer4x4(float2 screenPos)
            {
                uint2 p   = uint2(fmod(abs(floor(screenPos)), 4.0));
                uint  idx = p.y * 4u + p.x;
                uint  v   =
                    idx ==  0u ?  0u : idx ==  1u ?  8u :
                    idx ==  2u ?  2u : idx ==  3u ? 10u :
                    idx ==  4u ? 12u : idx ==  5u ?  4u :
                    idx ==  6u ? 14u : idx ==  7u ?  6u :
                    idx ==  8u ?  3u : idx ==  9u ? 11u :
                    idx == 10u ?  1u : idx == 11u ?  9u :
                    idx == 12u ? 15u : idx == 13u ?  7u :
                    idx == 14u ? 13u : 5u;
                return (float)v / 16.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // Projette _BaseMap depuis 3 axes du monde et mélange selon la normale — évite les
            // textures qui paraissent retournées/mal orientées d'un mur à l'autre avec les
            // primitives Cube de RoomPlaceholder (leurs UV sont fixés par axe local, pas le monde).
            half4 SampleTriplanar(float3 positionWS, float3 normalWS)
            {
                float3 blend = pow(abs(normalWS), _TriplanarSharpness);
                blend /= (blend.x + blend.y + blend.z + 1e-5);

                half4 texX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.zy * _TexScale);
                half4 texY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * _TexScale);
                half4 texZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xy * _TexScale);

                return texX * blend.x + texY * blend.y + texZ * blend.z;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Dithering : clip le pixel si DitherAlpha dépasse le seuil Bayer
                clip(Bayer4x4(IN.positionCS.xy) - _DitherAlpha);

                half4 col  = SampleTriplanar(IN.positionWS, normalize(IN.normalWS)) * _BaseColor;
                Light main = GetMainLight();
                float ndl  = saturate(dot(normalize(IN.normalWS), main.direction));
                col.rgb   *= main.color * (ndl * 0.6 + 0.4);
                return col;
            }
            ENDHLSL
        }

        // ── Shadow Caster ─────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _DitherAlpha;
            CBUFFER_END

            float Bayer4x4_Shadow(float2 p)
            {
                uint2 pi  = uint2(fmod(abs(floor(p)), 4.0));
                uint  idx = pi.y * 4u + pi.x;
                uint  v   =
                    idx ==  0u ?  0u : idx ==  1u ?  8u :
                    idx ==  2u ?  2u : idx ==  3u ? 10u :
                    idx ==  4u ? 12u : idx ==  5u ?  4u :
                    idx ==  6u ? 14u : idx ==  7u ?  6u :
                    idx ==  8u ?  3u : idx ==  9u ? 11u :
                    idx == 10u ?  1u : idx == 11u ?  9u :
                    idx == 12u ? 15u : idx == 13u ?  7u :
                    idx == 14u ? 13u : 5u;
                return (float)v / 16.0;
            }

            struct AttrS { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VaryS { float4 positionCS : SV_POSITION; };

            VaryS vertShadow(AttrS IN)
            {
                VaryS OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 lightDir = normalize(_MainLightPosition.xyz);
                posWS = ApplyShadowBias(posWS, normWS, lightDir);
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 fragShadow(VaryS IN) : SV_Target
            {
                clip(Bayer4x4_Shadow(IN.positionCS.xy) - _DitherAlpha);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
