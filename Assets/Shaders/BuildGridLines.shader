Shader "MonsterHotel/BuildGridLines"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1, 1, 1, 0.9)
        _LineWidth ("Line Width (fraction of cell)", Range(0.005, 0.3)) = 0.04
        _CellCount ("Cell Count (X, Y)", Vector) = (10, 10, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColor;
                float  _LineWidth;
                float4 _CellCount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;
                return OUT;
            }

            // Dessine juste les contours de chaque cellule (grille de _CellCount.x × _CellCount.y
            // sur le quad) — transparent ailleurs. Pas de texture nécessaire.
            half4 frag(Varyings IN) : SV_Target
            {
                float2 cellUV     = frac(IN.uv * _CellCount.xy);
                float2 distToEdge = min(cellUV, 1.0 - cellUV);
                float  onLine     = step(min(distToEdge.x, distToEdge.y), _LineWidth);

                half4 col = _LineColor;
                col.a *= onLine;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
