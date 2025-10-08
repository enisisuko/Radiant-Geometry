using UnityEngine;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 菜单光照系统
    /// 管理五个分区的光照效果，包括悬停增强、色彩变化、动态效果等
    /// </summary>
    public class MenuLightingSystem : MonoBehaviour
    {
        [Header("Light References")]
        public Light[] sectionLights = new Light[5];
        public Light globalAmbientLight;
        public Light centerSpotlight;
        
        [Header("Lighting Settings")]
        public float normalIntensity = 0.5f;
        public float hoverIntensity = 1.5f;
        public float transitionSpeed = 5f;
        public Color[] sectionLightColors = new Color[5]
        {
            new Color(1f, 0.3f, 0.3f), // 红色
            new Color(0.3f, 0.7f, 1f), // 蓝色
            new Color(0.8f, 0.3f, 1f), // 紫色
            new Color(1f, 0.8f, 0.3f), // 黄色
            new Color(0.3f, 1f, 0.3f)  // 绿色
        };
        
        [Header("Dynamic Effects")]
        public bool enableFlicker = true;
        public float flickerIntensity = 0.1f;
        public float flickerSpeed = 10f;
        public bool enablePulse = true;
        public float pulseAmplitude = 0.2f;
        public float pulseSpeed = 2f;
        
        [Header("Center Spotlight")]
        public bool enableCenterSpotlight = true;
        public float centerSpotIntensity = 2f;
        public float centerSpotRange = 10f;
        public Color centerSpotColor = Color.white;
        
        [Header("Ambient Control")]
        public bool enableAmbientControl = true;
        public Color ambientColor = new Color(0.1f, 0.1f, 0.2f);
        public float ambientIntensity = 0.3f;
        
        private int currentHoveredSection = -1;
        private float[] targetIntensities = new float[5];
        private Color[] targetColors = new Color[5];
        private bool[] isFlickering = new bool[5];
        private Coroutine[] flickerCoroutines = new Coroutine[5];
        
        void Start()
        {
            InitializeLights();
            SetupAmbientLighting();
        }
        
        void Update()
        {
            UpdateLightIntensities();
            UpdateLightColors();
            UpdateDynamicEffects();
            UpdateCenterSpotlight();
        }
        
        void InitializeLights()
        {
            // 初始化所有分区的光照
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                {
                    sectionLights[i].intensity = normalIntensity;
                    sectionLights[i].color = sectionLightColors[i];
                    targetIntensities[i] = normalIntensity;
                    targetColors[i] = sectionLightColors[i];
                }
            }
            
            // 设置中心聚光灯
            if (centerSpotlight != null && enableCenterSpotlight)
            {
                centerSpotlight.intensity = centerSpotIntensity;
                centerSpotlight.range = centerSpotRange;
                centerSpotlight.color = centerSpotColor;
                centerSpotlight.type = LightType.Spot;
            }
        }
        
        void SetupAmbientLighting()
        {
            if (globalAmbientLight != null && enableAmbientControl)
            {
                globalAmbientLight.color = ambientColor;
                globalAmbientLight.intensity = ambientIntensity;
                globalAmbientLight.type = LightType.Directional;
            }
        }
        
        void UpdateLightIntensities()
        {
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                {
                    // 计算目标强度
                    float targetIntensity = (i == currentHoveredSection) ? hoverIntensity : normalIntensity;
                    targetIntensities[i] = targetIntensity;
                    
                    // 平滑过渡到目标强度
                    sectionLights[i].intensity = Mathf.Lerp(
                        sectionLights[i].intensity, 
                        targetIntensities[i], 
                        transitionSpeed * Time.deltaTime
                    );
                }
            }
        }
        
        void UpdateLightColors()
        {
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                {
                    // 平滑过渡到目标颜色
                    sectionLights[i].color = Color.Lerp(
                        sectionLights[i].color,
                        targetColors[i],
                        transitionSpeed * Time.deltaTime
                    );
                }
            }
        }
        
        void UpdateDynamicEffects()
        {
            if (!enableFlicker && !enablePulse) return;
            
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                {
                    float effectMultiplier = 1f;
                    
                    // 闪烁效果
                    if (enableFlicker && isFlickering[i])
                    {
                        float flickerValue = Mathf.Sin(Time.time * flickerSpeed + i) * flickerIntensity;
                        effectMultiplier += flickerValue;
                    }
                    
                    // 脉冲效果
                    if (enablePulse)
                    {
                        float pulseValue = Mathf.Sin(Time.time * pulseSpeed + i) * pulseAmplitude;
                        effectMultiplier += pulseValue;
                    }
                    
                    // 应用效果
                    sectionLights[i].intensity = targetIntensities[i] * effectMultiplier;
                }
            }
        }
        
        void UpdateCenterSpotlight()
        {
            if (centerSpotlight != null && enableCenterSpotlight)
            {
                // 中心聚光灯跟随鼠标位置
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(Input.mousePosition.x, Input.mousePosition.y, centerSpotlight.transform.position.z)
                );
                
                centerSpotlight.transform.LookAt(mouseWorldPos);
                
                // 根据悬停状态调整强度
                float targetIntensity = (currentHoveredSection >= 0) ? centerSpotIntensity * 1.5f : centerSpotIntensity;
                centerSpotlight.intensity = Mathf.Lerp(centerSpotlight.intensity, targetIntensity, transitionSpeed * Time.deltaTime);
            }
        }
        
        public void SetHoveredSection(int sectionIndex)
        {
            currentHoveredSection = sectionIndex;
            
            // 更新目标颜色
            if (sectionIndex >= 0 && sectionIndex < sectionLightColors.Length)
            {
                for (int i = 0; i < targetColors.Length; i++)
                {
                    if (i == sectionIndex)
                    {
                        targetColors[i] = sectionLightColors[i];
                    }
                    else
                    {
                        // 其他区域稍微变暗
                        targetColors[i] = sectionLightColors[i] * 0.7f;
                    }
                }
            }
        }
        
        public void StartFlicker(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= isFlickering.Length) return;
            
            if (flickerCoroutines[sectionIndex] != null)
            {
                StopCoroutine(flickerCoroutines[sectionIndex]);
            }
            
            flickerCoroutines[sectionIndex] = StartCoroutine(FlickerCoroutine(sectionIndex));
        }
        
        public void StopFlicker(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= isFlickering.Length) return;
            
            isFlickering[sectionIndex] = false;
            
            if (flickerCoroutines[sectionIndex] != null)
            {
                StopCoroutine(flickerCoroutines[sectionIndex]);
                flickerCoroutines[sectionIndex] = null;
            }
        }
        
        IEnumerator FlickerCoroutine(int sectionIndex)
        {
            isFlickering[sectionIndex] = true;
            
            float duration = Random.Range(0.5f, 2f);
            float elapsed = 0f;
            
            while (elapsed < duration && isFlickering[sectionIndex])
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            isFlickering[sectionIndex] = false;
        }
        
        public void SetSectionColor(int sectionIndex, Color color)
        {
            if (sectionIndex >= 0 && sectionIndex < targetColors.Length)
            {
                targetColors[sectionIndex] = color;
            }
        }
        
        public void SetAllLightsColor(Color color)
        {
            for (int i = 0; i < targetColors.Length; i++)
            {
                targetColors[i] = color;
            }
        }
        
        public void ResetToDefault()
        {
            currentHoveredSection = -1;
            
            for (int i = 0; i < sectionLights.Length; i++)
            {
                if (sectionLights[i] != null)
                {
                    targetIntensities[i] = normalIntensity;
                    targetColors[i] = sectionLightColors[i];
                }
                
                StopFlicker(i);
            }
        }
        
        public void ActivateSpecialEffect(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= sectionLights.Length) return;
            
            StartCoroutine(SpecialEffectCoroutine(sectionIndex));
        }
        
        IEnumerator SpecialEffectCoroutine(int sectionIndex)
        {
            Light light = sectionLights[sectionIndex];
            if (light == null) yield break;
            
            Color originalColor = light.color;
            float originalIntensity = light.intensity;
            
            // 快速闪烁效果
            for (int i = 0; i < 5; i++)
            {
                light.color = Color.white;
                light.intensity = originalIntensity * 2f;
                yield return new WaitForSeconds(0.1f);
                
                light.color = originalColor;
                light.intensity = originalIntensity;
                yield return new WaitForSeconds(0.1f);
            }
            
            // 恢复原始状态
            light.color = originalColor;
            light.intensity = originalIntensity;
        }
    }
}