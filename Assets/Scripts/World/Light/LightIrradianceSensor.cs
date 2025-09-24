using System.Collections.Generic;
using UnityEngine;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // Light2D
#endif
using FadedDreams.World;                // LightSource2D
using FadedDreams.Player;               // PlayerLightController

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class LightIrradianceSensor : MonoBehaviour
    {
        [Header("Sampling Area")]
        [Tooltip("采样半径（世界单位），传感器从此半径内的光源/Light2D 累计光照")]
        public float radius = 2.5f;
        [Tooltip("用于 Physics2D.OverlapCircle 的 LayerMask（留空=全部）")]
        public LayerMask sampleMask = ~0;

        [Header("Normalization")]
        [Tooltip("认为“满格”所需的原始强度。越大则越不容易满格。")]
        public float fullIntensity = 5f;
        [Tooltip("每秒自然衰减（防止抖动），0=不衰减")]
        public float decayPerSecond = 0f;

        [Header("Hysteresis")]
        [Tooltip("达到此比例（0..1）判定为已满格")]
        [Range(0f, 1f)] public float saturateThreshold01 = 1f;
        [Tooltip("跌破此比例（0..1）判定为不满格（迟滞防抖）")]
        [Range(0f, 1f)] public float desaturateThreshold01 = 0.9f;

        [Header("Debug")]
        public bool drawGizmo = true;
        public Color gizmoColor = new Color(1, 1, 0, 0.2f);

        // 输出：原始强度与0..1 归一
        public float IrradianceRaw { get; private set; }
        public float Irradiance01 => fullIntensity <= 0f ? 0f : Mathf.Clamp01(IrradianceRaw / fullIntensity);
        public bool IsSaturated { get; private set; }

        // 缓存，避免 GC
        readonly Collider2D[] _hits = new Collider2D[32];

        void Update()
        {
            float raw = SampleIrradiance();
            if (decayPerSecond > 0f)
            {
                // 让读数更稳定：只在变强时立刻抬升，变弱时按秒衰减
                if (raw >= IrradianceRaw) IrradianceRaw = raw;
                else IrradianceRaw = Mathf.Max(0f, IrradianceRaw - decayPerSecond * Time.deltaTime);
            }
            else
            {
                IrradianceRaw = raw;
            }

            // 迟滞判断
            float k = Irradiance01;
            if (IsSaturated)
            {
                if (k < desaturateThreshold01) IsSaturated = false;
            }
            else
            {
                if (k >= saturateThreshold01) IsSaturated = true;
            }
        }

        float SampleIrradiance()
        {
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _hits, sampleMask);
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (!col) continue;

                // 1) 场景静态光源（自带强度/反射设置）
                var src = col.GetComponent<LightSource2D>();
                if (src != null)
                {
                    // 通过反射拿 intensity（LightSource2D 内部也用反射兜底）
                    var comp = src.light2DAny;
#if UNITY_RENDERING_UNIVERSAL
                    if (src.light2D) sum += Mathf.Max(0f, src.light2D.intensity);
                    else if (comp) sum += TryGetIntensityViaReflection(comp);
#else
                    if (comp) sum += TryGetIntensityViaReflection(comp);
#endif
                    continue;
                }

                // 2) URP Light2D 直接采样
#if UNITY_RENDERING_UNIVERSAL
                var l2d = col.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                if (l2d) { sum += Mathf.Max(0f, l2d.intensity); continue; }
#endif

                // 3) 玩家本体发光（取最近 Light2D 的 intensity）
                //    这里只处理“玩家”碰撞体：Tag=Player
                if (col.CompareTag("Player"))
                {
                    sum += EstimatePlayerLight(col.transform);
                }
            }

            return sum;
        }

        float EstimatePlayerLight(Transform player)
        {
            // 参照 ReadingStateController 的做法：寻找玩家子层级最近的 Light2D 作为基准亮度
            // （第1章没有激光输入，但玩家自身 Light2D 仍会根据能量映射强度）
            float best = 0f;
#if UNITY_RENDERING_UNIVERSAL
            var lights = player.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            float bestDist = float.MaxValue;
            foreach (var l in lights)
            {
                float d = (l.transform.position - player.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = Mathf.Max(best, l.intensity); }
            }
#else
            // 未启用 URP 定义时，尝试在子物体上反射拿 intensity 字段/属性名为 "Light2D"
            var comps = player.GetComponentsInChildren<Component>(true);
            float bestDist = float.MaxValue;
            foreach (var c in comps)
            {
                if (!c) continue;
                if (c.GetType().Name != "Light2D") continue;
                float d = (c.transform.position - player.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Mathf.Max(best, TryGetIntensityViaReflection(c));
                }
            }
#endif
            return best;
        }

        float TryGetIntensityViaReflection(Component comp)
        {
            if (!comp) return 0f;
            var t = comp.GetType();
            var p = t.GetProperty("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float))
            {
                object v = p.GetValue(comp, null);
                return v is float f ? Mathf.Max(0f, f) : 0f;
            }
            var f0 = t.GetField("intensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f0 != null && f0.FieldType == typeof(float))
            {
                object v = f0.GetValue(comp);
                return v is float f ? Mathf.Max(0f, f) : 0f;
            }
            return 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
