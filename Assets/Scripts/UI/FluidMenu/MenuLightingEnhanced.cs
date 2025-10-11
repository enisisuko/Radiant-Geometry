// MenuLightingEnhanced.cs
// 华丽光照系统 - 为3D流体主菜单提供动态光照效果
// 功能：呼吸动画、悬停增强、颜色渐变、光照同步

using UnityEngine;
using System.Collections.Generic;

namespace FadedDreams.UI
{
    /// <summary>
    /// 增强版菜单光照系统
    /// 提供华丽的动态光照效果
    /// </summary>
    public class MenuLightingEnhanced : MonoBehaviour
    {
        [Header("光源引用")]
        [Tooltip("主定向光")]
        public Light mainDirectionalLight;

        [Tooltip("环境点光源")]
        public Light ambientPointLight;

        [Tooltip("菜单项光源（6个）")]
        public Light[] menuItemLights = new Light[6];

        [Header("光照强度")]
        [Tooltip("基础强度")]
        public float baseIntensity = 2.5f;

        [Tooltip("悬停时的强度倍数")]
        public float hoverIntensityMultiplier = 2.0f;

        [Tooltip("选中时的强度倍数")]
        public float selectedIntensityMultiplier = 3.5f;

        [Header("呼吸动画")]
        [Tooltip("启用光照呼吸效果")]
        public bool enableBreathing = true;

        [Tooltip("呼吸速度")]
        public float breathingSpeed = 1.5f;

        [Tooltip("呼吸幅度")]
        [Range(0f, 0.5f)]
        public float breathingAmplitude = 0.15f;

        [Header("颜色渐变")]
        [Tooltip("启用颜色时间渐变")]
        public bool enableColorShift = true;

        [Tooltip("颜色渐变速度")]
        public float colorShiftSpeed = 0.2f;

        [Tooltip("颜色渐变幅度")]
        [Range(0f, 1f)]
        public float colorShiftAmount = 0.3f;

        [Header("光照范围")]
        [Tooltip("基础范围")]
        public float baseRange = 8f;

        [Tooltip("悬停时的范围倍数")]
        public float hoverRangeMultiplier = 1.3f;

        [Header("性能优化")]
        [Tooltip("启用LOD（远距离简化光照）")]
        public bool enableLOD = true;

        [Tooltip("更新频率（每秒）")]
        public int updateFrequency = 30;

        // 私有状态
        private float[] targetIntensities = new float[6];
        private float[] currentIntensities = new float[6];
        private Color[] baseColors = new Color[6];
        private float updateTimer = 0f;
        private int currentHoveredIndex = -1;

        private void Start()
        {
            InitializeLighting();
        }

        /// <summary>
        /// 初始化光照系统
        /// </summary>
        private void InitializeLighting()
        {
            // 自动查找光源（如果未手动指定）
            if (mainDirectionalLight == null)
            {
                GameObject mainLightGo = GameObject.Find("MainLight");
                if (mainLightGo != null)
                {
                    mainDirectionalLight = mainLightGo.GetComponent<Light>();
                }
            }

            if (ambientPointLight == null)
            {
                GameObject ambientGo = GameObject.Find("AmbientLight");
                if (ambientGo != null)
                {
                    ambientPointLight = ambientGo.GetComponent<Light>();
                }
            }

            // 初始化菜单项光源
            for (int i = 0; i < 6; i++)
            {
                if (menuItemLights[i] != null)
                {
                    baseColors[i] = menuItemLights[i].color;
                    targetIntensities[i] = baseIntensity;
                    currentIntensities[i] = baseIntensity;
                }
            }

            Debug.Log("[MenuLightingEnhanced] 光照系统初始化完成");
        }

        private void Update()
        {
            // 控制更新频率（性能优化）
            updateTimer += Time.deltaTime;
            float updateInterval = 1f / updateFrequency;

            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateLighting();
            }
        }

        /// <summary>
        /// 更新所有光照
        /// </summary>
        private void UpdateLighting()
        {
            // 更新呼吸效果
            if (enableBreathing)
            {
                UpdateBreathingEffect();
            }

            // 更新颜色渐变
            if (enableColorShift)
            {
                UpdateColorShift();
            }

            // 更新每个菜单项光源
            for (int i = 0; i < 6; i++)
            {
                if (menuItemLights[i] != null)
                {
                    UpdateMenuItemLight(i);
                }
            }
        }

        /// <summary>
        /// 更新呼吸效果
        /// </summary>
        private void UpdateBreathingEffect()
        {
            float breathValue = Mathf.Sin(Time.time * breathingSpeed) * breathingAmplitude;

            // 应用到环境光
            if (ambientPointLight != null)
            {
                float targetIntensity = 1.5f * (1f + breathValue);
                ambientPointLight.intensity = targetIntensity;
            }

            // 应用到所有菜单光源（微弱的呼吸）
            for (int i = 0; i < 6; i++)
            {
                if (menuItemLights[i] != null && i != currentHoveredIndex)
                {
                    targetIntensities[i] = baseIntensity * (1f + breathValue * 0.5f);
                }
            }
        }

        /// <summary>
        /// 更新颜色时间渐变
        /// </summary>
        private void UpdateColorShift()
        {
            float hueShift = Mathf.Sin(Time.time * colorShiftSpeed) * colorShiftAmount;

            for (int i = 0; i < 6; i++)
            {
                if (menuItemLights[i] != null)
                {
                    Color baseColor = baseColors[i];
                    Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                    
                    // 应用色相偏移
                    h = Mathf.Repeat(h + hueShift, 1f);
                    Color shiftedColor = Color.HSVToRGB(h, s, v);
                    
                    menuItemLights[i].color = shiftedColor;
                }
            }
        }

        /// <summary>
        /// 更新单个菜单项光照
        /// </summary>
        private void UpdateMenuItemLight(int index)
        {
            Light light = menuItemLights[index];
            if (light == null) return;

            // 平滑插值到目标强度
            currentIntensities[index] = Mathf.Lerp(
                currentIntensities[index],
                targetIntensities[index],
                Time.deltaTime * 5f
            );

            light.intensity = currentIntensities[index];
        }

        /// <summary>
        /// 设置悬停光照（外部调用）
        /// </summary>
        /// <param name="index">菜单项索引</param>
        /// <param name="isHovered">是否悬停</param>
        public void SetMenuItemHovered(int index, bool isHovered)
        {
            if (index < 0 || index >= 6) return;

            currentHoveredIndex = isHovered ? index : -1;

            for (int i = 0; i < 6; i++)
            {
                if (i == index && isHovered)
                {
                    // 悬停的光源：增强
                    targetIntensities[i] = baseIntensity * hoverIntensityMultiplier;
                    
                    if (menuItemLights[i] != null)
                    {
                        menuItemLights[i].range = baseRange * hoverRangeMultiplier;
                    }
                }
                else
                {
                    // 其他光源：恢复基础值
                    targetIntensities[i] = baseIntensity;
                    
                    if (menuItemLights[i] != null)
                    {
                        menuItemLights[i].range = baseRange;
                    }
                }
            }
        }

        /// <summary>
        /// 设置选中光照（外部调用）
        /// </summary>
        /// <param name="index">菜单项索引</param>
        public void SetMenuItemSelected(int index)
        {
            if (index < 0 || index >= 6) return;

            // 选中的光源：超强发光
            targetIntensities[index] = baseIntensity * selectedIntensityMultiplier;

            // 其他光源：暗淡
            for (int i = 0; i < 6; i++)
            {
                if (i != index)
                {
                    targetIntensities[i] = baseIntensity * 0.2f;
                }
            }
        }

        /// <summary>
        /// 重置所有光照到基础状态
        /// </summary>
        public void ResetAllLights()
        {
            currentHoveredIndex = -1;

            for (int i = 0; i < 6; i++)
            {
                targetIntensities[i] = baseIntensity;
                
                if (menuItemLights[i] != null)
                {
                    menuItemLights[i].range = baseRange;
                }
            }
        }

#if UNITY_EDITOR
        // 编辑器调试功能
        [ContextMenu("测试：悬停第一个选项")]
        private void TestHoverFirst()
        {
            SetMenuItemHovered(0, true);
        }

        [ContextMenu("测试：选中第一个选项")]
        private void TestSelectFirst()
        {
            SetMenuItemSelected(0);
        }

        [ContextMenu("测试：重置所有光照")]
        private void TestResetAll()
        {
            ResetAllLights();
        }
#endif
    }
}

