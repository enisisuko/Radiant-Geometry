// Fluid3DMenuItem.cs
// 3D流体菜单项组件
// 功能：悬停效果、点击反馈、发光动画、与光照系统联动

using UnityEngine;

namespace FadedDreams.UI
{
    /// <summary>
    /// 3D流体菜单项组件
    /// 处理单个菜单选项的视觉效果和交互
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Fluid3DMenuItem : MonoBehaviour
    {
        [Header("视觉设置")]
        [Tooltip("目标渲染器")]
        public Renderer targetRenderer;

        [Tooltip("基础发光颜色")]
        public Color baseEmission = Color.white;

        [Tooltip("基础发光强度")]
        public float baseEmissionIntensity = 2f;

        [Tooltip("悬停发光强度倍数")]
        public float hoverEmissionMultiplier = 2.5f;

        [Tooltip("选中发光强度倍数")]
        public float selectedEmissionMultiplier = 4f;

        [Header("缩放动画")]
        [Tooltip("启用呼吸动画")]
        public bool enableBreathing = true;

        [Tooltip("呼吸速度")]
        public float breathingSpeed = 1.2f;

        [Tooltip("呼吸幅度")]
        [Range(0f, 0.3f)]
        public float breathingAmplitude = 0.08f;

        [Tooltip("悬停缩放")]
        public float hoverScale = 1.3f;

        [Tooltip("选中缩放")]
        public float selectedScale = 1.5f;

        [Header("旋转动画")]
        [Tooltip("启用悬停旋转")]
        public bool enableHoverRotation = true;

        [Tooltip("旋转速度")]
        public float rotationSpeed = 30f;

        [Header("状态")]
        [Tooltip("是否可交互")]
        public bool interactable = true;

        // 私有状态
        private MaterialPropertyBlock mpb;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private Vector3 initialScale;
        private float hoverLerp = 0f;
        private float selectedPulse = 0f;
        private bool isHovered = false;
        private bool isSelected = false;

        private void Awake()
        {
            // 获取渲染器
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            // 创建MaterialPropertyBlock
            mpb = new MaterialPropertyBlock();

            // 保存初始缩放
            initialScale = transform.localScale;

            // 从材质获取基础发光颜色
            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                if (targetRenderer.sharedMaterial.HasProperty(EmissionColorID))
                {
                    baseEmission = targetRenderer.sharedMaterial.GetColor(EmissionColorID);
                }
            }
        }

        private void Update()
        {
            UpdateVisuals();
        }

        /// <summary>
        /// 更新视觉效果
        /// </summary>
        private void UpdateVisuals()
        {
            // 1. 更新发光
            UpdateEmission();

            // 2. 更新缩放
            UpdateScale();

            // 3. 更新旋转
            UpdateRotation();
        }

        /// <summary>
        /// 更新发光效果
        /// </summary>
        private void UpdateEmission()
        {
            if (targetRenderer == null) return;

            // 计算当前发光强度
            float emissionMultiplier = baseEmissionIntensity;

            // 呼吸效果
            if (enableBreathing && !isHovered && !isSelected)
            {
                float breathValue = Mathf.Sin(Time.time * breathingSpeed) * breathingAmplitude;
                emissionMultiplier *= (1f + breathValue);
            }

            // 悬停效果
            if (isHovered)
            {
                emissionMultiplier = Mathf.Lerp(
                    emissionMultiplier,
                    baseEmissionIntensity * hoverEmissionMultiplier,
                    hoverLerp
                );
            }

            // 选中脉冲效果
            if (selectedPulse > 0f)
            {
                float pulse = Mathf.SmoothStep(0f, 1f, selectedPulse);
                emissionMultiplier = Mathf.Lerp(
                    emissionMultiplier,
                    baseEmissionIntensity * selectedEmissionMultiplier,
                    pulse
                );
                
                selectedPulse = Mathf.MoveTowards(selectedPulse, 0f, Time.deltaTime * 2f);
            }

            // 不可交互时变暗
            if (!interactable)
            {
                emissionMultiplier *= 0.3f;
            }

            // 应用发光
            Color finalEmission = baseEmission * emissionMultiplier;
            
            mpb.Clear();
            targetRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorID, finalEmission);
            targetRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// 更新缩放效果
        /// </summary>
        private void UpdateScale()
        {
            float targetScale = 1f;

            // 呼吸缩放
            if (enableBreathing && !isHovered && !isSelected)
            {
                float breathValue = Mathf.Sin(Time.time * breathingSpeed * 0.8f) * breathingAmplitude;
                targetScale = 1f + breathValue;
            }

            // 悬停缩放
            if (isHovered)
            {
                targetScale = Mathf.Lerp(targetScale, hoverScale, hoverLerp);
            }

            // 选中缩放
            if (selectedPulse > 0f)
            {
                float pulse = Mathf.SmoothStep(0f, 1f, selectedPulse);
                targetScale = Mathf.Lerp(targetScale, selectedScale, pulse);
            }

            // 平滑应用缩放
            Vector3 currentScale = transform.localScale;
            Vector3 targetScaleVec = initialScale * targetScale;
            transform.localScale = Vector3.Lerp(currentScale, targetScaleVec, Time.deltaTime * 8f);
        }

        /// <summary>
        /// 更新旋转效果
        /// </summary>
        private void UpdateRotation()
        {
            if (enableHoverRotation && isHovered)
            {
                // 悬停时缓慢旋转
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime * hoverLerp);
            }
        }

        /// <summary>
        /// 设置悬停状态（外部调用）
        /// </summary>
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;

            // 平滑过渡
            if (hovered)
            {
                hoverLerp = Mathf.MoveTowards(hoverLerp, 1f, Time.deltaTime * 6f);
            }
            else
            {
                hoverLerp = Mathf.MoveTowards(hoverLerp, 0f, Time.deltaTime * 4f);
            }
        }

        /// <summary>
        /// 触发选中脉冲（外部调用）
        /// </summary>
        public void TriggerSelectedPulse()
        {
            selectedPulse = 1f;
            isSelected = true;
        }

        /// <summary>
        /// 设置可交互状态（外部调用）
        /// </summary>
        public void SetInteractable(bool canInteract)
        {
            interactable = canInteract;
            
            if (!interactable)
            {
                isHovered = false;
                hoverLerp = 0f;
            }
        }

        /// <summary>
        /// 重置状态
        /// </summary>
        public void Reset()
        {
            isHovered = false;
            isSelected = false;
            hoverLerp = 0f;
            selectedPulse = 0f;
            transform.localScale = initialScale;
        }

#if UNITY_EDITOR
        // 编辑器调试
        [ContextMenu("测试：触发悬停")]
        private void TestHover()
        {
            SetHovered(true);
        }

        [ContextMenu("测试：取消悬停")]
        private void TestUnhover()
        {
            SetHovered(false);
        }

        [ContextMenu("测试：触发选中")]
        private void TestSelect()
        {
            TriggerSelectedPulse();
        }
#endif
    }
}

