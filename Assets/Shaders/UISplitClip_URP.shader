Shader "Universal Render Pipeline/UI/SplitClip"
{
    Properties{
        _MainTex ("Texture", 2D) = "white" {}
        _Center  ("Center UV", Vector) = (0.5, 0.5, 0, 0)
        _Dir     ("Line Dir (uv)", Vector) = (1, 0, 0, 0)
        _Side    ("Side (+1 / -1)", Float) = 1
        _Feather ("Feather (uv)", Float) = 0.001
        _Color   ("Tint", Color) = (1,1,1,1)
    }
    SubShader{
        Tags{
            "Queue"="Transparent" "RenderType"="Transparent"
            "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass{
            Tags{ "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Center, _Dir, _Color;
            float _Side, _Feather;

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            Varyings vert(Attributes v){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i):SV_Target{
                float2 n = float2(-_Dir.y, _Dir.x);
                float sd = dot(n, i.uv - _Center.xy);
                float m  = smoothstep(0.0, _Feather, _Side * (-sd));
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                col.a *= m; col.rgb *= col.a;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
