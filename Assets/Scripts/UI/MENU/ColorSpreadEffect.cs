using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 色彩蔓延效果系统
    /// 实现点击后色彩从选中区域向其他区域蔓延的视觉效果
    /// </summary>
    public class ColorSpreadEffect : MonoBehaviour
    {
        [Header("UI References")]
        public Image[] sectionImages = new Image[5];
        public Text[] sectionTexts = new Text[5];
        public ParticleSystem[] spreadParticles = new ParticleSystem[5];
        
        [Header("Spread Settings")]
        public float spreadDuration = 1.5f;
        public AnimationCurve spreadCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public Color[] originalColors = new Color[5];
        public Color[] spreadColors = new Color[5];
        
        [Header("Visual Effects")]
        public bool enableParticleEffect = true;
        public bool enableScaleAnimation = true;
        public bool enableTextGlow = true;
        public float scaleMultiplier = 1.1f;
        public float glowIntensity = 2f;
        
        [Header("Wave Effect")]
        public bool enableWaveEffect = true;
        public float waveSpeed = 3f;
        public float waveAmplitude = 0.1f;
        public int waveCount = 3;
        
        private bool isSpreading = false;
        private int currentSpreadSection = -1;
        private Coroutine spreadCoroutine;
        private Vector3[] originalScales = new Vector3[5];
        private Color[] originalTextColors = new Color[5];
        
        void Start()
        {
            InitializeOriginalValues();
            SetupParticleSystems();
        }
        
        void InitializeOriginalValues()
        {
            // 保存原始缩放值
            for (int i = 0; i < sectionImages.Length; i++)
            {
                if (sectionImages[i] != null)
                {
                    originalScales[i] = sectionImages[i].transform.localScale;
                }
            }
            
            // 保存原始文字颜色
            for (int i = 0; i < sectionTexts.Length; i++)
            {
                if (sectionTexts[i] != null)
                {
                    originalTextColors[i] = sectionTexts[i].color;
                }
            }
        }
        
        void SetupParticleSystems()
        {
            for (int i = 0; i < spreadParticles.Length; i++)
            {
                if (spreadParticles[i] != null)
                {
                    var main = spreadParticles[i].main;
                    main.startColor = spreadColors[i];
                    main.startLifetime = spreadDuration;
                    main.maxParticles = 50;
                    
                    var emission = spreadParticles[i].emission;
                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[]
                    {
                        new ParticleSystem.Burst(0, 50)
                    });
                }
            }
        }
        
        public void StartColorSpread(int sourceSectionIndex)
        {
            if (isSpreading || sourceSectionIndex < 0 || sourceSectionIndex >= sectionImages.Length)
                return;
            
            currentSpreadSection = sourceSectionIndex;
            
            if (spreadCoroutine != null)
                StopCoroutine(spreadCoroutine);
            
            spreadCoroutine = StartCoroutine(ColorSpreadCoroutine());
        }
        
        IEnumerator ColorSpreadCoroutine()
        {
            isSpreading = true;
            
            // 播放粒子效果
            if (enableParticleEffect && spreadParticles[currentSpreadSection] != null)
            {
                spreadParticles[currentSpreadSection].Play();
            }
            
            // 开始蔓延动画
            float elapsedTime = 0f;
            Color sourceColor = spreadColors[currentSpreadSection];
            
            while (elapsedTime < spreadDuration)
            {
                float progress = spreadCurve.Evaluate(elapsedTime / spreadDuration);
                
                // 更新所有分区的颜色
                for (int i = 0; i < sectionImages.Length; i++)
                {
                    if (sectionImages[i] != null)
                    {
                        UpdateSectionColor(i, sourceColor, progress);
                        UpdateSectionScale(i, progress);
                        UpdateSectionText(i, sourceColor, progress);
                    }
                }
                
                // 波浪效果
                if (enableWaveEffect)
                {
                    UpdateWaveEffect(progress);
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // 确保最终状态正确
            FinalizeSpreadEffect();
            
            isSpreading = false;
        }
        
        void UpdateSectionColor(int sectionIndex, Color sourceColor, float progress)
        {
            if (sectionImages[sectionIndex] == null) return;
            
            Color targetColor;
            if (sectionIndex == currentSpreadSection)
            {
                // 源区域保持原始颜色
                targetColor = Color.Lerp(originalColors[sectionIndex], sourceColor, progress);
            }
            else
            {
                // 其他区域向源颜色过渡
                targetColor = Color.Lerp(originalColors[sectionIndex], sourceColor, progress);
            }
            
            sectionImages[sectionIndex].color = targetColor;
        }
        
        void UpdateSectionScale(int sectionIndex, float progress)
        {
            if (!enableScaleAnimation || sectionImages[sectionIndex] == null) return;
            
            float scaleMultiplierValue = 1f;
            if (sectionIndex == currentSpreadSection)
            {
                // 源区域放大效果
                scaleMultiplierValue = Mathf.Lerp(1f, scaleMultiplier, progress);
            }
            else
            {
                // 其他区域稍微缩小
                scaleMultiplierValue = Mathf.Lerp(1f, 0.95f, progress);
            }
            
            sectionImages[sectionIndex].transform.localScale = originalScales[sectionIndex] * scaleMultiplierValue;
        }
        
        void UpdateSectionText(int sectionIndex, Color sourceColor, float progress)
        {
            if (!enableTextGlow || sectionTexts[sectionIndex] == null) return;
            
            Color targetTextColor;
            if (sectionIndex == currentSpreadSection)
            {
                // 源区域的文字发光效果
                targetTextColor = Color.Lerp(originalTextColors[sectionIndex], Color.white, progress);
            }
            else
            {
                // 其他区域文字稍微变暗
                targetTextColor = Color.Lerp(originalTextColors[sectionIndex], 
                    originalTextColors[sectionIndex] * 0.7f, progress);
            }
            
            sectionTexts[sectionIndex].color = targetTextColor;
            
            // 添加发光效果
            if (sectionIndex == currentSpreadSection)
            {
                sectionTexts[sectionIndex].fontStyle = FontStyle.Bold;
            }
        }
        
        void UpdateWaveEffect(float progress)
        {
            for (int i = 0; i < sectionImages.Length; i++)
            {
                if (sectionImages[i] != null)
                {
                    float waveOffset = Mathf.Sin(progress * Mathf.PI * waveCount + i) * waveAmplitude;
                    Vector3 wavePosition = sectionImages[i].transform.localPosition;
                    wavePosition.y += waveOffset * Time.deltaTime * waveSpeed;
                    sectionImages[i].transform.localPosition = wavePosition;
                }
            }
        }
        
        void FinalizeSpreadEffect()
        {
            // 确保所有区域都变成源颜色
            Color sourceColor = spreadColors[currentSpreadSection];
            
            for (int i = 0; i < sectionImages.Length; i++)
            {
                if (sectionImages[i] != null)
                {
                    sectionImages[i].color = sourceColor;
                    sectionImages[i].transform.localScale = originalScales[i];
                }
                
                if (sectionTexts[i] != null)
                {
                    sectionTexts[i].color = Color.white;
                    sectionTexts[i].fontStyle = FontStyle.Bold;
                }
            }
        }
        
        public void ResetToOriginal()
        {
            if (spreadCoroutine != null)
            {
                StopCoroutine(spreadCoroutine);
                spreadCoroutine = null;
            }
            
            isSpreading = false;
            currentSpreadSection = -1;
            
            // 恢复原始状态
            for (int i = 0; i < sectionImages.Length; i++)
            {
                if (sectionImages[i] != null)
                {
                    sectionImages[i].color = originalColors[i];
                    sectionImages[i].transform.localScale = originalScales[i];
                    sectionImages[i].transform.localPosition = Vector3.zero;
                }
                
                if (sectionTexts[i] != null)
                {
                    sectionTexts[i].color = originalTextColors[i];
                    sectionTexts[i].fontStyle = FontStyle.Normal;
                }
            }
        }
        
        public void SetOriginalColors(Color[] colors)
        {
            if (colors.Length == originalColors.Length)
            {
                originalColors = colors;
            }
        }
        
        public void SetSpreadColors(Color[] colors)
        {
            if (colors.Length == spreadColors.Length)
            {
                spreadColors = colors;
            }
        }
        
        public void SetSpreadDuration(float duration)
        {
            spreadDuration = duration;
        }
        
        public bool IsSpreading()
        {
            return isSpreading;
        }
        
        public int GetCurrentSpreadSection()
        {
            return currentSpreadSection;
        }
        
        // 特殊效果：彩虹蔓延
        public void StartRainbowSpread(int sourceSectionIndex)
        {
            if (isSpreading) return;
            
            currentSpreadSection = sourceSectionIndex;
            
            if (spreadCoroutine != null)
                StopCoroutine(spreadCoroutine);
            
            spreadCoroutine = StartCoroutine(RainbowSpreadCoroutine());
        }
        
        IEnumerator RainbowSpreadCoroutine()
        {
            isSpreading = true;
            
            Color[] rainbowColors = new Color[]
            {
                Color.red,
                Color.orange,
                Color.yellow,
                Color.green,
                Color.blue,
                Color.indigo,
                Color.violet
            };
            
            float elapsedTime = 0f;
            float colorChangeInterval = spreadDuration / rainbowColors.Length;
            
            while (elapsedTime < spreadDuration)
            {
                int colorIndex = Mathf.FloorToInt(elapsedTime / colorChangeInterval) % rainbowColors.Length;
                Color currentRainbowColor = rainbowColors[colorIndex];
                
                for (int i = 0; i < sectionImages.Length; i++)
                {
                    if (sectionImages[i] != null)
                    {
                        sectionImages[i].color = currentRainbowColor;
                    }
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            isSpreading = false;
        }
    }
}