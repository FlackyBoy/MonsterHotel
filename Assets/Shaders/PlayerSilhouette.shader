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

        // Silhouette. ZTest Greater : ne dessine que là où un pixel plus proche de la caméra a déjà
        // été écrit dans le depth buffer (occulté par quelque chose — bloc, mur...). Rien à
        // calculer côté C#, tout est porté par le GPU, par caméra, pixel par pixel.
        //
        // Limite connue acceptée pour l'instant : ne distingue pas "occulté par soi-même" (bras
        // devant le torse) de "occulté par un objet externe" — la tentative de filtrage par stencil
        // (2 passes) a cassé le cas normal sans qu'on identifie la cause exacte (pas d'erreur de
        // compilation), revert à cette version simple qui fonctionne. Piste correcte si besoin de
        // reprendre ça plus tard : ScriptableRendererFeature dédiée qui capture la profondeur de
        // l'environnement AVANT que le joueur ne se dessine, pour comparer contre un depth buffer
        // qui ne contient jamais sa propre géométrie — plus fiable qu'un stencil partagé, mais plus
        // gros chantier (nouvelle render pass URP, pas juste un shader).
        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Greater
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SilhouetteColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _SilhouetteColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
