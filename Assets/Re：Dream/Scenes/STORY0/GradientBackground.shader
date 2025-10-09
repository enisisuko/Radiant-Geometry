Shader "Unlit/GradientBackground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        LOD 100
        Cull Off
        
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
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 左下角(0,0)是白色，右上角(1,1)是深色
                // 计算从左下角的距离
                float dist = length(i.uv);
                
                // 从白色渐变到深灰色
                fixed4 white = fixed4(1, 1, 1, 1);
                fixed4 dark = fixed4(0.1, 0.1, 0.15, 1);
                
                // 使用平滑的渐变
                float gradient = smoothstep(0, 1.414, dist);  // 1.414是对角线长度
                
                fixed4 col = lerp(white, dark, gradient);
                return col;
            }
            ENDCG
        }
    }
}

