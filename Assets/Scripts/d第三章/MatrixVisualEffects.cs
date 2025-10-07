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
        
        [Header("Matrix Visual Effects")]
        [SerializeField] private float bloomStrength = 1.2f;
        [SerializeField] private float chromaticAberrationOnLock = 0.1f;
        [SerializeField] private float warningPetalBoost = 0.25f;
        [SerializeField] private float groundGlyphIntensity = 0.3f;
        [SerializeField] private float outerTicksContrast = 0.8f;
        
        [Header("Phase Drift")]
        [SerializeField] private float globalPhaseSpeed = 0.5f;
        [SerializeField] private int[] lockBeatIndices = { 3, 6, 9, 12 };
        
        [Header("Emission Settings")]
        [SerializeField] private float bloomStrengthA = 1.0f; // 内环阵
        [SerializeField] private float bloomStrengthB = 0.8f; // 花瓣阵
        [SerializeField] private float bloomStrengthC = 0.6f; // 星曜阵
        [SerializeField] private float bloomStrengthD = 0.9f; // 拱弧阵
        
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
        /// 更新母体能量星环（随血量与红/绿失衡脉动）
        /// </summary>
        public void UpdateMotherEnergyRing(Transform mother, float healthRatio, BossColor bossColor, float phase)
        {
            if (mother == null) return;
            
            Light2D light = GetLight(mother);
            if (light != null)
            {
                // 根据血量调整强度
                float baseIntensity = healthRatio * glowIntensity;
                
                // 红/绿失衡脉动
                float imbalancePulse = Mathf.Sin(phase * 2f) * 0.3f + 0.7f;
                
                // 颜色根据Boss颜色调整
                Color ringColor = (bossColor == BossColor.Red) ? redColor : greenColor;
                ringColor = Color.Lerp(ringColor, Color.white, imbalancePulse * 0.4f);
                
                light.intensity = baseIntensity * imbalancePulse;
                light.color = ringColor;
            }
        }
        
        /// <summary>
        /// 更新花瓣金丝描边效果
        /// </summary>
        public void UpdatePetalGoldOutline(Transform petal, bool active, float intensity = 1f)
        {
            if (petal == null) return;
            
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) return;
            
            if (active)
            {
                // 金丝描边效果
                Color outlineColor = Color.Lerp(goldColor, Color.white, intensity * 0.5f);
                renderer.color = Color.Lerp(renderer.color, outlineColor, 0.3f);
                
                // 添加微镭射线效果
                StartCoroutine(AnimatePetalLaserSweep(petal));
            }
        }
        
        /// <summary>
        /// 更新星曜粒丝尾效果
        /// </summary>
        public void UpdateStarParticleTrail(Transform star, float speed, float intensity = 1f)
        {
            if (star == null) return;
            
            // 拖丝长度随速度略增
            float trailLength = 0.5f + speed * 0.1f;
            
            // 更新发光强度
            Light2D light = GetLight(star);
            if (light != null)
            {
                light.intensity = intensity * glowIntensity * 0.6f;
                light.pointLightOuterRadius = trailLength;
            }
        }
        
        /// <summary>
        /// 更新拱弧色散效果（锁扣拍时）
        /// </summary>
        public void UpdateArcChromaticAberration(Transform arc, bool isLockBeat, float intensity = 1f)
        {
            if (arc == null) return;
            
            LineRenderer lr = GetLineRenderer(arc);
            if (lr == null) return;
            
            if (isLockBeat)
            {
                // 色差外晕效果
                Color baseColor = goldColor;
                Color aberrationColor = Color.Lerp(baseColor, new Color(0.5f, 0.8f, 1f, 1f), chromaticAberrationOnLock);
                
                lr.material.color = Color.Lerp(baseColor, aberrationColor, intensity);
                lr.startWidth = 0.1f + intensity * 0.05f;
                lr.endWidth = 0.1f + intensity * 0.05f;
            }
            else
            {
                lr.material.color = goldColor * 0.7f;
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;
            }
        }
        
        /// <summary>
        /// 更新外轮刻整圈走光效果
        /// </summary>
        public void UpdateOuterTicksFullGlow(Transform[] markers, int currentBeat, float intensity = 1f)
        {
            if (markers == null) return;
            
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null) continue;
                
                // 计算当前应该亮的标记
                int markersPerBeat = markers.Length / 12;
                int startIndex = currentBeat * markersPerBeat;
                int endIndex = Mathf.Min(startIndex + markersPerBeat, markers.Length);
                
                bool shouldGlow = i >= startIndex && i < endIndex;
                
                SpriteRenderer renderer = GetSpriteRenderer(markers[i]);
                if (renderer != null)
                {
                    Color glowColor = shouldGlow ? goldColor : goldColor * 0.3f;
                    glowColor = Color.Lerp(glowColor, Color.white, intensity * outerTicksContrast);
                    renderer.color = glowColor;
                }
                
                Light2D light = GetLight(markers[i]);
                if (light != null)
                {
                    light.intensity = shouldGlow ? intensity * glowIntensity : 0.2f;
                }
            }
        }
        
        /// <summary>
        /// 更新地纹风向线效果
        /// </summary>
        public void UpdateGroundGlyphWindLines(Transform glyph, Vector3 dangerDirection, bool showWindLines, float intensity = 1f)
        {
            if (glyph == null) return;
            
            if (showWindLines)
            {
                // 实现风向线拉伸效果
                StartCoroutine(AnimateGroundWindLines(glyph, dangerDirection, intensity));
            }
        }
        
        /// <summary>
        /// 更新相性解压效果（玩家切对色时）
        /// </summary>
        public void UpdateColorCompatibilityRelief(Transform[] petals, bool isCompatible, float reliefAmount = 0.25f)
        {
            if (petals == null) return;
            
            foreach (var petal in petals)
            {
                if (petal == null) continue;
                
                Light2D light = GetLight(petal);
                if (light != null)
                {
                    if (isCompatible)
                    {
                        // 相性解压：光强回落25%
                        light.intensity *= (1f - reliefAmount);
                    }
                }
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
        /// 花瓣微镭射线扫过动画
        /// </summary>
        private IEnumerator AnimatePetalLaserSweep(Transform petal)
        {
            float duration = 0.3f;
            float elapsed = 0f;
            
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) yield break;
            
            Color originalColor = renderer.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 金丝描边扫过效果
                float sweepIntensity = Mathf.Sin(t * Mathf.PI) * 0.8f;
                Color sweepColor = Color.Lerp(originalColor, goldColor, sweepIntensity);
                renderer.color = sweepColor;
                
                yield return null;
            }
            
            // 恢复原始颜色
            renderer.color = originalColor;
        }
        
        /// <summary>
        /// 地纹风向线动画
        /// </summary>
        private IEnumerator AnimateGroundWindLines(Transform glyph, Vector3 dangerDirection, float intensity)
        {
            float duration = 1.0f;
            float elapsed = 0f;
            
            SpriteRenderer renderer = GetSpriteRenderer(glyph);
            if (renderer == null) yield break;
            
            Vector3 originalScale = glyph.localScale;
            Color originalColor = renderer.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 风向线拉伸效果
                Vector3 windScale = originalScale;
                windScale.x += dangerDirection.x * intensity * Mathf.Sin(t * Mathf.PI * 2f) * 0.1f;
                windScale.y += dangerDirection.y * intensity * Mathf.Sin(t * Mathf.PI * 2f) * 0.1f;
                glyph.localScale = windScale;
                
                // 颜色变化
                Color windColor = Color.Lerp(originalColor, goldColor, intensity * Mathf.Sin(t * Mathf.PI * 4f) * 0.5f + 0.5f);
                renderer.color = windColor;
                
                yield return null;
            }
            
            // 恢复原始状态
            glyph.localScale = originalScale;
            renderer.color = originalColor;
        }
        
        /// <summary>
        /// 母体吐息式放光动画
        /// </summary>
        public IEnumerator AnimateMotherBreath(Transform mother, float intensity = 1f)
        {
            float duration = 0.5f;
            float elapsed = 0f;
            
            Light2D light = GetLight(mother);
            if (light == null) yield break;
            
            float originalIntensity = light.intensity;
            Color originalColor = light.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 吐息式强度变化
                float breathIntensity = Mathf.Sin(t * Mathf.PI) * intensity * 0.5f + originalIntensity;
                light.intensity = breathIntensity;
                
                // 颜色微调
                Color breathColor = Color.Lerp(originalColor, Color.white, Mathf.Sin(t * Mathf.PI) * 0.3f);
                light.color = breathColor;
                
                yield return null;
            }
            
            // 恢复原始状态
            light.intensity = originalIntensity;
            light.color = originalColor;
        }
        
        /// <summary>
        /// 花瓣三段式动画：金丝描边→充填渐变→玻璃折射
        /// </summary>
        public IEnumerator AnimatePetalThreeStage(Transform petal, float delay = 0f)
        {
            yield return new WaitForSeconds(delay);
            
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) yield break;
            
            Color originalColor = renderer.color;
            Vector3 originalScale = petal.localScale;
            
            // 阶段1：金丝描边
            yield return StartCoroutine(AnimatePetalGoldOutline(petal, 0.2f));
            
            // 阶段2：充填渐变
            yield return StartCoroutine(AnimatePetalFill(petal, 0.3f));
            
            // 阶段3：玻璃折射
            yield return StartCoroutine(AnimatePetalRefraction(petal, 0.3f));
            
            // 恢复原始状态
            renderer.color = originalColor;
            petal.localScale = originalScale;
        }
        
        /// <summary>
        /// 花瓣金丝描边动画
        /// </summary>
        private IEnumerator AnimatePetalGoldOutline(Transform petal, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) yield break;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                Color goldOutline = Color.Lerp(renderer.color, goldColor, t);
                renderer.color = goldOutline;
                
                yield return null;
            }
        }
        
        /// <summary>
        /// 花瓣充填渐变动画
        /// </summary>
        private IEnumerator AnimatePetalFill(Transform petal, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) yield break;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 充填效果：从边缘到中心
                Color fillColor = Color.Lerp(renderer.color, Color.white, t * 0.5f);
                renderer.color = fillColor;
                
                yield return null;
            }
        }
        
        /// <summary>
        /// 花瓣玻璃折射动画
        /// </summary>
        private IEnumerator AnimatePetalRefraction(Transform petal, float duration)
        {
            float elapsed = 0f;
            SpriteRenderer renderer = GetSpriteRenderer(petal);
            if (renderer == null) yield break;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 玻璃折射效果：颜色微调和透明度变化
                Color refractionColor = Color.Lerp(renderer.color, new Color(0.8f, 0.9f, 1f, 1f), Mathf.Sin(t * Mathf.PI * 2f) * 0.3f);
                renderer.color = refractionColor;
                
                yield return null;
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
