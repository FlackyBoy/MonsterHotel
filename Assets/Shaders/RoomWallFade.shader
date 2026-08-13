Shader "MonsterHotel/RoomWallFade"
{
    Properties
    {
        _BaseColor    ("Color",  Color)         = (1,1,1,1)
        [NoScaleOffset] _BaseMap ("Albedo (triplanaire — projetée depuis le monde, pas les UV du mesh — voir Texture World Scale ci-dessous, pas Tiling)", 2D) = "white" {}
        _TexScale     ("Texture World Scale", Float) = 1
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 32)) = 4
        _FadeStrength ("Fade Strength", Range(0,1)) = 0
        _FadeRadius   ("Fade Radius",   Float)  = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward ───────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Positions des joueurs — mises à jour globalement chaque frame (Shader.SetGlobalVector)
            float4 _PlayerPos0;
            float4 _PlayerPos1;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _TexScale;
                float  _TriplanarSharpness;
                float  _FadeStrength;
                float  _FadeRadius;
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
                half4 col  = SampleTriplanar(IN.positionWS, normalize(IN.normalWS)) * _BaseColor;
                Light main = GetMainLight();
                float ndl  = saturate(dot(normalize(IN.normalWS), main.direction));
                col.rgb   *= main.color * (ndl * 0.6 + 0.4);

                // Distance au joueur le plus proche (plan XZ uniquement)
                float2 fragXZ  = IN.positionWS.xz;
                float  dist0   = distance(fragXZ, _PlayerPos0.xz);
                float  dist1   = distance(fragXZ, _PlayerPos1.xz);
                float  minDist = min(dist0, dist1);

                // Fondu radial : 0 près du joueur (transparent) → 1 loin (opaque)
                float radial = smoothstep(0.0, _FadeRadius, minDist);

                // Alpha final : _FadeStrength contrôle l'intensité globale
                col.a = lerp(1.0, radial, _FadeStrength);

                return col;
            }
            ENDHLSL
        }

        // ── Shadow Caster — s'efface quand le fondu est actif ─────
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

            float4 _PlayerPos0;
            float4 _PlayerPos1;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _FadeStrength;
                float  _FadeRadius;
            CBUFFER_END

            struct AttrS  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VaryS  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            VaryS vertShadow(AttrS IN)
            {
                VaryS OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 lightDir = normalize(_MainLightPosition.xyz);
                posWS = ApplyShadowBias(posWS, normWS, lightDir);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                return OUT;
            }

            half4 fragShadow(VaryS IN) : SV_Target
            {
                // Supprime l'ombre autour du joueur quand le fondu est actif
                float2 fragXZ  = IN.positionWS.xz;
                float  dist0   = distance(fragXZ, _PlayerPos0.xz);
                float  dist1   = distance(fragXZ, _PlayerPos1.xz);
                float  minDist = min(dist0, dist1);
                float  radial  = smoothstep(0.0, _FadeRadius, minDist);
                clip(lerp(1.0, radial, _FadeStrength) - 0.1);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
