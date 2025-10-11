// ColorfulFluid3DWater.shader
// 彩色流体水面Shader - 支持多色混合渲染
// 功能：颜色场渲染、颜色混合、HDR发光、动态波纹

Shader "FadedDreams/ColorfulFluid3DWater"
{
    Properties
    {
        // 3D颜色场纹理
        _ColorField ("颜色场 (3D)", 3D) = "white" {}
        _GridSize ("网格大小", Float) = 64
        
        // 水面基础属性
        _BaseColor ("基础颜色", Color) = (0.1, 0.2, 0.4, 0.9)
        _SurfaceStrength ("表面强度", Range(0, 1)) = 0.8
        
        // 波纹动画
        _WaveSpeed ("波纹速度", Float) = 1.0
        _WaveScale ("波纹缩放", Float) = 10.0
        _WaveStrength ("波纹强度", Float) = 0.3
        
        // 发光和混合
        _EmissionIntensity ("发光强度", Float) = 3.0
        _ColorBlendStrength ("颜色混合强度", Range(0, 2)) = 1.5
        _ColorContrast ("颜色对比度", Range(0.5, 3)) = 1.8
        
        // 反射
        _FresnelPower ("菲涅尔强度", Range(0, 10)) = 3.0
        _ReflectionStrength ("反射强度", Range(0, 1)) = 0.5
        
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
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float fogCoord : TEXCOORD6;
            };
            
            // 属性
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _SurfaceStrength;
                half _WaveSpeed;
                half _WaveScale;
                half _WaveStrength;
                half _EmissionIntensity;
                half _ColorBlendStrength;
                half _ColorContrast;
                half _FresnelPower;
                half _ReflectionStrength;
                half _Smoothness;
                half _Metallic;
                half _GridSize;
            CBUFFER_END
            
            TEXTURE3D(_ColorField);
            SAMPLER(sampler_ColorField);
            
            // 生成动态波纹法线
            float3 GenerateWaveNormal(float2 uv, float time)
            {
                float2 waveUV = uv * _WaveScale;
                
                // 多层波纹叠加
                float wave1 = sin(waveUV.x * 2.0 + time * _WaveSpeed) * _WaveStrength;
                float wave2 = sin(waveUV.y * 1.5 + time * _WaveSpeed * 1.2) * _WaveStrength * 0.7;
                float wave3 = sin((waveUV.x + waveUV.y) * 1.0 + time * _WaveSpeed * 0.9) * _WaveStrength * 0.5;
                
                // 计算法线
                float3 normal = normalize(float3(-wave1 * 0.3, 1.0, -wave2 * 0.3));
                return normal;
            }
            
            // 从3D颜色场采样颜色
            float4 SampleColorField(float3 posOS)
            {
                // 将对象空间位置转换到纹理空间[0,1]
                float3 uvw = (posOS + float3(15, 10, 10)) / float3(30, 20, 20); // 假设水面大小30x20x20
                uvw.y = saturate(uvw.y + 0.1); // 稍微向上偏移采样
                
                // 采样3D颜色纹理
                float4 colorSample = SAMPLE_TEXTURE3D(_ColorField, sampler_ColorField, uvw);
                
                // 增强颜色对比度
                colorSample.rgb = pow(colorSample.rgb, _ColorContrast);
                
                return colorSample;
            }
            
            // 混合多个颜色采样（创造更丰富的混合效果）
            float4 BlendColorSamples(float3 posOS)
            {
                float4 center = SampleColorField(posOS);
                
                // 周围采样创造更平滑的混合
                float offset = 0.5;
                float4 sample1 = SampleColorField(posOS + float3(offset, 0, 0));
                float4 sample2 = SampleColorField(posOS + float3(-offset, 0, 0));
                float4 sample3 = SampleColorField(posOS + float3(0, 0, offset));
                float4 sample4 = SampleColorField(posOS + float3(0, 0, -offset));
                
                // 加权平均
                float4 blended = (center * 2.0 + sample1 + sample2 + sample3 + sample4) / 6.0;
                
                // 增强颜色饱和度
                float3 hsv;
                float maxC = max(max(blended.r, blended.g), blended.b);
                float minC = min(min(blended.r, blended.g), blended.b);
                float delta = maxC - minC;
                
                if (delta > 0.001)
                {
                    // 增强饱和度
                    float saturation = delta / maxC;
                    saturation = pow(saturation, 0.7); // 提升饱和度
                    blended.rgb = lerp(float3(maxC, maxC, maxC), blended.rgb, saturation);
                }
                
                return blended;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 1. 采样并混合颜色场
                float4 fluidColor = BlendColorSamples(input.positionOS);
                
                // 2. 生成动态波纹法线
                float3 waveNormal = GenerateWaveNormal(input.uv, _Time.y);
                float3 normal = normalize(lerp(input.normalWS, waveNormal, _WaveStrength));
                
                // 3. 计算菲涅尔效果
                float3 viewDir = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                
                // 4. 主光照
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * mainLight.distanceAttenuation;
                
                // 5. 反射高光
                float3 reflectDir = reflect(-viewDir, normal);
                half specular = pow(saturate(dot(reflectDir, mainLight.direction)), 32.0) * _Smoothness;
                
                // 6. 合成最终颜色
                // 基础颜色：流体颜色与水面基础色混合
                half3 baseColor = lerp(_BaseColor.rgb, fluidColor.rgb, fluidColor.a * _ColorBlendStrength);
                
                // 应用光照
                half3 finalColor = baseColor * lighting;
                
                // 添加高光
                finalColor += specular * mainLight.color * _ReflectionStrength;
                
                // 发光效果（根据颜色亮度）
                float luminance = dot(fluidColor.rgb, float3(0.299, 0.587, 0.114));
                half3 emission = fluidColor.rgb * _EmissionIntensity * luminance;
                finalColor += emission;
                
                // 菲涅尔增强边缘
                finalColor = lerp(finalColor, mainLight.color, fresnel * _ReflectionStrength);
                
                // 7. 透明度
                half alpha = lerp(_BaseColor.a, 1.0, fluidColor.a * _SurfaceStrength);
                alpha *= (0.7 + fresnel * 0.3);
                
                // 8. 雾效
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    
    // 降级到标准流体Shader
    FallBack "FadedDreams/Fluid3DWater"
}
