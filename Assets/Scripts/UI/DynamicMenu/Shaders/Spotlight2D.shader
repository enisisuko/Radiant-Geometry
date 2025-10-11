// Spotlight2D.shader
// 2D聚光灯效果Shader
// 功能：定向光束、光锥角度、HDR发光、平滑衰减

Shader "FadedDreams/Spotlight2D"
{
    Properties
    {
        [Header(Spotlight Settings)]
        _SpotlightColor ("聚光灯颜色", Color) = (1, 1, 1, 1)
        _Intensity ("强度", Float) = 3.0
        _ConeAngle ("光锥角度", Range(5, 180)) = 30
        _Direction ("方向 (XY)", Vector) = (0, 1, 0, 0)
        _Position ("位置 (XY)", Vector) = (0.5, 0.5, 0, 0)
        
        [Header(Falloff)]
        _FalloffPower ("衰减强度", Range(0.1, 10)) = 2.0
        _MaxDistance ("最大距离", Float) = 1000
        _EdgeSoftness ("边缘柔和度", Range(0, 1)) = 0.2
        
        [Header(Visual)]
        _BeamWidth ("光束宽度", Range(0.01, 1)) = 0.1
        _BeamIntensity ("光束内部强度", Range(1, 3)) = 1.5
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }
        
        Blend SrcAlpha One // 叠加混合
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float2 screenPos : TEXCOORD1;
            };
            
            // 属性
            float4 _SpotlightColor;
            float _Intensity;
            float _ConeAngle;
            float4 _Direction;
            float4 _Position;
            float _FalloffPower;
            float _MaxDistance;
            float _EdgeSoftness;
            float _BeamWidth;
            float _BeamIntensity;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                // 计算屏幕空间位置
                o.screenPos = ComputeScreenPos(o.vertex).xy / ComputeScreenPos(o.vertex).w;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 获取当前像素位置（屏幕空间）
                float2 pixelPos = i.screenPos;
                
                // 聚光灯位置（归一化屏幕坐标）
                float2 spotPos = _Position.xy;
                
                // 聚光灯方向（归一化）
                float2 spotDir = normalize(_Direction.xy);
                
                // 从聚光灯到像素的向量
                float2 toPixel = pixelPos - spotPos;
                float distanceToSpot = length(toPixel);
                
                // 如果距离太远，直接返回透明
                if (distanceToSpot > _MaxDistance * 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }
                
                // 归一化方向
                float2 toPixelDir = normalize(toPixel);
                
                // 计算角度差（点积）
                float angleDot = dot(spotDir, toPixelDir);
                
                // 将点积转换为角度（0-180度）
                float angleInDegrees = acos(angleDot) * 57.2958; // Rad to Deg
                
                // 光锥角度判断
                float halfConeAngle = _ConeAngle * 0.5;
                
                // 在光锥内
                if (angleInDegrees < halfConeAngle)
                {
                    // 距离因子（改进：让光束在整个长度上保持较强的强度）
                    float normalizedDist = distanceToSpot / (_MaxDistance * 0.001);
                    float distanceFactor = 1.0 - saturate(normalizedDist);
                    
                    // 使用更平缓的衰减曲线，让光束整体更明亮
                    distanceFactor = pow(distanceFactor, _FalloffPower * 0.5);
                    
                    // 在终点附近增加亮度（聚焦效果）
                    float focusPoint = 1.0 - normalizedDist;
                    float focusBoost = 1.0 + smoothstep(0.8, 1.0, focusPoint) * 2.0; // 在接近终点时亮度提升
                    
                    // 角度衰减（从中心向边缘衰减）- 让光束更集中
                    float angleFactor = 1.0 - (angleInDegrees / halfConeAngle);
                    angleFactor = pow(angleFactor, 2.0); // 增加指数让边缘更暗
                    
                    // 光束中心增强（更强的中心效果）
                    float beamCenterFactor = 1.0;
                    if (angleFactor > (1.0 - _BeamWidth))
                    {
                        float beamCenter = (angleFactor - (1.0 - _BeamWidth)) / _BeamWidth;
                        beamCenterFactor = lerp(1.0, _BeamIntensity * 2.0, beamCenter); // 加倍中心强度
                    }
                    
                    // 综合强度（包含聚焦增强）
                    float finalIntensity = distanceFactor * angleFactor * _Intensity * beamCenterFactor * focusBoost;
                    
                    // 最终颜色（HDR）
                    fixed4 color = _SpotlightColor * finalIntensity;
                    color.a = saturate(finalIntensity * 0.8); // 透明度也受强度影响
                    
                    return color;
                }
                
                // 不在光锥内，返回透明
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}

