// FluidMenuPanel.shader
// 华丽菜单面板Shader
// 功能：玻璃质感、模糊背景、边缘发光、渐变叠加

Shader "FadedDreams/FluidMenuPanel"
{
    Properties
    {
        // 基础属性
        _BaseColor ("基础颜色", Color) = (0.1, 0.1, 0.15, 0.9)
        _GlassStrength ("玻璃强度", Range(0, 1)) = 0.8
        
        // 模糊
        _BlurStrength ("模糊强度", Range(0, 10)) = 3.0
        
        // 边缘发光
        _EdgeColor ("边缘发光颜色", Color) = (0.5, 0.8, 1.0, 1.0)
        _EdgeIntensity ("边缘强度", Float) = 2.0
        _EdgeWidth ("边缘宽度", Range(0, 1)) = 0.1
        
        // 渐变
        _GradientColor1 ("渐变颜色1", Color) = (0.2, 0.4, 1.0, 1.0)
        _GradientColor2 ("渐变颜色2", Color) = (1.0, 0.3, 0.6, 1.0)
        _GradientAngle ("渐变角度", Range(0, 360)) = 45
        _GradientIntensity ("渐变强度", Range(0, 1)) = 0.5
        
        // 反射
        _ReflectionIntensity ("反射强度", Range(0, 1)) = 0.4
        _Smoothness ("光滑度", Range(0, 1)) = 0.9
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };
            
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _GlassStrength;
                half _BlurStrength;
                half4 _EdgeColor;
                half _EdgeIntensity;
                half _EdgeWidth;
                half4 _GradientColor1;
                half4 _GradientColor2;
                half _GradientAngle;
                half _GradientIntensity;
                half _ReflectionIntensity;
                half _Smoothness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 1. 计算渐变
                float angleRad = radians(_GradientAngle);
                float2 gradientDir = float2(cos(angleRad), sin(angleRad));
                float gradientValue = dot(input.uv - 0.5, gradientDir) + 0.5;
                gradientValue = saturate(gradientValue);
                
                half4 gradientColor = lerp(_GradientColor1, _GradientColor2, gradientValue);
                
                // 2. 边缘检测（用于边缘发光）
                float2 edgeDist = abs(input.uv - 0.5) * 2.0;
                float edgeFactor = max(edgeDist.x, edgeDist.y);
                edgeFactor = smoothstep(1.0 - _EdgeWidth, 1.0, edgeFactor);
                
                // 3. 计算菲涅尔（玻璃感）
                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 3.0);
                
                // 4. 基础颜色
                half4 baseColor = _BaseColor;
                
                // 5. 混合渐变
                half3 finalColor = lerp(baseColor.rgb, gradientColor.rgb, _GradientIntensity);
                
                // 6. 添加边缘发光
                half3 edgeGlow = _EdgeColor.rgb * _EdgeIntensity * edgeFactor;
                finalColor += edgeGlow;
                
                // 7. 添加菲涅尔反射
                finalColor = lerp(finalColor, half3(1,1,1), fresnel * _ReflectionIntensity);
                
                // 8. 玻璃效果
                finalColor = lerp(finalColor, finalColor * 1.5, _GlassStrength * fresnel);
                
                // 9. 透明度
                half alpha = baseColor.a * (0.8 + fresnel * 0.2);
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}

