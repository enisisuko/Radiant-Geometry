using UnityEngine;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // Light2D
#endif

namespace FadedDreams.World
{
    /// <summary>
    /// 场景光源（统一版）：
    /// - 常亮(isConstant=true)：不掉能，亮度恒为 litIntensity。
    /// - 不常亮：有能量(0..100)，每秒 drainPerSecond；亮度按能量线性映射到 [unlitIntensity..litIntensity]。
    /// - 被其它光照到或 LightUp() 时：能量直接回到 100（可选播放爆燃→回落的强度动画）。
    /// - 提供 ProvidesLight(minThreshold) 给外部做“是否在发光”的判断。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class LightSource2D : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string uniqueId; // 保留ID供存档/统计使用（不再存 isOn/startOn）

        [Header("Mode")]
        [Tooltip("常亮：不掉能、亮度恒定；不常亮：有能量、会掉能、亮度随能量。")]
        public bool isConstant = false;

        [Header("Energy (Non-Constant)")]
        public float maxEnergy = 100f;
        public float currentEnergy = 100f;
        [Tooltip("不常亮时每秒能量流失")]
        public float drainPerSecond = 7f;

        [Header("Brightness Mapping")]
        [Range(0f, 15f)] public float litIntensity = 1f;
        [Tooltip("能量=0 时的最小强度（直接设值而不是倍率，更直观）")]
        [Range(0f, 15f)] public float unlitIntensity = 0.1f;
        [Tooltip("最低强度地板（避免完全黑掉）")]
        public float minIntensityFloor = 0f;

        [Header("Receiving Light (Auto-Refill)")]
        [Tooltip("被其它发光体照到时判定的半径")]
        public float receiveLightRadius = 2.5f;
        [Tooltip("认为“邻近有光”的最小 Light2D 强度阈值")]
        public float minSenseIntensity = 0.15f;
        [Tooltip("OverlapCircleAll 的层过滤（只勾选光/玩家等相关层）")]
        public LayerMask senseMask = ~0;

        [Header("Optional Visual Bindings")]
        public SpriteRenderer spriteRenderer;
        public bool dimAlphaInsteadOfColor = true;
        public float minAlphaWhenUnlit = 0.25f;

#if UNITY_RENDERING_UNIVERSAL
        public Light2D light2D; // 可选
#endif
        public Component light2DAny; // 兼容无URP时的通用槽（需有 float intensity）

        [Header("On-LightUp Burst (Optional)")]
        public bool enableLightOnBurst = true;
        public float burstRiseTime = 0.25f;
        public float burstFallTime = 1f;
        public float burstMultiplier = 2f;

        private Coroutine lightAnim;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;

            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
#if UNITY_RENDERING_UNIVERSAL
            if (!light2D) light2D = GetComponentInChildren<Light2D>();
            if (!light2DAny && light2D) light2DAny = light2D;
#else
            if (!light2DAny)
            {
                var comps = GetComponentsInChildren<Component>(true);
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    if (c.GetType().Name == "Light2D") { light2DAny = c; break; }
                }
            }
#endif
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                uniqueId = System.Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            maxEnergy = Mathf.Max(1f, maxEnergy);
        }

        private void Awake()
        {
            currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
            ApplyVisuals(); // 初始套用（不播放爆燃）
        }

        private void Update()
        {
            if (!isConstant)
            {
                // 被其它真正发光体照到 → 回满
                if (SenseNearbyLight())
                    currentEnergy = maxEnergy;
                else
                    currentEnergy -= drainPerSecond * Time.deltaTime;

                currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
            }

            // 实时按能量映射亮度
            ApplyVisuals();
        }

        /// <summary>主动点亮：用于被玩家爆发等命中时调用。</summary>
        public void LightUp()
        {
            currentEnergy = maxEnergy;
            ApplyNonIntensityVisualsForOn();

            if (enableLightOnBurst)
            {
                if (lightAnim != null) StopCoroutine(lightAnim);
                lightAnim = StartCoroutine(CoBurstToLit());
            }
            else
            {
                ApplyVisuals();
            }
        }

        /// <summary>
        /// 对外：这盏灯当前是否“在发光”（强度是否 ≥ 阈值）。
        /// </summary>
        public bool ProvidesLight(float minThreshold)
        {
            return CurrentIntensityForVisuals() >= Mathf.Max(0f, minThreshold);
        }

        // ================== 视觉与动画 ==================
        private void ApplyVisuals()
        {
            float targetIntensity = CurrentIntensityForVisuals();
            SetLightIntensity(targetIntensity);

            if (spriteRenderer)
            {
                if (dimAlphaInsteadOfColor)
                {
                    var c = spriteRenderer.color;
                    float baseAlpha = c.a <= 0f ? 1f : c.a;
                    // 用相对点亮度映射 alpha（在 litIntensity 上归一化）
                    float m = Mathf.Clamp01(targetIntensity / Mathf.Max(0.0001f, litIntensity));
                    float targetAlpha = Mathf.Lerp(minAlphaWhenUnlit, baseAlpha, m);
                    spriteRenderer.color = new Color(c.r, c.g, c.b, targetAlpha);
                }
                else
                {
                    var c = spriteRenderer.color;
                    float m = Mathf.Clamp01(targetIntensity / Mathf.Max(0.0001f, litIntensity));
                    spriteRenderer.color = new Color(c.r * m, c.g * m, c.b * m, c.a);
                }
            }
        }

        private float CurrentIntensityForVisuals()
        {
            if (isConstant) return Mathf.Max(minIntensityFloor, litIntensity);

            float e01 = maxEnergy <= 0f ? 0f : Mathf.Clamp01(currentEnergy / maxEnergy);
            float i = Mathf.Lerp(unlitIntensity, litIntensity, e01);
            return Mathf.Max(minIntensityFloor, i);
        }

        private void ApplyNonIntensityVisualsForOn()
        {
            if (!spriteRenderer) return;
            var c = spriteRenderer.color;
            if (dimAlphaInsteadOfColor)
            {
                float baseAlpha = c.a <= 0f ? 1f : c.a;
                spriteRenderer.color = new Color(c.r, c.g, c.b, baseAlpha);
            }
            else
            {
                spriteRenderer.color = new Color(c.r, c.g, c.b, c.a);
            }
        }

        private System.Collections.IEnumerator CoBurstToLit()
        {
            float peak = Mathf.Max(0f, litIntensity) * Mathf.Max(1f, burstMultiplier);
            float t = 0f;

            // 上升到峰值
            while (t < burstRiseTime)
            {
                float k = burstRiseTime <= 0f ? 1f : (t / burstRiseTime);
                float v = Mathf.Lerp(CurrentIntensityForVisuals(), peak, k);
                SetLightIntensity(v);
                t += Time.deltaTime;
                yield return null;
            }
            SetLightIntensity(peak);

            // 回落到按能量映射的当前强度
            t = 0f;
            while (t < burstFallTime)
            {
                float k = burstFallTime <= 0f ? 1f : (t / burstFallTime);
                float v = Mathf.Lerp(peak, CurrentIntensityForVisuals(), k);
                SetLightIntensity(v);
                t += Time.deltaTime;
                yield return null;
            }

            SetLightIntensity(CurrentIntensityForVisuals());
            lightAnim = null;
        }

        private void SetLightIntensity(float value)
        {
#if UNITY_RENDERING_UNIVERSAL
            if (light2D) light2D.intensity = value;
#endif
            if (light2DAny)
            {
                var type = light2DAny.GetType();
                var prop = type.GetProperty("intensity",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(float)) { prop.SetValue(light2DAny, value, null); return; }
                var field = type.GetField("intensity",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(float)) { field.SetValue(light2DAny, value); }
            }
        }

        // ================== 感知邻近“真正发光”的对象 ==================
        bool SenseNearbyLight()
        {
            var cols = Physics2D.OverlapCircleAll(transform.position, receiveLightRadius, senseMask);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (!c) continue;
                if (c.transform == transform) continue; // 忽略自己

                var otherLs = c.GetComponent<LightSource2D>();
                if (otherLs != null && otherLs != this && otherLs.ProvidesLight(minSenseIntensity)) return true;

#if UNITY_RENDERING_UNIVERSAL
                var l2d = c.GetComponent<Light2D>();
                if (l2d != null && l2d.intensity >= minSenseIntensity) return true;
#endif
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, receiveLightRadius);
        }
#endif
    }
}
