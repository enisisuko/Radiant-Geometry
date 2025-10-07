Shader "Enisisuko/ParticlesBloomAdditive_Builtin"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _TintColor ("HDR Tint (RGB) * Intensity (A)", Color) = (1,1,1,2)
        _SoftFactor ("Soft Particles Factor", Range(0.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "Forward"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile __ SOFTPARTICLES_ON

            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _TintColor;    // rgb: color, a: base intensity (HDR in material)
            float _SoftFactor;

            // Built-in soft particles需要的深度纹理声明
            #if defined(SOFTPARTICLES_ON)
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;       // 粒子颜色/透明
                float4 custom1: TEXCOORD1;   // Particle Custom Data (x 用作额外强度)
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR0;
                float  intens: TEXCOORD1;
                float4 projPos : TEXCOORD2;  // for soft particles (screen pos)
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.intens = max(v.custom1.x, 0.0);

                #if defined(SOFTPARTICLES_ON)
                o.projPos = ComputeScreenPos(o.pos);
                #endif

                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }

            inline float SoftParticleFade(float4 projPos)
            {
            #if defined(SOFTPARTICLES_ON)
                // 采样场景深度（投影坐标）
                float scene01 = Linear01Depth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(projPos)));
                // 粒子片元的线性深度（使用投影 w 保存的 eye depth）
                float particle01 = Linear01Depth(projPos.z / projPos.w);
                // 差越小越贴近→需要更多淡出
                float k = max(_SoftFactor * 0.1, 1e-5);
                return saturate((scene01 - particle01) / k);
            #else
                return 1.0;
            #endif
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_BaseMap, i.uv);

                // A 通道当“基础强度”，Custom1.x 作为“每粒子额外强度”
                fixed baseIntensity = _TintColor.a;
                fixed addIntensity  = i.intens;
                fixed finalIntensity = baseIntensity + addIntensity;

                // HDR 颜色驱动 Bloom（确保材质面板里使用 HDR 颜色值 > 1）
                half3 col = _TintColor.rgb * i.color.rgb * tex.rgb * finalIntensity;
                half alpha = tex.a * i.color.a;

                // Soft Particles
                alpha *= SoftParticleFade(i.projPos);

                fixed4 color = fixed4(col * alpha, alpha);

                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }

    FallBack Off
}
