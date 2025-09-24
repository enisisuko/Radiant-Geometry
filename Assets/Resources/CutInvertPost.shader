// Assets/Shaders/CutInvertPost.shader
Shader "Hidden/CutInvertPost"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            // 仅保留 Core（不用 Blit.hlsl）
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // —— 兼容宏：某些 URP 版本没有 *_X 宏时定义回退 —— 
            #ifndef TEXTURE2D_X
            #define TEXTURE2D_X TEXTURE2D
            #endif
            #ifndef SAMPLE_TEXTURE2D_X
            #define SAMPLE_TEXTURE2D_X SAMPLE_TEXTURE2D
            #endif

            // Blitter 写入的相机颜色纹理（由 C# 的 Blitter.BlitCameraTexture 传入）
            // 注意：名字必须是 _BlitTexture
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);


            // CutManager 推送的全局数组
            int     _CutScarCount;
            float4  _CutScarAB[32];   // xy=a, zw=b
            float4  _CutScarLife[32]; // x=born, y=life, z=width, w=sweepDur

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // 全屏三角形顶点（不依赖 Blit.hlsl / 也不要求网格）
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                // 生成覆盖全屏的 3 顶点（“大三角”）
                // 顶点UV： (0,0),(2,0),(0,2) 映射
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.uv = uv;

                // NDC 顶点：uv*2-1 形成覆盖全屏的三角
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            // 暂用 UV 作为“世界坐标”近似（便于先验收）；后续可换正交相机世界映射
            float2 WorldLikeFromUV(float2 uv) { return uv; }

            int ParityAt(float2 p, float timeNow)
            {
                int parity = 0;
                int n = _CutScarCount;

                [loop]
                for (int i = 0; i < n; i++)
                {
                    float2 a = _CutScarAB[i].xy;
                    float2 b = _CutScarAB[i].zw;
                    float  tBorn  = _CutScarLife[i].x;
                    float  life   = _CutScarLife[i].y;
                    float  width  = _CutScarLife[i].z;
                    float  sweep  = _CutScarLife[i].w;

                    float t = timeNow - tBorn;
                    if (t < 0 || t > life) continue;

                    float2 ab = b - a;
                    float len = max(length(ab), 1e-5);
                    float2 dir = ab / len;
                    float2 nrm = float2(-dir.y, dir.x); // 左法线
                    float2 ap  = p - a;

                    float along = dot(ap, dir);
                    float prog  = saturate(t / max(sweep, 1e-5)) * len;

                    if (along < -width || along > prog + width) continue;

                    float dist = abs(dot(ap, nrm));
                    if (dist <= width)
                        parity ^= 1; // XOR
                }
                return parity;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                // URP 内置 _TimeParameters：.y 为当前时间（秒）
                float timeNow = _TimeParameters.y;

                // 采样相机颜色（Blitter传入）
                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv);

                float2 p = WorldLikeFromUV(i.uv);
                int parity = ParityAt(p, timeNow);
                if (parity == 1)
                {
                    col.rgb = 1.0 - col.rgb; // 反色
                }
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
