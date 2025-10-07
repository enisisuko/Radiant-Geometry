// PlayerLightController.cs  — 简化版：仅能量与基础光照，无蓄力/爆发/光波
using UnityEngine;
using UnityEngine.Events;
#if UNITY_RENDERING_UNIVERSAL
using UnityEngine.Rendering.Universal; // Light2D
#endif
using FadedDreams.Core;
using FadedDreams.World;

namespace FadedDreams.Player
{
    public class PlayerLightController : MonoBehaviour
    {
        [Header("Energy")]
        public float maxEnergy = 100f;
        public float currentEnergy = 100f;
        [Tooltip("暗处每秒流失")]
        public float drainPerSecondInDark = 7f;
        [Tooltip("在光照内每秒恢复（缓慢回满）")]
        public float regenPerSecondNearLight = 20f;

        [Header("Light Sensing")]
        [Tooltip("认为“被光照到”的半径")]
        public float nearLightRadius = 2.5f;
        [Tooltip("只检测这些层的碰撞体以判断是否在光下（避免命中自己/地形）")]
        public LayerMask detectLightsMask = ~0;
        [Tooltip("认为“有光”的最小Light2D强度阈值")]
        public float minSenseIntensity = 0.15f;

        [Header("Baseline Light Mapping")]
        [Tooltip("能量=100% 时的基础强度")]
        public float baseIntensityAtFullEnergy = 1.2f;
        [Tooltip("能量=0% 时的基础强度")]
        public float baseIntensityAtZero = 0.1f;
        [Tooltip("能量=100% 时基础半径")]
        public float baseRadiusAtFullEnergy = 8f;
        [Tooltip("能量=0% 时基础半径")]
        public float baseRadiusAtZero = 2.5f;

        [Header("Color States")]
        public bool hasRed, hasGreen, hasBlue;
        public enum LightMode { None, Red, Green, Blue, White }
        public LightMode mode = LightMode.None;

        [Header("Events")]
        public UnityEvent onDeath;
        public UnityEvent onEnergyChanged;
        public UnityEvent onModeChanged;

#if UNITY_RENDERING_UNIVERSAL
        public Light2D playerLight2D; // 直接拖引用（推荐）
#endif
        [Tooltip("若未启用URP，可拖有 intensity/pointLightOuterRadius 的组件")]
        public Component light2DAny;

        public float Energy => Mathf.Clamp01(maxEnergy > 0 ? currentEnergy / maxEnergy : 0f);

        void Start()
        {
            UpdateMode(LightMode.None);
            PushLightByEnergy();
        }

        void Update()
        {
            HandleEnergy();       // 仅能量逻辑
            PushLightByEnergy();  // 仅按能量映射光照
        }

        void HandleEnergy()
        {
            bool lit = IsNearAnyLight();
            if (lit) currentEnergy += regenPerSecondNearLight * Time.deltaTime;
            else currentEnergy -= drainPerSecondInDark * Time.deltaTime;

            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
            onEnergyChanged?.Invoke();

            if (currentEnergy <= 0f)
            {
                onDeath?.Invoke();
                GameManager.Instance.OnPlayerDeath(); // 仍按你现有流程回检查点
            }
        }

        bool IsNearAnyLight()
        {
            var cols = Physics2D.OverlapCircleAll(transform.position, nearLightRadius, detectLightsMask);
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (!c) continue;
                if (c.transform == transform) continue;
                if (c.attachedRigidbody && c.attachedRigidbody.transform == transform) continue;
                if (c.CompareTag("Player")) continue;

                var ls = c.GetComponent<LightSource2D>();
                if (ls != null && ls.ProvidesLight(minSenseIntensity)) return true;

#if UNITY_RENDERING_UNIVERSAL
                var l2d = c.GetComponent<Light2D>();
                if (l2d != null && l2d.intensity >= minSenseIntensity) return true;
#endif
            }
            return false;
        }

        void PushLightByEnergy()
        {
            float e01 = Energy;
            float intensity = Mathf.Lerp(baseIntensityAtZero, baseIntensityAtFullEnergy, e01);
            float radius = Mathf.Lerp(baseRadiusAtZero, baseRadiusAtFullEnergy, e01);
            SetLight(intensity, radius);
        }

        void SetLight(float intensity, float radius)
        {
#if UNITY_RENDERING_UNIVERSAL
            if (playerLight2D)
            {
                playerLight2D.intensity = Mathf.Max(0f, intensity);
                playerLight2D.pointLightOuterRadius = Mathf.Max(0f, radius);
            }
#endif
            if (light2DAny) TrySetViaReflection(light2DAny, "intensity", Mathf.Max(0f, intensity));
            if (light2DAny) TrySetViaReflection(light2DAny, "pointLightOuterRadius", Mathf.Max(0f, radius));
        }

        void TrySetViaReflection(Component c, string name, float v)
        {
            if (!c) return;
            var t = c.GetType();
            var p = t.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(float)) { p.SetValue(c, v, null); return; }
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float)) { f.SetValue(c, v); }
        }

        // 仍保留模式切换占位（若其他系统用到）
        void UpdateMode(LightMode m) { mode = m; onModeChanged?.Invoke(); }

        // 对外能量接口 —— 供激光/道具等调用
        public void AddEnergy(float delta)
        {
            currentEnergy = Mathf.Clamp(currentEnergy + delta, 0f, maxEnergy);
            onEnergyChanged?.Invoke();
            if (currentEnergy <= 0f) { onDeath?.Invoke(); FadedDreams.Core.GameManager.Instance.OnPlayerDeath(); }
        }

        // 语义化别名：消耗能量（正数表示要扣多少）
        public void ConsumeEnergy(float amount) => AddEnergy(-Mathf.Abs(amount));

        // 兼容名：与部分旧代码一致
        public void ChangeEnergy(float delta) => AddEnergy(delta);

    }
}
