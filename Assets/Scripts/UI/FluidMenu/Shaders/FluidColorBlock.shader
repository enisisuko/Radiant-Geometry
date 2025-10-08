Shader "UI/FluidColorBlock"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Fluid Effects)]
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 2.0
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.02
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 2.0
        _WaveFrequency ("Wave Frequency", Range(0, 50)) = 10.0
        
        [Header(Pressure Field)]
        _PressureCenter ("Pressure Center", Vector) = (0,0,0,0)
        _PressureRadius ("Pressure Radius", Range(0, 2)) = 1.0
        _PressureStrength ("Pressure Strength", Range(0, 1)) = 0.5
        
        [Header(Soft Edges)]
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.1
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.5
        
        [Header(Animation)]
        _BreathScale ("Breath Scale", Range(0, 0.2)) = 0.05
        _BreathSpeed ("Breath Speed", Range(0, 5)) = 1.0
        _HoverScale ("Hover Scale", Range(1, 2)) = 1.5
        _SqueezeScale ("Squeeze Scale", Range(0.5, 1)) = 0.7
        
        [Header(Stencil)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 pressureUV : TEXCOORD2;
                float fluidDistortion : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            // Fluid properties
            fixed4 _EmissionColor;
            float _EmissionIntensity;
            float _DistortionStrength;
            float _WaveSpeed;
            float _WaveFrequency;
            
            // Pressure field
            float4 _PressureCenter;
            float _PressureRadius;
            float _PressureStrength;
            
            // Soft edges
            float _EdgeSoftness;
            float _EdgeFade;
            
            // Animation
            float _BreathScale;
            float _BreathSpeed;
            float _HoverScale;
            float _SqueezeScale;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                // Calculate pressure field effect
                float2 pressureDir = v.texcoord - _PressureCenter.xy;
                float pressureDistance = length(pressureDir);
                float pressureEffect = 1.0 - smoothstep(0, _PressureRadius, pressureDistance);
                pressureEffect *= _PressureStrength;
                
                // Apply pressure-based vertex displacement
                float2 pressureOffset = normalize(pressureDir) * pressureEffect * _DistortionStrength;
                OUT.vertex.xy += pressureOffset;
                
                // Fluid wave distortion
                float wave = sin(_Time.y * _WaveSpeed + pressureDistance * _WaveFrequency) * pressureEffect;
                OUT.fluidDistortion = wave * _DistortionStrength;
                
                // Store pressure UV for fragment shader
                OUT.pressureUV = v.texcoord;
                
                OUT.color = v.color * _Color;
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Apply fluid distortion to UV coordinates
                float2 distortedUV = IN.texcoord;
                distortedUV.x += IN.fluidDistortion * 0.1;
                distortedUV.y += IN.fluidDistortion * 0.05;
                
                // Sample main texture with distortion
                half4 color = (tex2D(_MainTex, distortedUV) + _TextureSampleAdd) * IN.color;
                
                // Calculate pressure field effect for fragment
                float2 pressureDir = IN.pressureUV - _PressureCenter.xy;
                float pressureDistance = length(pressureDir);
                float pressureEffect = 1.0 - smoothstep(0, _PressureRadius, pressureDistance);
                pressureEffect *= _PressureStrength;
                
                // Soft edges with pressure influence
                float2 edgeDistance = abs(IN.pressureUV - 0.5) * 2.0;
                float maxEdge = max(edgeDistance.x, edgeDistance.y);
                float edgeFade = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, maxEdge);
                edgeFade = lerp(edgeFade, 1.0, pressureEffect * 0.5); // Pressure makes edges softer
                
                // Apply edge fade
                color.a *= edgeFade * _EdgeFade;
                
                // Add emission based on pressure
                float emissionMultiplier = 1.0 + pressureEffect * 2.0;
                color.rgb += _EmissionColor.rgb * _EmissionIntensity * emissionMultiplier;
                
                // Add subtle color variation based on pressure
                color.rgb += pressureEffect * _EmissionColor.rgb * 0.3;
                
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}