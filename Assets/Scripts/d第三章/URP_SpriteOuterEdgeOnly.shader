Shader "URP/SpriteOuterEdgeOnlyWide"
{
    Properties{
        _MainTex("Sprite", 2D) = "white" {}
        _EdgeColor("Edge Color", Color) = (1,0.85,0.2,1)
        _ThicknessPx("Thickness (px)", Range(0,16)) = 8
        _Rings("Rings (1-3)", Range(1,3)) = 3
        _Softness("Softness", Range(0,1)) = 0.25
    }
    SubShader{
        Tags{
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass{
            Name "OuterEdge"
            Tags{ "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes{ float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings  { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // x=1/w, y=1/h
            float4 _EdgeColor;
            float  _ThicknessPx, _Rings, _Softness;

            static const float2 DIRS[12] = {
                float2( 1,0), float2( 0.8660254, 0.5), float2( 0.5, 0.8660254),
                float2( 0,1), float2(-0.5, 0.8660254), float2(-0.8660254, 0.5),
                float2(-1,0), float2(-0.8660254,-0.5), float2(-0.5,-0.8660254),
                float2( 0,-1), float2( 0.5,-0.8660254), float2( 0.8660254,-0.5)
            };

            Varyings Vert(Attributes v){
                Varyings o; o.positionHCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color; return o;
            }

            float4 Frag(Varyings i):SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float  a = c.a;
                if (_ThicknessPx <= 0.001 || a <= 0.0) return float4(0,0,0,0);

                float2 texel = _MainTex_TexelSize.xy;

                // 三个半径（均匀分布），通过 _Rings 控制启用到第几圈
                float r1 = _ThicknessPx * (1.0/3.0);
                float r2 = _ThicknessPx * (2.0/3.0);
                float r3 = _ThicknessPx;
                float use2 = step(1.5, _Rings);  // _Rings>=2 ? 1:0
                float use3 = step(2.5, _Rings);  // _Rings>=3 ? 1:0

                float maxA = 0.0;

                [unroll] for (int k=0; k<12; k++)
                {
                    float2 d = DIRS[k];
                    float2 o1 = d * texel * r1;
                    float2 o2 = d * texel * r2;
                    float2 o3 = d * texel * r3;

                    float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + o1).a;
                    float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + o2).a * use2;
                    float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + o3).a * use3;

                    maxA = max(maxA, max(a1, max(a2, a3)));
                }

                // 膨胀后的 alpha 与自身 alpha 的差 → 边带
                float edgeBand = saturate(maxA - a);

                // 只保留外侧（防止渗进内部叠层）
                float outside = edgeBand * (1 - a);

                // 柔和过渡
                outside = smoothstep(0, max(1e-4, _Softness), outside);

                return float4(_EdgeColor.rgb, outside * _EdgeColor.a);
            }
            ENDHLSL
        }
    }
}
