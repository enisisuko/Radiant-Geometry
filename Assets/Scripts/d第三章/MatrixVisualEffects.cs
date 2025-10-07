// 📋 代码总览: 请先阅读 Assets/Scripts/CODE_OVERVIEW.md 了解完整项目结构
// 🚀 开发指南: 参考 Assets/Scripts/DEVELOPMENT_GUIDE.md 进行开发

using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using FD.Bosses.C3;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// 大阵视觉效果管理器 - 处理所有视觉渲染和动画
    /// </summary>
    public class MatrixVisualEffects : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material motherMaterial;
        [SerializeField] private Material petalMaterial;
        [SerializeField] private Material starMaterial;
        [SerializeField] private Material arcMaterial;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Material groundGlyphMaterial;
        
        [Header("Colors")]
        [SerializeField] private Color redColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color greenColor = new Color(0.25f, 1f, 0.25f, 1f);
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f, 1f);
        [SerializeField] private Color whiteColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.1f, 0.1f, 1f);
        
        [Header("Animation Settings")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float glowIntensity = 1.5f;
        [SerializeField] private float dangerGlowIntensity = 2f;
        [SerializeField] private float arcFlowSpeed = 1f;
        
        // 渲染组件缓存（2D适配）
        private Dictionary<Transform, SpriteRenderer> spriteRendererCache = new Dictionary<Transform, SpriteRenderer>();
        private Dictionary<Transform, Light2D> lightCache = new Dictionary<Transform, Light2D>();
        private Dictionary<Transform, LineRenderer> lineRendererCache = new Dictionary<Transform, LineRenderer>();
        
        /// <summary>
        /// 更新母体颜色（2D适配）
        /// </summary>
        public void UpdateMotherColor(Transform mother, int index, BossColor bossColor, float intensity = 1f)
        {
            if (mother == null) return;
            
            SpriteRenderer renderer = GetSpriteRenderer(mother);
            if (renderer == null) return;
            
            Color baseColor = (bossColor == BossColor.Red) ? redColor : greenColor;
            Color outlineColor = (bossColor == BossColor.Red) ? greenColor : redColor;
            
            // 设置主颜色
            renderer.color = Color.Lerp(baseColor, Color.white, intensity * 0.3f);
            
            // 更新发光
            UpdateMotherBrightness(mother, intensity);
        }
        
        /// <summary>
        /// 更新花瓣颜色（2D适配）
        /// </summary>
        public void UpdatePetalColor(Transform petal, int motherIndex, int petalIndex, BossColor bossColor, FadedDreams.Player.ColorMode playerMode, float intensity = 1f)
        {
            if (petal == null) return;
            
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) return;
            
            // 花瓣交替红绿
            bool isRedPetal = (petalIndex % 2) == 0;
            Color baseColor = isRedPetal ? redColor : greenColor;
            
            // 危险花瓣高亮
            bool isDangerous = IsPetalDangerous(bossColor, playerMode, isRedPetal);
            if (isDangerous)
            {
                baseColor = Color.Lerp(baseColor, dangerColor, 0.7f);
                intensity *= dangerGlowIntensity;
            }
            
            renderer.color = Color.Lerp(baseColor, Color.white, intensity * 0.2f);
            
            // 更新发光
            UpdatePetalGlow(petal, intensity);
        }
        
        /// <summary>
        /// 更新星曜颜色（2D适配）
        /// </summary>
        public void UpdateStarColor(Transform star, int motherIndex, int starIndex, float intensity = 1f)
        {
            if (star == null) return;
            
            SpriteRenderer renderer = GetSpriteRenderer(star);
            if (renderer == null) return;
            
            // 白-金-薄青的金属流光过渡
            Color baseColor = Color.Lerp(whiteColor, goldColor, Mathf.Sin(Time.time * 2f + starIndex * 0.5f) * 0.5f + 0.5f);
            baseColor = Color.Lerp(baseColor, new Color(0.7f, 1f, 1f, 1f), 0.3f);
            
            renderer.color = Color.Lerp(baseColor, Color.white, intensity * 0.4f);
            
            // 更新发光
            UpdateStarGlow(star, intensity);
        }
        
        /// <summary>
        /// 更新拱弧亮度
        /// </summary>
        public void UpdateArcBrightness(Transform arc, float brightness)
        {
            if (arc == null) return;
            
            LineRenderer lr = GetLineRenderer(arc);
            if (lr == null) return;
            
            Color arcColor = Color.Lerp(goldColor * 0.3f, goldColor, brightness);
            lr.material.color = arcColor;
            lr.startWidth = 0.05f + brightness * 0.1f;
            lr.endWidth = 0.05f + brightness * 0.1f;
            
            // 光流效果
            if (brightness > 0.8f)
            {
                StartCoroutine(AnimateArcFlow(arc));
            }
        }
        
        /// <summary>
        /// 更新标记发光（2D适配）
        /// </summary>
        public void UpdateMarkerGlow(Transform marker, bool glow)
        {
            if (marker == null) return;
            
            SpriteRenderer renderer = GetSpriteRenderer(marker);
            if (renderer == null) return;
            
            Color baseColor = glow ? goldColor : goldColor * 0.3f;
            renderer.color = baseColor;
            
            // 添加发光效果
            Light2D light = GetLight(marker);
            if (light != null)
            {
                light.intensity = glow ? 1f : 0.2f;
                light.color = goldColor;
            }
        }
        
        /// <summary>
        /// 更新地纹强度（2D适配）
        /// </summary>
        public void UpdateGroundGlyphIntensity(Transform glyph, float intensity)
        {
            if (glyph == null) return;
            
            SpriteRenderer renderer = GetSpriteRenderer(glyph);
            if (renderer == null) return;
            
            Color glyphColor = goldColor * intensity;
            renderer.color = glyphColor;
        }
        
        /// <summary>
        /// 更新地纹流向
        /// </summary>
        public void UpdateGroundGlyphFlow(Transform glyph, bool tangentFlow)
        {
            if (glyph == null) return;
            
            // 实现切线流效果
            StartCoroutine(AnimateGroundFlow(glyph, tangentFlow));
        }
        
        /// <summary>
        /// 更新母体亮度
        /// </summary>
        public void UpdateMotherBrightness(Transform mother, float brightness)
        {
            if (mother == null) return;
            
            Light2D light = GetLight(mother);
            if (light != null)
            {
                light.intensity = brightness * glowIntensity;
            }
        }
        
        /// <summary>
        /// 更新花瓣危险状态
        /// </summary>
        public void UpdatePetalDanger(Transform petal, bool dangerous)
        {
            if (petal == null) return;
            
            if (dangerous)
            {
                StartCoroutine(AnimatePetalDanger(petal));
            }
        }
        
        /// <summary>
        /// 更新花瓣发光
        /// </summary>
        private void UpdatePetalGlow(Transform petal, float intensity)
        {
            Light2D light = GetLight(petal);
            if (light != null)
            {
                light.intensity = intensity * glowIntensity * 0.8f;
            }
        }
        
        /// <summary>
        /// 更新星曜发光
        /// </summary>
        private void UpdateStarGlow(Transform star, float intensity)
        {
            Light2D light = GetLight(star);
            if (light != null)
            {
                light.intensity = intensity * glowIntensity * 0.6f;
            }
        }
        
        /// <summary>
        /// 判断花瓣是否危险
        /// </summary>
        private bool IsPetalDangerous(BossColor bossColor, FadedDreams.Player.ColorMode playerMode, bool isRedPetal)
        {
            return (bossColor == BossColor.Red && playerMode == FadedDreams.Player.ColorMode.Green && isRedPetal) ||
                   (bossColor == BossColor.Green && playerMode == FadedDreams.Player.ColorMode.Red && !isRedPetal);
        }
        
        /// <summary>
        /// 获取SpriteRenderer组件（2D适配）
        /// </summary>
        private SpriteRenderer GetSpriteRenderer(Transform obj)
        {
            if (!spriteRendererCache.ContainsKey(obj))
            {
                spriteRendererCache[obj] = obj.GetComponent<SpriteRenderer>();
            }
            return spriteRendererCache[obj];
        }
        
        /// <summary>
        /// 获取光源组件
        /// </summary>
        private Light2D GetLight(Transform obj)
        {
            if (!lightCache.ContainsKey(obj))
            {
                lightCache[obj] = obj.GetComponent<Light2D>();
            }
            return lightCache[obj];
        }
        
        /// <summary>
        /// 获取线条渲染器组件
        /// </summary>
        private LineRenderer GetLineRenderer(Transform obj)
        {
            if (!lineRendererCache.ContainsKey(obj))
            {
                lineRendererCache[obj] = obj.GetComponent<LineRenderer>();
            }
            return lineRendererCache[obj];
        }
        
        /// <summary>
        /// 拱弧光流动画
        /// </summary>
        private IEnumerator AnimateArcFlow(Transform arc)
        {
            LineRenderer lr = GetLineRenderer(arc);
            if (lr == null) yield break;
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 光点沿弧线移动
                int pointCount = lr.positionCount;
                for (int i = 0; i < pointCount; i++)
                {
                    float glow = Mathf.Sin((t * Mathf.PI * 2f) - (i / (float)pointCount) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    // 这里可以实现更复杂的光点效果
                }
                
                yield return null;
            }
        }
        
        /// <summary>
        /// 地纹流向动画
        /// </summary>
        private IEnumerator AnimateGroundFlow(Transform glyph, bool tangentFlow)
        {
            // 实现地面网格的流向效果
            yield return new WaitForSeconds(0.1f);
        }
        
        /// <summary>
        /// 花瓣危险动画
        /// </summary>
        private IEnumerator AnimatePetalDanger(Transform petal)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            
            Vector3 originalScale = petal.localScale;
            Color originalColor = GetSpriteRenderer(petal)?.color ?? Color.white;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 缩放脉冲
                float scale = 1f + 0.2f * Mathf.Sin(t * Mathf.PI * 4f);
                petal.localScale = originalScale * scale;
                
                // 颜色闪烁
                SpriteRenderer renderer = GetSpriteRenderer(petal);
                if (renderer != null)
                {
                    Color flashColor = Color.Lerp(originalColor, dangerColor, Mathf.Sin(t * Mathf.PI * 8f) * 0.5f + 0.5f);
                    renderer.color = flashColor;
                }
                
                yield return null;
            }
            
            // 恢复原始状态
            petal.localScale = originalScale;
            if (GetSpriteRenderer(petal) != null)
            {
                GetSpriteRenderer(petal).color = originalColor;
            }
        }
        
        /// <summary>
        /// 清理缓存（2D适配）
        /// </summary>
        public void ClearCache()
        {
            spriteRendererCache.Clear();
            lightCache.Clear();
            lineRendererCache.Clear();
        }
    }
}
