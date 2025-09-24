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

            // ������ Core������ Blit.hlsl��
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ���� ���ݺ꣺ĳЩ URP �汾û�� *_X ��ʱ������� ���� 
            #ifndef TEXTURE2D_X
            #define TEXTURE2D_X TEXTURE2D
            #endif
            #ifndef SAMPLE_TEXTURE2D_X
            #define SAMPLE_TEXTURE2D_X SAMPLE_TEXTURE2D
            #endif

            // Blitter д��������ɫ������ C# �� Blitter.BlitCameraTexture ���룩
            // ע�⣺���ֱ����� _BlitTexture
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);


            // CutManager ���͵�ȫ������
            int     _CutScarCount;
            float4  _CutScarAB[32];   // xy=a, zw=b
            float4  _CutScarLife[32]; // x=born, y=life, z=width, w=sweepDur

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // ȫ�������ζ��㣨������ Blit.hlsl / Ҳ��Ҫ������
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                // ���ɸ���ȫ���� 3 ���㣨�������ǡ���
                // ����UV�� (0,0),(2,0),(0,2) ӳ��
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.uv = uv;

                // NDC ���㣺uv*2-1 �γɸ���ȫ��������
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            // ���� UV ��Ϊ���������ꡱ���ƣ����������գ��������ɻ������������ӳ��
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
                    float2 nrm = float2(-dir.y, dir.x); // ����
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
                // URP ���� _TimeParameters��.y Ϊ��ǰʱ�䣨�룩
                float timeNow = _TimeParameters.y;

                // ���������ɫ��Blitter���룩
                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv);

                float2 p = WorldLikeFromUV(i.uv);
                int parity = ParityAt(p, timeNow);
                if (parity == 1)
                {
                    col.rgb = 1.0 - col.rgb; // ��ɫ
                }
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
