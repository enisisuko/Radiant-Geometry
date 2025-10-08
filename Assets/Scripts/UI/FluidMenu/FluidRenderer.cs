using UnityEngine;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体渲染器
    /// 负责将流体场数据渲染为纹理
    /// </summary>
    public class FluidRenderer : MonoBehaviour
    {
        [Header("渲染设置")]
        public FilterMode textureFilterMode = FilterMode.Bilinear;
        public TextureWrapMode textureWrapMode = TextureWrapMode.Clamp;
        public TextureFormat textureFormat = TextureFormat.RGBA32;
        
        [Header("颜色设置")]
        public Color fluidColor = Color.white;
        public bool useAlphaFromDensity = true;
        public float alphaMultiplier = 1.0f;
        
        // 渲染数据
        private Texture2D fluidTexture;
        private Color[] fluidPixels;
        
        // 引用流体场管理器
        private FluidFieldManager fieldManager;
        
        void Start()
        {
            fieldManager = GetComponent<FluidFieldManager>();
            CreateFluidTexture();
        }
        
        void CreateFluidTexture()
        {
            if (fieldManager == null) return;
            
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            fluidTexture = new Texture2D(gridWidth, gridHeight, textureFormat, false);
            fluidTexture.filterMode = textureFilterMode;
            fluidTexture.wrapMode = textureWrapMode;
            fluidPixels = new Color[gridWidth * gridHeight];
        }
        
        public void UpdateFluidTexture()
        {
            if (fieldManager == null || fluidTexture == null) return;
            
            int gridWidth = fieldManager.GetGridWidth();
            int gridHeight = fieldManager.GetGridHeight();
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    float density = fieldManager.GetDensity(x, y);
                    float alpha = useAlphaFromDensity ? Mathf.Clamp01(density * alphaMultiplier) : 1f;
                    
                    // 创建颜色，让Image的颜色显示
                    Color pixelColor = new Color(fluidColor.r, fluidColor.g, fluidColor.b, alpha);
                    fluidPixels[y * gridWidth + x] = pixelColor;
                }
            }
            
            fluidTexture.SetPixels(fluidPixels);
            fluidTexture.Apply();
        }
        
        public Texture2D GetFluidTexture()
        {
            return fluidTexture;
        }
        
        public void SetFluidColor(Color color)
        {
            fluidColor = color;
        }
        
        public void SetAlphaMultiplier(float multiplier)
        {
            alphaMultiplier = multiplier;
        }
        
        public void SetUseAlphaFromDensity(bool useAlpha)
        {
            useAlphaFromDensity = useAlpha;
        }
        
        public void SetTextureFilterMode(FilterMode filterMode)
        {
            textureFilterMode = filterMode;
            if (fluidTexture != null)
            {
                fluidTexture.filterMode = filterMode;
            }
        }
        
        public void SetTextureWrapMode(TextureWrapMode wrapMode)
        {
            textureWrapMode = wrapMode;
            if (fluidTexture != null)
            {
                fluidTexture.wrapMode = wrapMode;
            }
        }
        
        public void RecreateTexture()
        {
            if (fluidTexture != null)
            {
                DestroyImmediate(fluidTexture);
            }
            CreateFluidTexture();
        }
        
        public Color GetFluidColor()
        {
            return fluidColor;
        }
        
        public float GetAlphaMultiplier()
        {
            return alphaMultiplier;
        }
        
        public bool GetUseAlphaFromDensity()
        {
            return useAlphaFromDensity;
        }
        
        public FilterMode GetTextureFilterMode()
        {
            return textureFilterMode;
        }
        
        public TextureWrapMode GetTextureWrapMode()
        {
            return textureWrapMode;
        }
        
        public int GetTextureWidth()
        {
            return fluidTexture != null ? fluidTexture.width : 0;
        }
        
        public int GetTextureHeight()
        {
            return fluidTexture != null ? fluidTexture.height : 0;
        }
        
        void OnDestroy()
        {
            if (fluidTexture != null)
            {
                DestroyImmediate(fluidTexture);
            }
        }
    }
}
