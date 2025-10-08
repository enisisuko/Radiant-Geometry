using UnityEngine;

namespace FadedDreams.UI
{
    /// <summary>
    /// 中心旋转小球效果控制器
    /// 提供多种动画效果：旋转、脉冲、呼吸、轨道运动等
    /// </summary>
    public class CenterRotatingBall : MonoBehaviour
    {
        [Header("Rotation Settings")]
        public Vector3 rotationSpeed = new Vector3(0, 30, 0);
        public bool randomizeRotation = true;
        public float rotationVariation = 0.5f;
        
        [Header("Pulse Settings")]
        public bool enablePulse = true;
        public float pulseSpeed = 2f;
        public float pulseAmplitude = 0.1f;
        public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Breathing Effect")]
        public bool enableBreathing = true;
        public float breathingSpeed = 1f;
        public float breathingAmplitude = 0.05f;
        public Vector3 breathingAxis = Vector3.up;
        
        [Header("Orbital Motion")]
        public bool enableOrbital = false;
        public float orbitalRadius = 0.5f;
        public float orbitalSpeed = 1f;
        public Vector3 orbitalAxis = Vector3.forward;
        
        [Header("Color Animation")]
        public bool enableColorAnimation = true;
        public Color[] colorCycle = new Color[]
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };
        public float colorChangeSpeed = 1f;
        
        [Header("Hover Response")]
        public bool respondToHover = true;
        public float hoverScaleMultiplier = 1.2f;
        public float hoverResponseSpeed = 5f;
        
        private Vector3 basePosition;
        private Vector3 baseScale;
        private Vector3 currentRotationSpeed;
        private Renderer ballRenderer;
        private Material ballMaterial;
        private int currentColorIndex = 0;
        private float colorTransitionTime = 0f;
        private bool isHovered = false;
        private float targetScale = 1f;
        
        void Start()
        {
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            
            // 获取渲染器和材质
            ballRenderer = GetComponent<Renderer>();
            if (ballRenderer != null)
            {
                ballMaterial = ballRenderer.material;
            }
            
            // 初始化旋转速度
            if (randomizeRotation)
            {
                currentRotationSpeed = rotationSpeed + new Vector3(
                    Random.Range(-rotationVariation, rotationVariation),
                    Random.Range(-rotationVariation, rotationVariation),
                    Random.Range(-rotationVariation, rotationVariation)
                );
            }
            else
            {
                currentRotationSpeed = rotationSpeed;
            }
        }
        
        void Update()
        {
            UpdateRotation();
            UpdatePulse();
            UpdateBreathing();
            UpdateOrbital();
            UpdateColor();
            UpdateHoverResponse();
        }
        
        void UpdateRotation()
        {
            transform.Rotate(currentRotationSpeed * Time.deltaTime);
        }
        
        void UpdatePulse()
        {
            if (!enablePulse) return;
            
            float pulseValue = Mathf.Sin(Time.time * pulseSpeed);
            float normalizedPulse = (pulseValue + 1f) * 0.5f; // 0 to 1
            float curveValue = pulseCurve.Evaluate(normalizedPulse);
            float scaleMultiplier = 1f + (curveValue * pulseAmplitude);
            
            Vector3 newScale = baseScale * scaleMultiplier;
            transform.localScale = newScale;
        }
        
        void UpdateBreathing()
        {
            if (!enableBreathing) return;
            
            float breathingValue = Mathf.Sin(Time.time * breathingSpeed);
            Vector3 breathingOffset = breathingAxis * (breathingValue * breathingAmplitude);
            transform.localPosition = basePosition + breathingOffset;
        }
        
        void UpdateOrbital()
        {
            if (!enableOrbital) return;
            
            float angle = Time.time * orbitalSpeed;
            Vector3 orbitalOffset = new Vector3(
                Mathf.Cos(angle) * orbitalRadius,
                Mathf.Sin(angle) * orbitalRadius,
                0
            );
            
            // 根据轨道轴旋转偏移
            if (orbitalAxis != Vector3.forward)
            {
                Quaternion rotation = Quaternion.LookRotation(orbitalAxis);
                orbitalOffset = rotation * orbitalOffset;
            }
            
            transform.localPosition = basePosition + orbitalOffset;
        }
        
        void UpdateColor()
        {
            if (!enableColorAnimation || ballMaterial == null || colorCycle.Length < 2) return;
            
            colorTransitionTime += Time.deltaTime * colorChangeSpeed;
            
            if (colorTransitionTime >= 1f)
            {
                colorTransitionTime = 0f;
                currentColorIndex = (currentColorIndex + 1) % colorCycle.Length;
            }
            
            Color currentColor = Color.Lerp(
                colorCycle[currentColorIndex],
                colorCycle[(currentColorIndex + 1) % colorCycle.Length],
                colorTransitionTime
            );
            
            ballMaterial.color = currentColor;
        }
        
        void UpdateHoverResponse()
        {
            if (!respondToHover) return;
            
            float targetScaleValue = isHovered ? hoverScaleMultiplier : 1f;
            targetScale = Mathf.Lerp(targetScale, targetScaleValue, hoverResponseSpeed * Time.deltaTime);
            
            transform.localScale = baseScale * targetScale;
        }
        
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
        }
        
        public void SetColor(Color color)
        {
            if (ballMaterial != null)
            {
                ballMaterial.color = color;
            }
        }
        
        public void ResetToBase()
        {
            transform.localPosition = basePosition;
            transform.localScale = baseScale;
            isHovered = false;
            targetScale = 1f;
        }
        
        // 公共方法：设置旋转速度
        public void SetRotationSpeed(Vector3 speed)
        {
            currentRotationSpeed = speed;
        }
        
        // 公共方法：设置脉冲参数
        public void SetPulseSettings(float speed, float amplitude)
        {
            pulseSpeed = speed;
            pulseAmplitude = amplitude;
        }
        
        // 公共方法：激活动画效果
        public void ActivateSpecialEffect()
        {
            StartCoroutine(SpecialEffectCoroutine());
        }
        
        private System.Collections.IEnumerator SpecialEffectCoroutine()
        {
            Vector3 originalScale = transform.localScale;
            Color originalColor = ballMaterial != null ? ballMaterial.color : Color.white;
            
            // 快速缩放效果
            float duration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float scaleMultiplier = 1f + Mathf.Sin(progress * Mathf.PI * 4) * 0.3f;
                transform.localScale = originalScale * scaleMultiplier;
                
                if (ballMaterial != null)
                {
                    ballMaterial.color = Color.Lerp(originalColor, Color.white, Mathf.Sin(progress * Mathf.PI));
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 恢复原始状态
            transform.localScale = originalScale;
            if (ballMaterial != null)
            {
                ballMaterial.color = originalColor;
            }
        }
    }
}