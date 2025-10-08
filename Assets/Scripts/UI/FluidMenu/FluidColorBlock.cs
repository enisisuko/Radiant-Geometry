using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 单个流体色块组件
    /// 负责管理色块的材质参数、动画和Shader效果
    /// </summary>
    public class FluidColorBlock : MonoBehaviour
    {
        [Header("渲染组件")]
        public Image image;
        public Renderer renderer3D;
        public Material material;
        
        [Header("动画设置")]
        public float breathScale = 0.05f;
        public float breathSpeed = 1.0f;
        public float hoverIntensity = 2.0f;
        public float baseIntensity = 1.0f;
        
        [Header("流体效果")]
        public float pressureRadius = 1.0f;
        public float distortionStrength = 0.02f;
        
        // 私有变量
        private MaterialPropertyBlock materialPropertyBlock;
        private FluidMenuManager.MenuItemConfig config;
        private int blockIndex;
        private bool isHovered = false;
        private bool isInitialized = false;
        
        // Shader属性ID（缓存以提高性能）
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int PressureCenterId = Shader.PropertyToID("_PressureCenter");
        private static readonly int PressureRadiusId = Shader.PropertyToID("_PressureRadius");
        private static readonly int PressureStrengthId = Shader.PropertyToID("_PressureStrength");
        private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
        private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
        private static readonly int BreathScaleId = Shader.PropertyToID("_BreathScale");
        private static readonly int BreathSpeedId = Shader.PropertyToID("_BreathSpeed");
        private static readonly int HoverScaleId = Shader.PropertyToID("_HoverScale");
        private static readonly int SqueezeScaleId = Shader.PropertyToID("_SqueezeScale");
        
        // 动画状态
        private float currentIntensity;
        private float targetIntensity;
        private float intensityVelocity;
        private float currentAlpha = 1f;
        private float targetAlpha = 1f;
        private float alphaVelocity;
        
        void Awake()
        {
            InitializeComponents();
        }
        
        void Start()
        {
            if (isInitialized)
            {
                StartBreathAnimation();
            }
        }
        
        void Update()
        {
            if (!isInitialized) return;
            
            UpdateAnimations();
            UpdateShaderParameters();
        }
        
        void InitializeComponents()
        {
            // 自动获取组件
            if (image == null) image = GetComponent<Image>();
            if (renderer3D == null) renderer3D = GetComponent<Renderer>();
            
            // 创建材质属性块
            materialPropertyBlock = new MaterialPropertyBlock();
            
            // 获取或创建材质
            if (material == null)
            {
                if (image != null && image.material != null)
                {
                    material = image.material;
                }
                else if (renderer3D != null && renderer3D.material != null)
                {
                    material = renderer3D.material;
                }
            }
        }
        
        public void Initialize(FluidMenuManager.MenuItemConfig config, int index)
        {
            this.config = config;
            this.blockIndex = index;
            
            // 设置基础颜色
            if (image != null)
            {
                image.color = config.primaryColor;
            }
            
            // 初始化强度
            currentIntensity = baseIntensity;
            targetIntensity = baseIntensity;
            
            isInitialized = true;
            
            // 开始呼吸动画
            StartBreathAnimation();
        }
        
        public void SetHovered(bool hovered)
        {
            if (isHovered == hovered) return;
            
            isHovered = hovered;
            targetIntensity = hovered ? hoverIntensity : baseIntensity;
        }
        
        public void UpdateShaderParameters(float scale, Vector3 position)
        {
            if (material == null) return;
            
            // 计算压力中心（基于位置）
            Vector4 pressureCenter = new Vector4(position.x, position.y, 0, 0);
            
            // 计算压力强度（基于缩放）
            float pressureStrength = Mathf.Clamp01((scale - 1f) * 2f);
            
            // 更新材质属性
            if (image != null)
            {
                material.SetColor(EmissionColorId, config.primaryColor);
                material.SetFloat(EmissionIntensityId, currentIntensity);
                material.SetVector(PressureCenterId, pressureCenter);
                material.SetFloat(PressureRadiusId, scale * pressureRadius);
                material.SetFloat(PressureStrengthId, pressureStrength);
                material.SetFloat(DistortionStrengthId, distortionStrength);
                material.SetFloat(WaveSpeedId, 2.0f);
                material.SetFloat(WaveFrequencyId, 10.0f);
                material.SetFloat(BreathScaleId, breathScale);
                material.SetFloat(BreathSpeedId, breathSpeed);
                material.SetFloat(HoverScaleId, isHovered ? 1.5f : 1.0f);
                material.SetFloat(SqueezeScaleId, 0.7f);
            }
            else if (renderer3D != null)
            {
                // 使用MaterialPropertyBlock避免材质实例化
                renderer3D.GetPropertyBlock(materialPropertyBlock);
                
                materialPropertyBlock.SetColor(EmissionColorId, config.primaryColor);
                materialPropertyBlock.SetFloat(EmissionIntensityId, currentIntensity);
                materialPropertyBlock.SetVector(PressureCenterId, pressureCenter);
                materialPropertyBlock.SetFloat(PressureRadiusId, scale * pressureRadius);
                materialPropertyBlock.SetFloat(PressureStrengthId, pressureStrength);
                materialPropertyBlock.SetFloat(DistortionStrengthId, distortionStrength);
                materialPropertyBlock.SetFloat(WaveSpeedId, 2.0f);
                materialPropertyBlock.SetFloat(WaveFrequencyId, 10.0f);
                materialPropertyBlock.SetFloat(BreathScaleId, breathScale);
                materialPropertyBlock.SetFloat(BreathSpeedId, breathSpeed);
                materialPropertyBlock.SetFloat(HoverScaleId, isHovered ? 1.5f : 1.0f);
                materialPropertyBlock.SetFloat(SqueezeScaleId, 0.7f);
                
                renderer3D.SetPropertyBlock(materialPropertyBlock);
            }
        }
        
        public void SetAlpha(float alpha)
        {
            targetAlpha = alpha;
        }
        
        public IEnumerator FadeIn(float duration)
        {
            float startAlpha = 0f;
            float elapsedTime = 0f;
            
            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                float currentAlpha = Mathf.Lerp(startAlpha, 1f, progress);
                
                SetAlpha(currentAlpha);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            SetAlpha(1f);
        }
        
        public IEnumerator FadeOut(float duration)
        {
            float startAlpha = currentAlpha;
            float elapsedTime = 0f;
            
            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                float currentAlpha = Mathf.Lerp(startAlpha, 0f, progress);
                
                SetAlpha(currentAlpha);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            SetAlpha(0f);
        }
        
        void StartBreathAnimation()
        {
            if (!isInitialized) return;
            
            // 呼吸动画通过Shader参数实现
            // 在UpdateShaderParameters中已经设置了BreathScale和BreathSpeed
        }
        
        void UpdateAnimations()
        {
            // 平滑强度变化
            currentIntensity = Mathf.SmoothDamp(currentIntensity, targetIntensity, ref intensityVelocity, 0.3f);
            
            // 平滑透明度变化
            currentAlpha = Mathf.SmoothDamp(currentAlpha, targetAlpha, ref alphaVelocity, 0.3f);
            
            // 应用透明度
            if (image != null)
            {
                Color color = image.color;
                color.a = currentAlpha;
                image.color = color;
            }
        }
        
        void UpdateShaderParameters()
        {
            // 这里可以添加实时更新的Shader参数
            // 比如基于时间的动画效果
        }
        
        public void TriggerClickEffect()
        {
            // 点击时的特殊效果
            StartCoroutine(ClickEffectCoroutine());
        }
        
        IEnumerator ClickEffectCoroutine()
        {
            float originalIntensity = currentIntensity;
            targetIntensity = originalIntensity * 3f;
            
            yield return new WaitForSeconds(0.1f);
            
            targetIntensity = originalIntensity;
        }
        
        public FluidMenuManager.MenuItemConfig GetConfig()
        {
            return config;
        }
        
        public int GetBlockIndex()
        {
            return blockIndex;
        }
        
        public bool IsHovered()
        {
            return isHovered;
        }
        
        public float GetCurrentIntensity()
        {
            return currentIntensity;
        }
        
        public float GetCurrentAlpha()
        {
            return currentAlpha;
        }
        
        public void SetColors(Color primaryColor, Color secondaryColor)
        {
            if (config != null)
            {
                config.primaryColor = primaryColor;
                config.secondaryColor = secondaryColor;
            }
            
            if (image != null)
            {
                image.color = primaryColor;
            }
        }
        
        public void SetName(string name)
        {
            if (config != null)
            {
                config.name = name;
            }
        }
        
        public void SetScale(float scale)
        {
            transform.localScale = Vector3.one * scale;
        }
        
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
        
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        void OnDestroy()
        {
            // 清理资源
            if (materialPropertyBlock != null)
            {
                materialPropertyBlock.Clear();
            }
        }
    }
}