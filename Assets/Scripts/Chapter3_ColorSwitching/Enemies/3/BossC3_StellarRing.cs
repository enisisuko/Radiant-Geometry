using UnityEngine;

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
        
        private LineRenderer lineRenderer;
        private SpriteRenderer spriteRenderer;
        
        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        public void Setup(float radius, float width, Color color, Material material)
        {
            this.radius = radius;
            this.width = width;
            this.ringColor = color;
            this.ringMaterial = material;
            
            if (lineRenderer != null)
            {
                lineRenderer.material = material;
                lineRenderer.startWidth = width;
                lineRenderer.endWidth = width;
                lineRenderer.color = color;
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
            if (lineRenderer != null) lineRenderer.color = color;
            if (spriteRenderer != null) spriteRenderer.color = color;
        }
        
        public void SetRadius(float newRadius)
        {
            radius = newRadius;
            UpdateRing();
        }
        
        public void SetWidth(float newWidth)
        {
            width = newWidth;
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