using UnityEngine;
using UnityEngine.UI;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体形状生成器
    /// 创建水一样的不规则形状纹理
    /// </summary>
    public class FluidShapeGenerator : MonoBehaviour
    {
        [Header("形状参数")]
        public int textureSize = 256;
        public float noiseScale = 0.1f;
        public float edgeSoftness = 0.3f;
        public float irregularity = 0.4f;
        
        [Header("动画参数")]
        public float animationSpeed = 1.0f;
        public float animationAmplitude = 0.1f;
        
        private Texture2D fluidTexture;
        private float animationTime = 0f;
        
        void Start()
        {
            GenerateFluidTexture();
        }
        
        void Update()
        {
            // 更新动画时间
            animationTime += Time.deltaTime * animationSpeed;
            
            // 重新生成纹理以创建动画效果
            if (animationTime >= 1.0f)
            {
                animationTime = 0f;
                GenerateFluidTexture();
            }
        }
        
        public Texture2D GenerateFluidTexture()
        {
            fluidTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            fluidTexture.filterMode = FilterMode.Bilinear;
            fluidTexture.wrapMode = TextureWrapMode.Clamp;
            
            Color[] pixels = new Color[textureSize * textureSize];
            
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float u = (float)x / textureSize;
                    float v = (float)y / textureSize;
                    
                    // 计算到中心的距离
                    Vector2 center = new Vector2(0.5f, 0.5f);
                    Vector2 pos = new Vector2(u, v);
                    float distanceToCenter = Vector2.Distance(pos, center);
                    
                    // 基础圆形
                    float baseShape = 1.0f - smoothstep(0.3f, 0.5f, distanceToCenter);
                    
                    // 添加不规则性
                    float noise1 = Mathf.PerlinNoise(u * noiseScale + animationTime, v * noiseScale + animationTime);
                    float noise2 = Mathf.PerlinNoise(u * noiseScale * 2 + animationTime * 0.5f, v * noiseScale * 2 + animationTime * 0.5f);
                    float noise3 = Mathf.PerlinNoise(u * noiseScale * 4 + animationTime * 0.25f, v * noiseScale * 4 + animationTime * 0.25f);
                    
                    float combinedNoise = (noise1 + noise2 * 0.5f + noise3 * 0.25f) / 1.75f;
                    
                    // 创建不规则边缘
                    float irregularEdge = 1.0f - smoothstep(0.2f + combinedNoise * irregularity, 0.6f + combinedNoise * irregularity, distanceToCenter);
                    
                    // 组合形状
                    float finalShape = Mathf.Max(baseShape, irregularEdge);
                    
                    // 添加边缘软化
                    float edgeFade = 1.0f - smoothstep(0.4f - edgeSoftness, 0.4f + edgeSoftness, distanceToCenter);
                    finalShape *= edgeFade;
                    
                    // 添加动画扰动
                    float animationNoise = Mathf.Sin(animationTime * 2 * Mathf.PI + distanceToCenter * 10) * animationAmplitude;
                    finalShape += animationNoise;
                    
                    // 确保值在0-1范围内
                    finalShape = Mathf.Clamp01(finalShape);
                    
                    // 设置像素颜色
                    pixels[y * textureSize + x] = new Color(1, 1, 1, finalShape);
                }
            }
            
            fluidTexture.SetPixels(pixels);
            fluidTexture.Apply();
            
            return fluidTexture;
        }
        
        private float smoothstep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0f - 2.0f * t);
        }
        
        public Texture2D GetFluidTexture()
        {
            return fluidTexture;
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
