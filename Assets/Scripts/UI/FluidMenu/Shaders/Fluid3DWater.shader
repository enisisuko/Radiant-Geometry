// Fluid3DWater.shader
// 华丽3D水面Shader
// 功能：法线动画、菲涅尔反射、折射、HDR发光、深度渐变
// 适用于URP渲染管线

Shader "FadedDreams/Fluid3DWater"
{
    Properties
    {
        // 基础颜色
        _ShallowColor ("浅水颜色", Color) = (0.3, 0.8, 1.0, 0.8)
        _DeepColor ("深水颜色", Color) = (0.05, 0.2, 0.5, 1.0)
        _DepthFade ("深度渐变距离", Float) = 5.0
        
        // 波纹动画
        _WaveSpeed ("波纹速度", Float) = 1.0
        _WaveScale ("波纹缩放", Float) = 10.0
        _WaveStrength ("波纹强度", Float) = 0.5
        _WaveFrequency ("波纹频率", Float) = 2.0
        
        // 法线贴图
        _NormalMap ("法线贴图", 2D) = "bump" {}
        _NormalStrength ("法线强度", Range(0, 2)) = 1.0
        
        // 反射
        _ReflectionStrength ("反射强度", Range(0, 1)) = 0.6
        _FresnelPower ("菲涅尔强度", Range(0, 10)) = 3.0
        
        // 发光
        _EmissionColor ("发光颜色", Color) = (0.2, 0.6, 1.0, 1.0)
        _EmissionIntensity ("发光强度", Float) = 2.0
        
        // 折射
        _RefractionStrength ("折射强度", Range(0, 1)) = 0.3
        
        // 光滑度
        _Smoothness ("光滑度", Range(0, 1)) = 0.95
        _Metallic ("金属度", Range(0, 1)) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float fogCoord : TEXCOORD5;
            };
            
            // 属性
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthFade;
                half _WaveSpeed;
                half _WaveScale;
                half _WaveStrength;
                half _WaveFrequency;
                half _NormalStrength;
                half _ReflectionStrength;
                half _FresnelPower;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half _RefractionStrength;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END
            
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            // 波纹函数
            float3 GenerateWaveNormal(float2 uv, float time)
            {
                float2 waveUV = uv * _WaveScale;
                
                // 多层波纹叠加
                float wave1 = sin(waveUV.x * _WaveFrequency + time * _WaveSpeed) * _WaveStrength;
                float wave2 = sin(waveUV.y * _WaveFrequency * 0.8 + time * _WaveSpeed * 1.2) * _WaveStrength * 0.7;
                float wave3 = sin((waveUV.x + waveUV.y) * _WaveFrequency * 0.6 + time * _WaveSpeed * 0.9) * _WaveStrength * 0.5;
                
                float height = wave1 + wave2 + wave3;
                
                // 计算法线
                float3 normal = normalize(float3(-wave1 * 0.3, 1.0, -wave2 * 0.3));
                return normal;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 1. 生成动态波纹法线
                float3 waveNormal = GenerateWaveNormal(input.uv, _Time.y);
                float3 normal = normalize(lerp(input.normalWS, waveNormal, _NormalStrength));
                
                // 2. 计算菲涅尔效果
                float3 viewDir = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                
                // 3. 深度渐变（浅水→深水）
                float depth = length(input.positionWS - _WorldSpaceCameraPos) / _DepthFade;
                depth = saturate(depth);
                half4 waterColor = lerp(_ShallowColor, _DeepColor, depth);
                
                // 4. 主光照
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * mainLight.distanceAttenuation;
                
                // 5. 反射高光
                float3 reflectDir = reflect(-viewDir, normal);
                half specular = pow(saturate(dot(reflectDir, mainLight.direction)), 32.0) * _Smoothness;
                
                // 6. 发光效果
                half3 emission = _EmissionColor.rgb * _EmissionIntensity;
                
                // 7. 最终颜色合成
                half3 finalColor = waterColor.rgb * lighting;
                finalColor += specular * mainLight.color * _ReflectionStrength;
                finalColor += emission * (1.0 + fresnel * 0.5); // 边缘发光更强
                finalColor = lerp(finalColor, mainLight.color, fresnel * _ReflectionStrength);
                
                // 8. 透明度
                half alpha = waterColor.a * (0.7 + fresnel * 0.3);
                
                // 9. 雾效
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    
    // 降级到内置管线
    FallBack "Standard"
}

