using UnityEngine;
using System.Collections;

namespace FD.Bosses.C3
{
    public class StellarRing : MonoBehaviour
    {
        [Header("Stellar Ring Settings")]
        public float radius = 5f;
        public float width = 0.5f;
        public Color ringColor = Color.white;
        public float fillAmount = 1f;
        public Material ringMaterial;
        
        [Header("Animation Settings")]
        public float burstIntensity = 1.5f;
        public float burstDuration = 0.5f;
        public float pingSize = 1.2f;
        public float pingDuration = 0.3f;
        
        private LineRenderer lineRenderer;
        private SpriteRenderer spriteRenderer;
        
        // 动画状态
        private float baseRadius;
        private float baseWidth;
        private bool isBursting = false;
        private bool isPinging = false;
        private float animationProgress = 0f;
        
        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            baseRadius = radius;
            baseWidth = width;
        }
        
        public void Setup(float radius, float width, Color color, Material material)
        {
            this.radius = radius;
            this.width = width;
            this.ringColor = color;
            this.ringMaterial = material;
            
            baseRadius = radius;
            baseWidth = width;
            
            if (lineRenderer != null)
            {
                lineRenderer.material = material;
                lineRenderer.startWidth = width;
                lineRenderer.endWidth = width;
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
            
            if (spriteRenderer != null)
            {
                spriteRenderer.material = material;
                spriteRenderer.color = color;
            }
        }
        
        public void SetMaterial(Material material)
        {
            ringMaterial = material;
            if (lineRenderer != null) lineRenderer.material = material;
            if (spriteRenderer != null) spriteRenderer.material = material;
        }
        
        public void SetFillAmount(float amount)
        {
            fillAmount = Mathf.Clamp01(amount);
            UpdateRing();
        }
        
        public void SetColor(Color color)
        {
            ringColor = color;
            if (lineRenderer != null) 
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
            if (spriteRenderer != null) spriteRenderer.color = color;
        }
        
        public void SetRadius(float newRadius)
        {
            radius = newRadius;
            baseRadius = newRadius;
            UpdateRing();
        }
        
        public void SetWidth(float newWidth)
        {
            width = newWidth;
            baseWidth = newWidth;
            
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = newWidth;
                lineRenderer.endWidth = newWidth;
            }
        }
        
        public void SetAlpha(float alpha)
        {
            Color color = ringColor;
            color.a = alpha;
            SetColor(color);
        }
        
        public float GetRadius()
        {
            return radius;
        }
        
        public float GetWidth()
        {
            return width;
        }
        
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        // === All-In-One 兼容接口 ===
        
        /// <summary>
        /// 爆发效果 - 环放大并增强
        /// </summary>
        public void Burst(float duration, float scale)
        {
            if (!gameObject.activeInHierarchy) return;
            
            if (isBursting)
            {
                StopAllCoroutines();
            }
            
            StartCoroutine(BurstCoroutine(duration, scale));
        }
        
        /// <summary>
        /// 在世界坐标位置创建脉冲效果
        /// </summary>
        public void PingAtWorld(Vector3 worldPos, float scale)
        {
            if (!gameObject.activeInHierarchy) return;
            
            StartCoroutine(PingCoroutine(worldPos, scale));
        }
        
        /// <summary>
        /// 每帧更新 - 根据Boss状态调整环的表现
        /// </summary>
        public void Tick(float deltaTime, Phase phase, BossColor color, float hpPercent)
        {
            // 根据血量调整环的颜色强度
            Color targetColor = color == BossColor.Red ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 1f, 0.2f);
            targetColor.a = Mathf.Lerp(0.3f, 1f, 1f - hpPercent);
            
            SetColor(Color.Lerp(ringColor, targetColor, deltaTime * 2f));
            
            // 根据阶段调整环的大小
            float targetRadius = phase == Phase.P1 ? baseRadius : baseRadius * 1.2f;
            radius = Mathf.Lerp(radius, targetRadius, deltaTime * 3f);
            
            UpdateRing();
        }
        
        /// <summary>
        /// 坍缩效果 - 环缩小并消失
        /// </summary>
        public void Collapse()
        {
            if (!gameObject.activeInHierarchy) return;
            
            StartCoroutine(CollapseCoroutine());
        }
        
        // === 协程实现 ===
        
        private IEnumerator BurstCoroutine(float duration, float scale)
        {
            isBursting = true;
            
            float elapsed = 0f;
            float startRadius = radius;
            float targetRadius = baseRadius * scale;
            float startWidth = width;
            float targetWidth = baseWidth * scale;
            
            // 爆发阶段
            while (elapsed < duration * 0.5f)
            {
                float t = elapsed / (duration * 0.5f);
                
                radius = Mathf.Lerp(startRadius, targetRadius, t);
                width = Mathf.Lerp(startWidth, targetWidth, t);
                
                if (lineRenderer != null)
                {
                    lineRenderer.startWidth = width;
                    lineRenderer.endWidth = width;
                }
                
                UpdateRing();
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 恢复阶段
            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                float t = elapsed / (duration * 0.5f);
                
                radius = Mathf.Lerp(targetRadius, baseRadius, t);
                width = Mathf.Lerp(targetWidth, baseWidth, t);
                
                if (lineRenderer != null)
                {
                    lineRenderer.startWidth = width;
                    lineRenderer.endWidth = width;
                }
                
                UpdateRing();
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            radius = baseRadius;
            width = baseWidth;
            isBursting = false;
        }
        
        private IEnumerator PingCoroutine(Vector3 worldPos, float scale)
        {
            isPinging = true;
            
            // 创建临时脉冲效果
            GameObject pingObj = new GameObject("StellarRing_Ping");
            pingObj.transform.position = worldPos;
            
            LineRenderer pingRenderer = pingObj.AddComponent<LineRenderer>();
            pingRenderer.material = ringMaterial;
            pingRenderer.startWidth = width * 0.5f;
            pingRenderer.endWidth = width * 0.5f;
            pingRenderer.startColor = ringColor;
            pingRenderer.endColor = ringColor;
            
            float elapsed = 0f;
            float startRadius = 0.1f;
            float targetRadius = baseRadius * scale;
            
            while (elapsed < pingDuration)
            {
                float t = elapsed / pingDuration;
                float currentRadius = Mathf.Lerp(startRadius, targetRadius, t);
                
                // 更新脉冲环
                int segments = 32;
                pingRenderer.positionCount = segments + 1;
                
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(
                        Mathf.Cos(angle) * currentRadius,
                        Mathf.Sin(angle) * currentRadius,
                        0f
                    );
                    pingRenderer.SetPosition(i, pos);
                }
                
                // 淡出
                Color c = ringColor;
                c.a = 1f - t;
                pingRenderer.startColor = c;
                pingRenderer.endColor = c;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            Destroy(pingObj);
            isPinging = false;
        }
        
        private IEnumerator CollapseCoroutine()
        {
            float elapsed = 0f;
            float startRadius = radius;
            float startWidth = width;
            Color startColor = ringColor;
            
            while (elapsed < burstDuration)
            {
                float t = elapsed / burstDuration;
                
                radius = Mathf.Lerp(startRadius, 0.1f, t);
                width = Mathf.Lerp(startWidth, 0.01f, t);
                
                Color c = startColor;
                c.a = 1f - t;
                SetColor(c);
                
                if (lineRenderer != null)
                {
                    lineRenderer.startWidth = width;
                    lineRenderer.endWidth = width;
                }
                
                UpdateRing();
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            SetVisible(false);
        }
        
        private void UpdateRing()
        {
            if (lineRenderer == null) return;
            
            int segments = Mathf.Max(32, Mathf.RoundToInt(radius * 8));
            int activeSegments = Mathf.RoundToInt(segments * fillAmount);
            
            lineRenderer.positionCount = activeSegments + 1;
            
            for (int i = 0; i <= activeSegments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );
                lineRenderer.SetPosition(i, pos);
            }
        }
    }
}