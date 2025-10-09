Shader "Custom/GradientBackground"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0, 0, 0, 1)
        _BottomColor ("Bottom Color", Color) = (1, 1, 1, 1)
        _GradientPower ("Gradient Power", Range(0.1, 5.0)) = 1.0
        _OffsetX ("Horizontal Offset", Range(-1, 1)) = -0.5
        _OffsetY ("Vertical Offset", Range(-1, 1)) = -0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            };
            
            float4 _TopColor;
            float4 _BottomColor;
            float _GradientPower;
            float _OffsetX;
            float _OffsetY;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 计算从左下角的距离
                float2 offset = float2(i.uv.x + _OffsetX, i.uv.y + _OffsetY);
                float dist = length(offset);
                
                // 应用渐变power来控制渐变强度
                float gradient = pow(saturate(dist), _GradientPower);
                
                // 在底部颜色和顶部颜色之间插值
                fixed4 col = lerp(_BottomColor, _TopColor, gradient);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}

