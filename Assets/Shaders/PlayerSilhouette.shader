Shader "MonsterHotel/PlayerSilhouette"
{
    Properties
    {
        _SilhouetteColor ("Silhouette Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Silhouette. Compare la profondeur du joueur à "_EnvironmentDepthTex" (voir
        // PlayerOccluderDepthFeature.cs) — une texture de profondeur qui ne contient QUE
        // l'environnement (blocs, murs, monstres...), jamais le joueur lui-même. Si l'environnement
        // est plus proche de la caméra que ce fragment à ce pixel : le joueur est occulté par autre
        // chose que lui-même → on dessine la silhouette. ZTest Always (le filtrage se fait
        // entièrement à la main contre cette texture dédiée) — c'est ce qui évite l'auto-occultation
        // (silhouette visible sur le joueur lui-même, bras devant le torse) sans les pièges d'un
        // stencil partagé entre deux passes du même mesh.
        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EnvironmentDepthTex);
            SAMPLER(sampler_EnvironmentDepthTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _SilhouetteColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;

                float envRawDepth = SAMPLE_TEXTURE2D(_EnvironmentDepthTex, sampler_EnvironmentDepthTex, uv).r;
                float envEyeDepth = LinearEyeDepth(envRawDepth, _ZBufferParams);

                // Distance caméra→ce fragment du joueur, même espace (vue) que envEyeDepth.
                float myEyeDepth = -TransformWorldToView(IN.positionWS).z;

                // Rien capturé à ce pixel (raw depth ~0, ciel/vide) → pas d'occlusion par l'environnement.
                if (envRawDepth <= 0.0001) discard;

                // L'environnement est-il notablement plus proche que ce point du joueur ?
                if (envEyeDepth >= myEyeDepth - 0.02) discard;

                return _SilhouetteColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
