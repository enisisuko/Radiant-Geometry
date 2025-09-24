Shader "FadedDreams/ProjectedLightText"
{
    Properties
    {
        _MainTex ("Mask (White=Light)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,10)) = 2
        _Softness ("Edge Softness", Range(0,1)) = 0.25
        _Stretch ("Stretch", Range(0.5, 2)) = 1
        _Skew ("Skew", Range(-0.6, 0.6)) = 0
        _Flicker ("Flicker", Range(0,1)) = 0.1
        _Shatter ("Shatter", Range(0,1)) = 0
    }
    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "RenderPipeline"="UniversalRenderPipeline"
        }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UnlitProjected"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // URP Core（TransformObjectToHClip 等）
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Tint;
            float _Intensity, _Softness, _Stretch, _Skew, _Flicker, _Shatter;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            float hash(float2 p){ return frac(sin(dot(p, float2(12.9898,78.233)))*43758.5453); }

            Varyings vert (Attributes v)
            {
                Varyings o;

                // 拉伸/斜切在对象空间的 XZ 平面上进行（按钮是水平放的 Quad）
                float3 p = v.positionOS.xyz;
                float2 xz = p.xz;
                xz.x *= _Stretch;
                xz.x += xz.y * _Skew;
                p.xz = xz;

                o.positionHCS = TransformObjectToHClip(float4(p,1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // 纹理羽化（把白字变成柔边的光）
            float smoothEdge(float a, float softness){
                // a: 亮度（0~1），softness 越大越柔
                float t = saturate((a - softness) / max(1e-4, (1-softness)));
                return t;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // 轻微噪声闪烁
                float n = hash(i.uv * (_Time.y * 3.17));
                float flick = 1 + (n - 0.5) * _Flicker;

                // Shatter：把 UV 切成小格并做随机偏移
                float2 uv = i.uv;
                if(_Shatter > 0.001)
                {
                    float cells = 24.0;
                    float2 grid = floor(uv * cells);
                    float2 inCell = frac(uv * cells);
                    float rnd = hash(grid + _Time.yy);
                    float2 offset = (rnd - 0.5) * 0.06 * _Shatter;
                    uv = (grid + inCell + offset) / cells;
                    tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                }

                float lum = tex.r; // 白字
                float edge = smoothEdge(lum, _Softness);

                float3 col = _Tint.rgb * _Intensity * flick * edge;
                return float4(col, edge); // Additive 混合
            }
            ENDHLSL
        }
    }
}
