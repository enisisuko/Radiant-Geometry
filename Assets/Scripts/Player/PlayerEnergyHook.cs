// Scripts/Player/PlayerEnergyHook.cs
using UnityEngine;
using System.Reflection;

namespace FadedDreams.Player
{
    /// <summary>
    /// 外部能量控制挂钩：
    /// - SetEnergyLossPaused(true) 时启用“快速回充”（X 秒回满）。
    /// - 若目标组件有 Energy(0..1) 属性/字段则直接使用；否则回退为 currentEnergy/maxEnergy 的映射。
    /// </summary>
    public class PlayerEnergyHook : MonoBehaviour
    {
        [Tooltip("从此组件读取/写回能量（优先 Energy；否则使用 currentEnergy/maxEnergy 映射）。为空则在自身上找。")]
        public Component energyOwner;

        [Header("Fast Recharge Settings")]
        [Tooltip("开启读状态时，从当前值回满所需秒数（<=0 表示瞬间回满）。")]
        public float secondsToFullWhenPaused = 0.75f;

        private bool paused;
        // 反射缓存
        MemberInfo energyMember;          // Energy 属性或字段（0..1）
        FieldInfo currentEnergyField;     // float
        FieldInfo maxEnergyField;         // float

        void Awake()
        {
            if (!energyOwner) energyOwner = GetComponent<Component>();
            CacheMembers();
        }

        public void SetEnergyLossPaused(bool pause)
        {
            paused = pause;
        }

        void LateUpdate()
        {
            if (!paused) return;

            float cur = GetEnergy01();
            if (cur < 0f) return; // 找不到可写目标

            if (secondsToFullWhenPaused <= 0f)
            {
                SetEnergy01(1f);
                return;
            }

            float step = Time.deltaTime / Mathf.Max(0.0001f, secondsToFullWhenPaused);
            SetEnergy01(Mathf.Min(1f, cur + step));
        }

        // —— 反射与读写 —— //
        void CacheMembers()
        {
            if (energyOwner == null) return;
            var t = energyOwner.GetType();

            // 1) 优先 Energy 属性/字段（0..1）
            var pEnergyLower = t.GetProperty("energy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var pEnergy = pEnergyLower ?? t.GetProperty("Energy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pEnergy != null && pEnergy.PropertyType == typeof(float) && pEnergy.CanRead && pEnergy.CanWrite)
            {
                energyMember = pEnergy; return;
            }
            var fEnergyLower = t.GetField("energy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fEnergy = fEnergyLower ?? t.GetField("Energy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fEnergy != null && fEnergy.FieldType == typeof(float))
            {
                energyMember = fEnergy; return;
            }

            // 2) 回退为 currentEnergy/maxEnergy（你的 PLC 结构）
            currentEnergyField = t.GetField("currentEnergy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            maxEnergyField = t.GetField("maxEnergy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        float GetEnergy01()
        {
            if (energyOwner == null) return -1f;

            if (energyMember is PropertyInfo p)
            {
                object v = p.GetValue(energyOwner, null);
                return v is float f ? Mathf.Clamp01(f) : -1f;
            }
            if (energyMember is FieldInfo f0)
            {
                object v = f0.GetValue(energyOwner);
                return v is float f ? Mathf.Clamp01(f) : -1f;
            }

            if (currentEnergyField != null && maxEnergyField != null)
            {
                float cur = (float)currentEnergyField.GetValue(energyOwner);
                float max = Mathf.Max(0.0001f, (float)maxEnergyField.GetValue(energyOwner));
                return Mathf.Clamp01(cur / max);
            }
            return -1f;
        }

        void SetEnergy01(float v01)
        {
            v01 = Mathf.Clamp01(v01);
            if (energyOwner == null) return;

            if (energyMember is PropertyInfo p && p.CanWrite)
            {
                p.SetValue(energyOwner, v01, null);
                return;
            }
            if (energyMember is FieldInfo f0)
            {
                f0.SetValue(energyOwner, v01);
                return;
            }

            if (currentEnergyField != null && maxEnergyField != null)
            {
                float max = Mathf.Max(0.0001f, (float)maxEnergyField.GetValue(energyOwner));
                currentEnergyField.SetValue(energyOwner, v01 * max);
            }
        }
    }
}
